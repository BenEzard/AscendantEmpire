using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Units.Classes;
using AscendantEmpire.Application.Helpers.Randomness;
using AscendantEmpire.Domain.Entities.Units;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AscendantEmpire.Application.Features.Units.Services;

/// <summary>
/// Kills and wounds the troops of every Force that went short of food, in proportion to how far short it went.
/// </summary>
/// <remarks>
/// <para>
/// The arithmetic is all in <see cref="StarvationCasualtyCalculator"/>; what happens here is reading the Forces, giving each one its own random stream, and
/// writing the results back. A Force left with nobody at all is destroyed through <see cref="IForceLifecycleWriter"/>, so its disappearance is recorded like
/// any other.
/// </para>
/// <para>
/// <b>Stages, never saves.</b> Gathering calls this before its own <c>SaveChangesAsync</c>, so the casualties commit in the same statement batch as the
/// depletion that caused them - a Turn can never take the food without the losses, or the losses without the food.
/// </para>
/// <para>
/// <b>Nothing tells the Player.</b> Troop counts simply drop until CM-50 persists what attrition did and raises it in the notifications panel.
/// </para>
/// </remarks>
internal class StarvationUnderfedForceResolver(
    IForceLifecycleWriter forceLifecycleWriter,
    ILogger<StarvationUnderfedForceResolver> logger) : IUnderfedForceResolver
{
    public async Task ResolveAsync(
        AscendantEmpireContext context,
        int gameId,
        int playerId,
        int roundNumber,
        int? playerTurnId,
        IReadOnlyCollection<ForceFeedingResult> results,
        CancellationToken cancelToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(results);

        // TODO CM-51: a tile holding 15 or fewer troops in total takes injuries but never deaths. The gate is per tile rather than per Force, so that
        //  splitting an army into tiny bands cannot buy the exemption; every result here carries its CampaignMapTileId to group by.

        // Ordered by Force id purely so a log read back later follows a stable order. No Force's outcome depends on it: each draws from its own stream.
        List<ForceFeedingResult> underfed = [.. results.Where(result => result.IsUnderfed).OrderBy(result => result.UnitInstanceId)];

        if (underfed.Count == 0)
        {
            return;
        }

        // Projected as nullable so a missing Game is distinguishable from a Game seeded with zero, which is legitimate and which the test fixtures use.
        long? gameSeed = await context.GameIndex
            .Where(game => game.Id == gameId)
            .Select(game => (long?)game.GenerationSeed)
            .SingleOrDefaultAsync(cancelToken);

        if (gameSeed is not long seed)
        {
            // Rolling against an invented seed would make the casualties unreproducible, which is the one property the whole derivation exists to give.
            logger.LogError(
                "Game Id {GameId} was not found, so the {ForceCount} Force(s) belonging to Player Id {PlayerId} that went short of food took no casualties.",
                gameId,
                underfed.Count,
                playerId);

            return;
        }

        int[] underfedForceIds = [.. underfed.Select(result => result.UnitInstanceId)];

        // Tracked, not projected: these rows are about to be written, and a Force that dies has to reach the lifecycle writer as a tracked entity.
        Dictionary<int, UnitInstance> forcesById = await context.UnitInstances
            .Where(force => underfedForceIds.Contains(force.Id))
            .ToDictionaryAsync(force => force.Id, cancelToken);

        int totalKilled = 0;
        int totalWounded = 0;
        int forcesDestroyed = 0;

        foreach (ForceFeedingResult result in underfed)
        {
            if (forcesById.TryGetValue(result.UnitInstanceId, out UnitInstance? force) == false)
            {
                // Gathering read the Force moments ago on its own context, so this is a Force deleted underneath the Turn rather than anything routine.
                logger.LogWarning(
                    "Unit Instance Id {UnitInstanceId} went short of food in Game Id {GameId} but was no longer there to take the casualties.",
                    result.UnitInstanceId,
                    gameId);

                continue;
            }

            int troopsBefore = force.HealthyCount + force.InjuredCount;

            // The Force's own stream for this Round. Derived rather than drawn from a shared sequence, so a re-run of the pipeline repeats these casualties
            // exactly, and the order the Forces happened to be processed in decides nothing.
            Random random = GameRandom.Create(seed, RandomPurpose.StarvationCasualties, roundNumber, playerId, force.Id);

            StarvationCasualties casualties = StarvationCasualtyCalculator.Calculate(troopsBefore, result.Need, result.Shortfall, random);

            if (casualties.Total == 0)
            {
                continue;
            }

            (int healthyCount, int injuredCount) = StarvationCasualtyCalculator.Apply(force.HealthyCount, force.InjuredCount, casualties);

            // Counted off the troop totals rather than taken from the roll: a wound with nobody healthy left to take it becomes a death, so the two differ.
            int killed = troopsBefore - (healthyCount + injuredCount);
            int wounded = casualties.Total - killed;

            force.HealthyCount = healthyCount;
            force.InjuredCount = injuredCount;

            totalKilled += killed;
            totalWounded += wounded;

            logger.LogInformation(
                "Unit Instance Id {UnitInstanceId} went short of food on tile {CampaignMapTileId} in Game Id {GameId}, Round {RoundNumber}: needed {Need}, "
                    + "fed {Fed}, short by {Shortfall}. Of {TroopsBefore} troop(s), {Killed} died and {Wounded} were wounded.",
                force.Id,
                result.CampaignMapTileId,
                gameId,
                roundNumber,
                result.Need.ToDecimal(),
                result.Fed.ToDecimal(),
                result.Shortfall.ToDecimal(),
                troopsBefore,
                killed,
                wounded);

            if (healthyCount + injuredCount > 0)
            {
                continue;
            }

            if (await DestroyAsync(context, force, playerTurnId, cancelToken))
            {
                forcesDestroyed++;
            }
        }

        logger.LogInformation(
            "Starvation cost Player Id {PlayerId} in Game Id {GameId}, Round {RoundNumber}, {Killed} troop(s) dead and {Wounded} wounded across "
                + "{ForceCount} underfed Force(s); {DestroyedCount} Force(s) were wiped out.",
            playerId,
            gameId,
            roundNumber,
            totalKilled,
            totalWounded,
            underfed.Count,
            forcesDestroyed);
    }

    /// <summary>
    /// Stages the removal of a Force starvation left with nobody, together with the orders that would otherwise hold it in the database.
    /// </summary>
    /// <remarks>
    /// The destruction is staged first and the orders cleared only once it has been accepted, so a refusal cannot leave a surviving Force stripped of its
    /// orders. Neither step saves; both go into gathering's save along with everything else.
    /// </remarks>
    /// <returns><see langword="true"/> when the Force was staged for removal.</returns>
    private async Task<bool> DestroyAsync(AscendantEmpireContext context, UnitInstance force, int? playerTurnId, CancellationToken cancelToken)
    {
        if (playerTurnId is not int turnId)
        {
            // Leaving the Force standing at nought troops is the safe failure: gathering skips Forces with no troops, so it will not feed or starve again,
            // and nothing has been deleted that cannot be put right. Destroying it with no Turn to stamp the row with is the thing that must not happen.
            logger.LogError(
                "Unit Instance Id {UnitInstanceId} was wiped out by starvation but the end-of-Turn context carried no Player Turn Id, so it could not be "
                    + "destroyed and has been left with no troops.",
                force.Id);

            return false;
        }

        ServiceResultBase destroyResult = await forceLifecycleWriter.DestroyAsync(context, force, turnId, cancelToken);

        if (destroyResult.HasSucceeded == false)
        {
            logger.LogError(
                "Unit Instance Id {UnitInstanceId} was wiped out by starvation but could not be destroyed, so it has been left with no troops. "
                    + "Errors: {Errors}",
                force.Id,
                string.Join("; ", destroyResult.Errors));

            return false;
        }

        await ClearOrdersAsync(context, force.Id, cancelToken);

        return true;
    }

    /// <summary>
    /// Stages the removal of everything holding a real foreign key to the Force, which the database would otherwise refuse the delete over.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Two tables point at <c>UnitInstances</c> and both are <c>NoAction</c>: the orders issued to the Force, and the participant rows naming it - which
    /// include rows on <b>another</b> Force's order, where this one was a secondary party to a split or a combine. The movement steps of the Force's own
    /// orders go too, for the same reason one rung further down.
    /// </para>
    /// <para>
    /// TODO: an order belonging to another Force loses a participant here and is left describing a party that no longer exists. Cancelling or repairing such
    /// an order is the right answer and needs the order types' own rules, which are not written yet.
    /// </para>
    /// <para>
    /// TODO: the Force may have been the last member of its Command, leaving the Command with nobody. Disbanding an emptied Command waits on the officer
    /// economy being modelled.
    /// </para>
    /// </remarks>
    private static async Task ClearOrdersAsync(AscendantEmpireContext context, int unitInstanceId, CancellationToken cancelToken)
    {
        List<ForceOrder> orders = await context.ForceOrders
            .Where(order => order.UnitInstanceId == unitInstanceId)
            .ToListAsync(cancelToken);

        int[] orderIds = [.. orders.Select(order => order.Id)];

        List<ForceOrderMovementStep> steps = await context.ForceOrderMovementSteps
            .Where(step => orderIds.Contains(step.ForceOrderId))
            .ToListAsync(cancelToken);

        List<ForceOrderParticipant> participants = await context.ForceOrderParticipants
            .Where(participant => orderIds.Contains(participant.ForceOrderId) || participant.UnitInstanceId == unitInstanceId)
            .ToListAsync(cancelToken);

        context.ForceOrderMovementSteps.RemoveRange(steps);
        context.ForceOrderParticipants.RemoveRange(participants);
        context.ForceOrders.RemoveRange(orders);
    }
}
