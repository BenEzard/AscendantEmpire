using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Technologies.Services;
using AscendantEmpire.Domain.Entities.Technologies;
using AscendantEmpire.Domain.Enums.Technologies;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AscendantEmpire.Application.Features.Turns.Services.TurnPipeline.Campaign.EndTurn;

/// <summary>
/// Settles the Turn's experience, spends whatever directed research points the Player is still holding on the project they selected, and closes the Turn's
/// research books.
/// </summary>
/// <remarks>
/// <para>
/// <b>Experience is drained first.</b> It arrives already attached to the kind of activity that earned it and can only fill the one bucket, where directed
/// points spill into whichever bucket has headroom. Spending first would let that spill fill the experience bucket and leave the points the Player actually
/// earned with nowhere to go.
/// </para>
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
    IPlayerTechnologySustenanceModifierService sustenanceModifierService,
    IPlayerTechnologyExtractionEfficiencyService extractionEfficiencyService,
    IExperienceAllocator experienceAllocator,
    IExperienceAccrualReader experienceAccrualReader,
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
        //       their catalogues yet. Sustenance modifier and extraction efficiency grants are applied, by their own reconcilers.

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

        // Before the directed spend, so experience the Player earned fills the bucket it was earned for. The other way round, directed spill would fill that
        // bucket first and the earned points would arrive to find no headroom left.
        ExperienceDrainTotals experience = await DrainExperienceAsync(db, context, playerId, playerTurnId, researchState);

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

        // Written every Round whether or not anything was earned, so a Round of no progress reads as a zero rather than as a gap in the books.
        db.ResearchHistories.Add(new ResearchHistory
        {
            GameId = context.GameId,
            PlayerId = playerId,
            PlayerTurnId = playerTurnId,
            RoundNumber = context.CurrentRoundNumber,
            Bucket = ResearchBucket.Experience,
            Generated = experience.Generated,
            Spent = experience.Spent,
            Overflowed = 0,
            Lost = experience.Lost
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
    /// What one Turn's experience drain did with the points, for the Round's history row.
    /// </summary>
    private readonly record struct ExperienceDrainTotals(int Generated, int Spent, int Lost);

    /// <summary>
    /// Settles the Turn's experience: adds what this Turn's actions earned, splits every accrued type across the Technologies that consume it, and banks
    /// what nothing could take.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The recipient set is read once, before anything is distributed. A Technology this drain discovers therefore cannot also receive from it, and a
    /// dependent the discovery makes available waits for the next Round - which is what makes the split deterministic rather than dependent on the order
    /// types happen to be processed in.
    /// </para>
    /// <para>
    /// Nothing is saved here. The accrual, the split, the discoveries and both history rows land in the caller's single <c>SaveChangesAsync</c>, so a
    /// failure anywhere leaves the accrual rows untouched for the next attempt rather than consuming points that reached no bucket.
    /// </para>
    /// </remarks>
    private async Task<ExperienceDrainTotals> DrainExperienceAsync(
        AscendantEmpireContext db,
        TurnPipelineContextData context,
        int playerId,
        int playerTurnId,
        PlayerResearchState researchState)
    {
        // Loaded before the accrual runs. Rows it inserts are Added rather than stored, so a query made afterwards would not return them.
        List<PendingExperienceAccrual> accruals = await db.PendingExperienceAccruals
            .Where(row => row.PlayerId == playerId)
            .ToListAsync();

        await AccrueDerivedRulesAsync(db, context, playerId, playerTurnId);

        accruals.AddRange(db.ChangeTracker
            .Entries<PendingExperienceAccrual>()
            .Where(entry => entry.State == EntityState.Added && entry.Entity.PlayerId == playerId)
            .Select(entry => entry.Entity));

        Dictionary<int, int> pointsByExperienceTypeId = accruals
            .Where(accrual => accrual.Points > 0)
            .ToDictionary(accrual => accrual.ExperienceTypeId, accrual => accrual.Points);

        // The accumulator is emptied whether or not anything could be distributed, which is the contract PendingExperienceAccrual documents: nothing of this
        // Turn's survives into the next.
        db.PendingExperienceAccruals.RemoveRange(accruals);

        int generatedPoints = pointsByExperienceTypeId.Values.Sum();

        if (generatedPoints == 0)
        {
            return default;
        }

        List<PlayerTechnology> candidates = await db.PlayerTechnologies
            .Where(row => row.PlayerId == playerId
                       && (row.Status == TechnologyStatus.Available || row.Status == TechnologyStatus.Unlocked)
                       && row.ExperienceRequired != null
                       && row.ExperiencePoints < row.ExperienceRequired)
            .ToListAsync();

        ExperienceSplit split = ExperienceDistribution.Split(
            pointsByExperienceTypeId,
            await ReadRecipientsAsync(db, candidates));

        int spentPoints = await ApplySplitAsync(db, context, playerId, researchState, candidates, split);

        BankUnusedPoints(db, context, playerId, await ReadBankedUnusedAsync(db, playerId, split), split);

        logger.LogInformation(
            "Distributed experience for Player Id {PlayerId} in Game Id {GameId}, Round {RoundNumber}: {GeneratedPoints} accrued, {SpentPoints} allocated, "
            + "{UnusedPoints} banked as unused.",
            playerId,
            context.GameId,
            context.CurrentRoundNumber,
            generatedPoints,
            spentPoints,
            generatedPoints - spentPoints);

        return new ExperienceDrainTotals(generatedPoints, spentPoints, generatedPoints - spentPoints);
    }

    /// <summary>
    /// Adds what the Player's movement and construction this Turn earned to the accumulator.
    /// </summary>
    /// <remarks>
    /// A refused accrual is logged rather than failing the Turn. The experience catalogue is written by the database seed tool, so a Game on a database it
    /// has never run against has no types to accrue against, and that must not stop Players ending their Turns.
    /// </remarks>
    private async Task AccrueDerivedRulesAsync(AscendantEmpireContext db, TurnPipelineContextData context, int playerId, int playerTurnId)
    {
        IReadOnlyDictionary<string, int> earned = await experienceAccrualReader.ReadForTurnAsync(db, playerId, playerTurnId, CancellationToken.None);

        if (earned.Count == 0)
        {
            return;
        }

        ServiceResultBase result = await experienceAllocator.AccrueAsync(db, context.GameId, playerId, earned, CancellationToken.None);

        if (result.HasSucceeded)
        {
            return;
        }

        logger.LogWarning(
            "Experience earned by Player Id {PlayerId} in Game Id {GameId} during Round {RoundNumber} was refused, so none of it was distributed: {Errors}",
            playerId,
            context.GameId,
            context.CurrentRoundNumber,
            string.Join("; ", result.Errors));
    }

    /// <summary>
    /// Turns the Technologies that could still take experience into the shape the split works on.
    /// </summary>
    /// <remarks>A candidate no experience type feeds is left out, because it could never be a consumer of anything the split is dividing.</remarks>
    private static async Task<ExperienceRecipient[]> ReadRecipientsAsync(AscendantEmpireContext db, List<PlayerTechnology> candidates)
    {
        if (candidates.Count == 0)
        {
            return [];
        }

        int[] candidateIds = [.. candidates.Select(row => row.TechnologyId)];

        var links = await db.TechnologyExperienceTypes
            .AsNoTracking()
            .Where(link => candidateIds.Contains(link.TechnologyId))
            .Select(link => new { link.TechnologyId, link.ExperienceTypeId })
            .ToArrayAsync();

        Dictionary<int, HashSet<int>> typeIdsByTechnologyId = links
            .GroupBy(link => link.TechnologyId)
            .ToDictionary(group => group.Key, group => group.Select(link => link.ExperienceTypeId).ToHashSet());

        return
        [
            .. candidates
                .Where(row => typeIdsByTechnologyId.ContainsKey(row.TechnologyId))
                .Select(row => new ExperienceRecipient(
                    row.TechnologyId,
                    row.ExperienceRequired!.Value - row.ExperiencePoints,
                    typeIdsByTechnologyId[row.TechnologyId]))
        ];
    }

    /// <summary>
    /// Adds each Technology's allocation to its experience bucket and discovers the ones that completes.
    /// </summary>
    /// <returns>How many points landed in a bucket.</returns>
    private async Task<int> ApplySplitAsync(
        AscendantEmpireContext db,
        TurnPipelineContextData context,
        int playerId,
        PlayerResearchState researchState,
        List<PlayerTechnology> candidates,
        ExperienceSplit split)
    {
        Dictionary<int, PlayerTechnology> candidatesById = candidates.ToDictionary(row => row.TechnologyId);

        int spentPoints = 0;
        List<int> discoveredTechnologyIds = [];

        foreach ((int technologyId, int points) in split.PointsByTechnologyId)
        {
            PlayerTechnology playerTechnology = candidatesById[technologyId];

            playerTechnology.ExperiencePoints += points;
            spentPoints += points;

            if (ResearchBucketFill.IsComplete(playerTechnology) == false)
            {
                continue;
            }

            // CK_PlayerTechnology_StatusStamps asks a Discovered row only for a DiscoveredOnRound, so a Technology completed from Available keeps its null
            // AcquisitionPaidOnRound - experience never required the cost to be settled.
            playerTechnology.Status = TechnologyStatus.Discovered;
            playerTechnology.DiscoveredOnRound = context.CurrentRoundNumber;
            playerTechnology.DiscoveryMethod = TechnologyDiscoveryMethod.Experience;

            discoveredTechnologyIds.Add(technologyId);

            if (researchState.CurrentTechnologyId == technologyId)
            {
                // The project finished itself without the pool, so the selection is cleared for the same reason a directed completion clears it.
                researchState.CurrentTechnologyId = null;
            }
        }

        if (discoveredTechnologyIds.Count == 0)
        {
            return spentPoints;
        }

        // Once for the whole drain rather than once per discovery: all three are convergent, so a single pass over the settled state is both cheaper and
        // exactly as correct.
        int reconciledCount = await technologyAvailabilityService.ReconcileAsync(db, context.GameId, playerId, CancellationToken.None);
        int modifierCount = await sustenanceModifierService.ReconcileAsync(db, context.GameId, playerId, CancellationToken.None);
        int efficiencyCount = await extractionEfficiencyService.ReconcileAsync(db, context.GameId, playerId, CancellationToken.None);

        logger.LogInformation(
            "Player Id {PlayerId} in Game Id {GameId} discovered Technology Id(s) {TechnologyIds} by experience in Round {RoundNumber}; "
            + "{ReconciledCount} row(s) changed status, {ModifierCount} sustenance modifier(s) and {EfficiencyCount} extraction efficiency modifier(s) were reconciled.",
            playerId,
            context.GameId,
            string.Join(", ", discoveredTechnologyIds),
            context.CurrentRoundNumber,
            reconciledCount,
            modifierCount,
            efficiencyCount);

        return spentPoints;
    }

    /// <summary>
    /// The Player's existing unused-experience rows for the types this split wasted, so they can be added to rather than duplicated.
    /// </summary>
    private static async Task<List<PlayerUnusedExperience>> ReadBankedUnusedAsync(AscendantEmpireContext db, int playerId, ExperienceSplit split)
    {
        if (split.UnusedPointsByExperienceTypeId.Count == 0)
        {
            return [];
        }

        int[] experienceTypeIds = [.. split.UnusedPointsByExperienceTypeId.Keys];

        return await db.PlayerUnusedExperience
            .Where(row => row.PlayerId == playerId && experienceTypeIds.Contains(row.ExperienceTypeId))
            .ToListAsync();
    }

    /// <summary>
    /// Adds what nothing could take to the Player's lifetime unused counters.
    /// </summary>
    /// <remarks>A dead end by design - these points are never spent later. They exist so Players can be compared on what they wasted.</remarks>
    private static void BankUnusedPoints(
        AscendantEmpireContext db,
        TurnPipelineContextData context,
        int playerId,
        List<PlayerUnusedExperience> banked,
        ExperienceSplit split)
    {
        foreach ((int experienceTypeId, int points) in split.UnusedPointsByExperienceTypeId)
        {
            PlayerUnusedExperience? existing = banked.FirstOrDefault(row => row.ExperienceTypeId == experienceTypeId);

            if (existing is null)
            {
                db.PlayerUnusedExperience.Add(new PlayerUnusedExperience
                {
                    GameId = context.GameId,
                    PlayerId = playerId,
                    ExperienceTypeId = experienceTypeId,
                    Points = points
                });

                continue;
            }

            existing.Points += points;
        }
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

        // Likewise the one owner of which sustenance modifiers the Player holds for what they have discovered. Convergent, so what it writes here is the
        // same whether this is the Player's first discovery or their fiftieth.
        int modifierCount = await sustenanceModifierService.ReconcileAsync(db, context.GameId, playerId, CancellationToken.None);
        int efficiencyCount = await extractionEfficiencyService.ReconcileAsync(db, context.GameId, playerId, CancellationToken.None);

        logger.LogInformation(
            "Player Id {PlayerId} in Game Id {GameId} discovered Technology Id {TechnologyId} by directed research in Round {RoundNumber}; "
            + "{ReconciledCount} row(s) changed status, {ModifierCount} sustenance modifier(s) and {EfficiencyCount} extraction efficiency modifier(s) were reconciled.",
            playerId,
            context.GameId,
            currentTechnologyId,
            context.CurrentRoundNumber,
            reconciledCount,
            modifierCount,
            efficiencyCount);

        return spentPoints;
    }
}
