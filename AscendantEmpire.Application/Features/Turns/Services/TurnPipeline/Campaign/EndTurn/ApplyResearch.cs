using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Technologies.Services;
using AscendantEmpire.Domain.Entities.Technologies;
using AscendantEmpire.Domain.Enums.Technologies;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AscendantEmpire.Application.Features.Turns.Services.TurnPipeline.Campaign.EndTurn;

/// <summary>
/// Spends whatever directed research points the Player is still holding on the project they selected, and closes the Turn's research books.
/// </summary>
/// <remarks>
/// <para>
/// The arithmetic lives in <see cref="ResearchBucketFill"/>, so the bucket rule can be pinned without a database. This class is the orchestration around it:
/// which row is being researched, whether the Technology is now discovered, and the once-per-Round guard.
/// </para>
/// <para>
/// <b>The pool always ends the Turn at zero.</b> Points a Player could not use are lost, which is <c>UnspentResearchPointsBehaviour.Lost</c>.
/// </para>
/// <para>
/// <b>Read-modify-write on <c>PlayerTechnology</c> is deliberate here</b>, despite that entity's remark that its point columns should be incremented with
/// <c>ExecuteUpdateAsync</c>. This step has to decide the clamp, the status and the two discovery stamps together, which no single bulk update can do
/// atomically, and it runs once per Round for one Player behind <c>LastResearchAppliedOnRound</c> - the same ground <c>SetResearchProjectHandler</c> already
/// stands on. The remark stays correct for passive accrual, which arrives from many sources at once.
/// </para>
/// </remarks>
internal class ApplyResearch(
    IDbContextFactory<AscendantEmpireContext> dbFactory,
    IPlayerTechnologyAvailabilityService technologyAvailabilityService,
    ILogger<ApplyResearch> logger) : IEndCampaignTurnProcess
{
    public string ItemName => "Apply Research";

    public int ExecutionOrder => (int)EndCampaignTurnProcessOrder.ApplyResearch;

    public async Task<string> ProcessAsync(TurnPipelineContextData context)
    {
        // TODO: GameConfiguration.UnspentResearchPointsBehaviour is not read. The task this was built for mandates the pool ending the Turn at zero, which is
        //       the Lost behaviour; honour Returned once something can carry points between Rounds.
        //
        // TODO: Overflow - carrying what is left onto a second unlocked Technology - is not done, so ResearchHistory.Overflowed is always zero. It is gated
        //       on GameConfiguration.ResearchOverflowEnabled and on the Research Coordinator specialist, neither of which anything reads yet.
        //
        // TODO: PlayerResearchState.NextTechnologyId is not promoted when the current project completes. That belongs with the start-of-turn "your project
        //       finished" notification, which does not exist; until then the Player is prompted by ResearchSelectionCheck instead.
        //
        // TODO: TechnologyGrant rows are not applied on discovery, so discovering a Technology unlocks nothing it grants. Most grant targets do not exist in
        //       their catalogues yet.

        if (context.PlayerId is null)
        {
            // The end of a Turn is always Player-scoped. A null here is a defect in the caller, and spending every Player's points would be worse than refusing.
            string error = $"{ItemName} was given an end-of-Turn context with no Player Id for Game Id {context.GameId}.";
            logger.LogError("{Error}", error);

            return error;
        }

        if (context.PlayerTurnId is not int playerTurnId)
        {
            // The Round's research summary is recorded against the Turn, so without one there is nothing to stamp the history row with.
            string error = $"{ItemName} was given an end-of-Turn context with no Player Turn Id for Game Id {context.GameId}.";
            logger.LogError("{Error}", error);

            return error;
        }

        int playerId = context.PlayerId.Value;

        await using AscendantEmpireContext db = await dbFactory.CreateDbContextAsync();

        // Tracked, because this row is about to be written.
        PlayerResearchState? researchState = await db.PlayerResearchStates.FirstOrDefaultAsync(state => state.PlayerId == playerId);

        if (researchState is null)
        {
            // Game setup writes a row for every Player unconditionally, so a missing one is a Game set up before the Technology schema existed. Skipped
            // rather than failed, so the Player can still end their Turn.
            logger.LogWarning(
                "Player Id {PlayerId} in Game Id {GameId} has no research state row, so no research was applied in Round {RoundNumber}.",
                playerId,
                context.GameId,
                context.CurrentRoundNumber);

            return string.Empty;
        }

        // Strictly less-than, so the spend happens exactly once per Round however many times this runs. Without it a retried end-of-Turn would pour the same
        // pool into the project twice.
        if (researchState.LastResearchAppliedOnRound >= context.CurrentRoundNumber)
        {
            return string.Empty;
        }

        int generatedPoints = researchState.UnspentDirectedPoints;
        int spentPoints = 0;

        if (researchState.CurrentTechnologyId is int currentTechnologyId)
        {
            spentPoints = await SpendOnProjectAsync(db, context, playerId, currentTechnologyId, researchState, generatedPoints);
        }

        researchState.UnspentDirectedPoints = 0;
        researchState.LastResearchAppliedOnRound = context.CurrentRoundNumber;

        // One row per Player per bucket per Round, written here rather than opened at the start of the Turn and updated, because ResearchHistory is
        // append-only and carries no RowVersion.
        //
        // Generated is read off the pool because nothing spends during a Turn yet, so what the Player is holding at the end of one is exactly what they were
        // given at the start. When in-turn spending lands, generation will have to be recorded where it happens instead.
        db.ResearchHistories.Add(new ResearchHistory
        {
            GameId = context.GameId,
            PlayerId = playerId,
            PlayerTurnId = playerTurnId,
            RoundNumber = context.CurrentRoundNumber,
            Bucket = ResearchBucket.Directed,
            Generated = generatedPoints,
            Spent = spentPoints,
            Overflowed = 0,
            Lost = generatedPoints - spentPoints
        });

        await db.SaveChangesAsync();

        logger.LogInformation(
            "Applied research for Player Id {PlayerId} in Game Id {GameId}, Round {RoundNumber}: {GeneratedPoints} generated, {SpentPoints} spent, "
            + "{LostPoints} lost.",
            playerId,
            context.GameId,
            context.CurrentRoundNumber,
            generatedPoints,
            spentPoints,
            generatedPoints - spentPoints);

        return string.Empty;
    }

    /// <summary>
    /// Pours the pool into the selected Technology and discovers it if that filled the last bucket.
    /// </summary>
    /// <returns>How many points landed in a bucket. Zero where the project cannot absorb any.</returns>
    /// <remarks>
    /// Nothing is saved here - the caller owns the single <c>SaveChangesAsync</c>, so the spend, the state stamps and the history row all land together or
    /// not at all.
    /// </remarks>
    private async Task<int> SpendOnProjectAsync(
        AscendantEmpireContext db,
        TurnPipelineContextData context,
        int playerId,
        int currentTechnologyId,
        PlayerResearchState researchState,
        int generatedPoints)
    {
        PlayerTechnology? playerTechnology = await db.PlayerTechnologies.FirstOrDefaultAsync(row => row.PlayerId == playerId && row.TechnologyId == currentTechnologyId);

        if (playerTechnology is null)
        {
            // A row exists for every Player and every Technology from game setup onwards, and the availability reconcile backfills any the catalogue gained since.
            // A missing one means the selection points at a Technology this Player has no row for, which is a defect rather than a game state.
            logger.LogWarning(
                "Player Id {PlayerId} in Game Id {GameId} is researching Technology Id {TechnologyId} but has no row for it, so nothing was spent in Round {RoundNumber}.",
                playerId,
                context.GameId,
                currentTechnologyId,
                context.CurrentRoundNumber);

            return 0;
        }

        if (playerTechnology.Status != TechnologyStatus.Unlocked)
        {
            // Directed research needs Unlocked - prerequisites discovered and the acquisition cost settled. SetResearchProjectHandler refuses anything else,
            // so this is unreachable by selection; it is here because a Technology could in principle be discovered by some other method while selected.
            logger.LogWarning(
                "Player Id {PlayerId} in Game Id {GameId} is researching Technology Id {TechnologyId}, which is {Status} rather than Unlocked, "
                + "so nothing was spent in Round {RoundNumber}.",
                playerId,
                context.GameId,
                currentTechnologyId,
                playerTechnology.Status,
                context.CurrentRoundNumber);

            return 0;
        }

        int spentPoints = ResearchBucketFill.Spend(playerTechnology, generatedPoints);

        if (ResearchBucketFill.IsComplete(playerTechnology) == false)
        {
            return spentPoints;
        }

        // CK_PlayerTechnology_StatusStamps wants a Discovered row to carry a DiscoveredOnRound and nothing else in particular, so AcquisitionPaidOnRound
        // stays exactly as it was - the cost was settled when the project was chosen and that remains true.
        playerTechnology.Status = TechnologyStatus.Discovered;
        playerTechnology.DiscoveredOnRound = context.CurrentRoundNumber;
        playerTechnology.DiscoveryMethod = TechnologyDiscoveryMethod.DirectedResearch;

        // Cleared, so the Player is asked to choose again. Left in place they would keep selecting a Discovered Technology, spend nothing every Round, and
        // never be prompted by ResearchSelectionCheck, which fires only on a null selection.
        researchState.CurrentTechnologyId = null;

        // The one owner of the Locked/Available question, so the dependents of what was just discovered become available here rather than being worked out
        // a second time in this file. It joins this unit of work and leaves the save to the caller.
        int reconciledCount = await technologyAvailabilityService.ReconcileAsync(db, context.GameId, playerId, CancellationToken.None);

        logger.LogInformation(
            "Player Id {PlayerId} in Game Id {GameId} discovered Technology Id {TechnologyId} by directed research in Round {RoundNumber}; "
            + "{ReconciledCount} row(s) changed status.",
            playerId,
            context.GameId,
            currentTechnologyId,
            context.CurrentRoundNumber,
            reconciledCount);

        return spentPoints;
    }
}
