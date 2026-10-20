using AscendantEmpire.Application.Data;
using AscendantEmpire.Domain.Entities.Technologies;
using AscendantEmpire.Domain.Entities.UnitDesigns;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AscendantEmpire.Application.Features.Turns.Services.TurnPipeline.Campaign.StartTurn;

/// <summary>
/// Works out how many directed research points the Player's people produce for the Turn that is opening, and hands them the figure.
/// </summary>
/// <remarks>
/// <para>
/// The calculation is the design doc's first cut: <c>Floor((settlement population + camping-force population) / TechPointsPerPopulationSize)</c>. Everybody
/// counts once - a Force that founded a village was consumed by it, and one that founded a camp had its strength decremented by exactly the population taken
/// - so summing settlements and settler Forces together cannot double anybody.
/// </para>
/// <para>
/// <b>Forces are matched on capability tags, not on a component code.</b> <c>CreateVillageStrategy</c> and <c>EstablishCampStrategy</c> already gate on
/// <c>create-settlement</c> and <c>create-early-camp</c>, both carried by the <c>camping-materials</c> equipment component, and a second camping component
/// authored later would be picked up here without this query knowing about it.
/// </para>
/// <para>
/// <b>Concurrency.</b> The Round stamp closes the replay window only once the first run has committed, exactly as <c>ReplenishMovementPoints</c> does, and
/// for the same reason: campaign Turns are handed over one at a time, so two overlapping start-of-Turn runs for one Player are not reachable today.
/// </para>
/// </remarks>
internal class GenerateResearchPoints(
    IDbContextFactory<AscendantEmpireContext> dbFactory,
    ILogger<GenerateResearchPoints> logger) : IStartCampaignTurnProcess
{
    public string ItemName => "Generate Research Points";

    public int ExecutionOrder => (int)StartCampaignTurnProcessOrder.GenerateResearchPoints;

    public async Task<string> ProcessAsync(TurnPipelineContextData context)
    {
        // TODO: PlayerResearchModifier rows are not folded in yet. The design's formula is a base figure plus the modifiers a Player holds, and the entity
        //       already models the flat and percentage parts - but nothing writes a modifier row, so applying them here would be dead code that no test
        //       could reach. Fold them in when a Technology, Specialist or Department first grants one.

        if (context.PlayerId is null)
        {
            // The start of a Turn is always Player-scoped - CampaignRoundService builds this context with the four-argument constructor. A null here is a
            // defect in the caller, and generating points for every Player in the Game would be far worse than refusing.
            string error = $"{ItemName} was given a start-of-Turn context with no Player Id for Game Id {context.GameId}.";
            logger.LogError("{Error}", error);

            return error;
        }

        int playerId = context.PlayerId.Value;

        await using AscendantEmpireContext db = await dbFactory.CreateDbContextAsync();

        // Tracked, because this row is about to be written.
        PlayerResearchState? researchState = await db.PlayerResearchStates
            .FirstOrDefaultAsync(state => state.PlayerId == playerId);

        if (researchState is null)
        {
            // Game setup writes a row for every Player unconditionally, so a missing one is a Game set up before the Technology schema existed. Skipped
            // rather than failed: the pipeline is fail-fast, and refusing here would leave the Player unable to take their Turn at all.
            logger.LogWarning(
                "Player Id {PlayerId} in Game Id {GameId} has no research state row, so no research points were generated in Round {RoundNumber}.",
                playerId,
                context.GameId,
                context.CurrentRoundNumber);

            return string.Empty;
        }

        // Strictly less-than, so the grant happens exactly once per Round however many times this runs.
        if (researchState.LastResearchGeneratedOnRound >= context.CurrentRoundNumber)
        {
            return string.Empty;
        }

        // Ownership, not founding: a captured settlement feeds its captor's research, not the research of whoever built it. Razing is stamped on the
        // Structure and the Settlement row survives it, so the removal filter is mandatory rather than defensive.
        //
        // A settlement still under construction is counted. Its settlers left the map at founding and are already its population, so excluding it would
        // make those people vanish from every total for the several Turns it takes to build.
        int settlementPopulation = await db.Settlements
            .Where(settlement => settlement.GameId == context.GameId)
            .Where(settlement => settlement.StructureInstance.OwnedByPlayerId == playerId)
            .Where(settlement => settlement.StructureInstance.RemovedOnTurnId == null)
            .SumAsync(settlement => settlement.Population);

        // Healthy and injured alike, matching how a Settlement's population is seeded from the Force that founded it: a Force's sickness is its own
        // mechanic and is not modelled by splitting the head count.
        //
        // Scalars only off the compiled snapshot. Materialising CompiledUnitTemplate would pull in its owned Attack and Resistance damage profiles, which
        // the in-memory provider cannot read back - the capability collection is filtered on, never shaped into the result.
        int campingForcePopulation = await db.UnitInstances
            .Where(force => force.GameId == context.GameId)
            .Where(force => force.PlayerId == playerId)
            .Where(force => force.UnitTemplate.Compiled!.Capabilities
                .Any(capability => capability.TagCode == CapabilityTagCodes.CREATE_SETTLEMENT
                                || capability.TagCode == CapabilityTagCodes.CREATE_EARLY_CAMP))
            .SumAsync(force => force.HealthyCount + force.InjuredCount);

        // Integer division is the Floor the design asks for: both operands are non-negative, and the divisor is constrained positive by
        // CK_PlayerResearchState_TechPointsPerPopulationSizePositive.
        //
        // Assignment rather than addition. The end-of-turn step zeroes this field, so a plain assignment is self-correcting if a Turn ever ends without it
        // having run, where accumulating would quietly carry a stale figure into the next Round.
        int generatedPoints = (settlementPopulation + campingForcePopulation) / researchState.TechPointsPerPopulationSize;

        researchState.UnspentDirectedPoints = generatedPoints;
        researchState.LastResearchGeneratedOnRound = context.CurrentRoundNumber;

        await db.SaveChangesAsync();

        logger.LogInformation(
            "Generated {GeneratedPoints} directed research point(s) for Player Id {PlayerId} in Game Id {GameId}, Round {RoundNumber}, "
            + "from {SettlementPopulation} settlement population and {CampingForcePopulation} camping Force population at {PopulationPerPoint} per point.",
            generatedPoints,
            playerId,
            context.GameId,
            context.CurrentRoundNumber,
            settlementPopulation,
            campingForcePopulation,
            researchState.TechPointsPerPopulationSize);

        return string.Empty;
    }
}
