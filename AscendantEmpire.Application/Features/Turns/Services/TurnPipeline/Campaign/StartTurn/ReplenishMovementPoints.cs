using AscendantEmpire.Application.Data;
using AscendantEmpire.Domain.Entities.Units;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AscendantEmpire.Application.Features.Turns.Services.TurnPipeline.Campaign.StartTurn;

/// <summary>
/// Hands every one of the Player's Forces its campaign movement allowance for the Turn that is opening.
/// </summary>
/// <remarks>
/// <para>
/// The arithmetic itself lives in <see cref="MovementReplenishment"/>, so the turn-by-turn table can be pinned without a database. 
/// This class is the orchestration around it: which Forces are entitled to a grant, which template the allowance comes from, and once per Round.
/// </para>
/// <para>
/// Loads and saves through the change tracker rather than <c>ExecuteUpdateAsync</c>. The rule is a three-way branch against an allowance that lives on a different table, 
/// so pushing it into an expression tree would mean a second copy of the rule in SQL — one the unit tests could not reach — for a query that touches tens of rows once a Turn.
/// </para>
/// <para>
/// <b>Concurrency.</b> <see cref="UnitInstance"/> carries no row version, so two overlapping start-of-Turn runs for one Player would both read, both compute and both save. 
/// The Round stamp only closes that window once the first has committed. Campaign Turns are handed over one at a time, so this is not reachable today, but it is the reason a row version is on the roadmap.
/// </para>
/// </remarks>
internal class ReplenishMovementPoints(
    IDbContextFactory<AscendantEmpireContext> dbFactory,
    ILogger<ReplenishMovementPoints> logger) : IStartCampaignTurnProcess
{
    public string ItemName => "Replenish Movement Points";

    public int ExecutionOrder => (int)StartCampaignTurnProcessOrder.ReplenishMovementPoints;

    public async Task<string> ProcessAsync(TurnPipelineContextData context)
    {
        // TODO: Supply must be resolved before movement is granted, and this process must then read the verdict CheckUnitSupply reaches.
        //       The ordering already enforces it — CheckUnitSupply is 20,000 and this process is 30,000 — precisely so the supply verdict exists by
        //       the time the allowance is handed out. CheckUnitSupply is still a stub, so there is nothing to read yet.
        //
        //       When it does write a supply state, the rule to implement here is: a Unit whose body component REQUIRES FUEL, and for which NO FUEL IS
        //       AVAILABLE, receives NO CAMPAIGN MOVEMENT AT ALL that Turn. Its remaining movement is set to zero — not to the allowance, not to the
        //       carry, and not left where it was — because an unfuelled Unit cannot move whatever the terrain or its bonuses.
        //
        //       Battle fuel runs on its own clock and is not this process's job, but the two rules have to agree, so they are recorded here:
        //         * a Unit that ENTERS A BATTLE UNSUPPLIED carries two Battle Rounds' worth of fuel, and cannot move for the rest of that Battle;
        //         * a Unit that ENTERS A BATTLE SUPPLIED keeps that supply until the end of that Player's NEXT campaign Turn.
        //
        // TODO: BattleMovementRemaining is granted when a Unit is fielded and never replenished. The four battle pipeline marker interfaces have no
        //       implementations and no ordering enum, so there is nowhere for a battle equivalent of this process to live yet.

        if (context.PlayerId is null)
        {
            // The start of a Turn is always Player-scoped — CampaignRoundService builds this context with the four-argument constructor. A null here
            // is a defect in the caller, and replenishing every Player in the Game would be a great deal worse than refusing.
            string error = $"{ItemName} was given a start-of-Turn context with no Player Id for Game Id {context.GameId}.";
            logger.LogError("{Error}", error);

            return error;
        }

        int playerId = context.PlayerId.Value;

        await using AscendantEmpireContext db = await dbFactory.CreateDbContextAsync();

        // Tracked, because these rows are about to be written.
        //
        // Forces standing on a Battle Map are deliberately included. Campaign movement is spent on the campaign map and is now a separate pool from
        // BattleMovementRemaining, so a Force does not emerge from a long Battle frozen in place through no fault of its owner.
        //
        // Wiped-out Forces are excluded, matching CampaignRemainingMovementCheck and BattleRemainingMovementCheck, which already refuse to report them.
        //
        // The Round stamp comparison is strictly less-than, so the grant happens exactly once per Round however many times this runs.
        List<UnitInstance> forces = await db.UnitInstances
            .Where(u => u.GameId == context.GameId)
            .Where(u => u.PlayerId == playerId)
            .Where(u => u.HealthyCount + u.InjuredCount > 0)
            .Where(u => u.CampaignMovementReplenishedOnRound < context.CurrentRoundNumber)
            .ToListAsync();

        if (forces.Count == 0)
        {
            return string.Empty;
        }

        int[] templateIds = [.. forces.Select(f => f.UnitTemplateId).Distinct()];

        // Scalars only, and deliberately not an Include. Materialising CompiledUnitTemplate would pull in its owned Attack and Resistance damage profiles,
        // which the in-memory provider cannot read back — that alone would push every test of this process onto a SQL Server container to fetch a single long.
        // StepForceHandler projects the same way, for the same reason.
        Dictionary<int, long> allowanceByTemplateId = await db.CompiledUnitTemplates
            .Where(c => templateIds.Contains(c.UnitTemplateId))
            .Select(c => new { c.UnitTemplateId, c.CampaignMovementPoints })
            .ToDictionaryAsync(c => c.UnitTemplateId, c => c.CampaignMovementPoints);

        int replenishedCount = 0;

        foreach (UnitInstance force in forces)
        {
            if (allowanceByTemplateId.TryGetValue(force.UnitTemplateId, out long allowanceRaw) == false)
            {
                // A template that was never compiled, or whose snapshot has gone since the Unit was fielded. Skipped rather than failed: the pipeline
                // is fail-fast, so returning an error here would leave the Player unable to take their Turn at all. Left unstamped so the grant is
                // retried next Round, by which time a recompile may well have fixed it.
                logger.LogWarning(
                    "Unit Instance Id {UnitInstanceId} has no compiled snapshot for Unit Template Id {UnitTemplateId}, so it was not replenished in Round {RoundNumber}.",
                    force.Id,
                    force.UnitTemplateId,
                    context.CurrentRoundNumber);

                continue;
            }

            force.CampaignMovementRemaining = MovementReplenishment.ReplenishCampaignMovement(allowanceRaw, force.CampaignMovementRemaining);
            force.CampaignMovementReplenishedOnRound = context.CurrentRoundNumber;

            replenishedCount++;
        }

        await db.SaveChangesAsync();

        logger.LogInformation(
            "Replenished campaign movement for {ReplenishedCount} of {ForceCount} Force(s) belonging to Player Id {PlayerId} in Game Id {GameId}, Round {RoundNumber}.",
            replenishedCount,
            forces.Count,
            playerId,
            context.GameId,
            context.CurrentRoundNumber);

        return string.Empty;
    }
}
