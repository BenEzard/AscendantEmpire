namespace AscendantEmpire.Application.Features.Structures.Services;

/// <summary>
/// Derives how much construction work a Force does per Turn, from the power of its body, how many of it are fit to work, and how much of its Turn it has left.
/// </summary>
/// <remarks>
/// <para>
/// The formula is <c>Power × HealthyCount × MIN(1, CampaignMovementRemaining / CampaignMovementPoints)</c>. Each term is a lever the design leans on: a stronger body builds
/// faster, more hands build faster, and a Force that has spent its Turn marching has less of it left to build with.
/// </para>
/// <para>
/// <b>Healthy only.</b> The injured are carried, not put to work - unlike the settlement's own population, which counts every soldier because injury is a Force concept that
/// a settlement does not model. See <c>docs/design/settlements.md</c>.
/// </para>
/// <para>
/// <b>The movement fraction is capped at 1.</b> Unspent movement is carried into the next Turn on top of the allowance, so a Force can open its Turn holding more than its
/// full allowance; without the cap it would build faster than a fresh Force, for no reason anybody designed. A Force in movement debt contributes nothing, which is the same
/// floor the settle rules already apply - founding needs movement left.
/// </para>
/// <para>
/// <b>Rounded down, then floored at one point.</b> A rate of zero is a Structure that can never finish, and nothing should be able to found one. The floor means a founding
/// always makes progress, however slowly, rather than producing a permanent hole in the map.
/// </para>
/// <para>
/// Power and movement are raw fixed-point on the Scale 1,000 convention of <c>ScaledAmount</c> (1,000 raw = 1.0), and so is the rate this returns: a Force of 200 humans
/// (Power 7.0) on a full Turn builds 1,400.0 points, which is 1,400,000 raw. <c>Structure.BuildPointsRequired</c> is on the same scale, so the two divide directly.
/// </para>
/// <para>
/// Pure and static, like <c>BattleMovementCalculator</c>, so the settle handler and any future build order share one definition of the formula and it can be tested with no
/// database at all.
/// </para>
/// </remarks>
public static class ConstructionRateCalculator
{
    /// <summary>
    /// The least work a Force can be credited with per Turn. One raw point: enough that construction always finishes eventually.
    /// </summary>
    public const long MINIMUM_BUILD_POINTS_PER_TURN_RAW = 1;

    /// <summary>
    /// Calculates the construction work a Force does per Turn.
    /// </summary>
    /// <param name="bodyPowerRaw">The Force's Power, raw Scale-1,000, as its compiled template carries it from its body (7,000 = 7.0).</param>
    /// <param name="healthyCount">How many of the Force are fit to work. The injured are excluded.</param>
    /// <param name="campaignMovementRemainingRaw">
    /// The movement the Force has left this Turn, raw Scale-1,000. May exceed the allowance, because unspent movement carries over, and may be negative, because a Force
    /// holding any movement at all may enter a tile and pays its whole cost.
    /// </param>
    /// <param name="campaignMovementPointsRaw">The Force's full per-Turn allowance, raw Scale-1,000, from the same compiled template.</param>
    /// <returns>The work done per Turn, raw Scale-1,000, never below <see cref="MINIMUM_BUILD_POINTS_PER_TURN_RAW"/>.</returns>
    public static long CalculateBuildPointsPerTurn(
        long bodyPowerRaw,
        int healthyCount,
        long campaignMovementRemainingRaw,
        long campaignMovementPointsRaw)
    {
        if (bodyPowerRaw <= 0 || healthyCount <= 0)
        {
            return MINIMUM_BUILD_POINTS_PER_TURN_RAW;
        }

        // A template with no allowance authored cannot have the fraction worked out at all. Credited with a full Turn rather than none: the Force is plainly standing there
        // able to work, and a divide by zero or a silent nothing would both be worse answers than "all of it".
        if (campaignMovementPointsRaw <= 0)
        {
            return ApplyFloor(bodyPowerRaw * healthyCount);
        }

        // The cap and the floor in one: more than a full Turn's movement is worth a full Turn, and movement debt is worth nothing.
        long effectiveRemainingRaw = Math.Clamp(campaignMovementRemainingRaw, 0, campaignMovementPointsRaw);

        // Multiplied before dividing, so the fraction keeps its precision: dividing first would round a part-spent Turn to 0 or 1 and lose the difference between them.
        //
        // The widest this gets is Power × healthyCount × allowance. At Scale 1,000 that is roughly 7e3 × 1e6 × 1e3 for a Force larger than any the game can field, which is
        // four orders of magnitude inside long.
        long workRaw = bodyPowerRaw * healthyCount * effectiveRemainingRaw / campaignMovementPointsRaw;

        return ApplyFloor(workRaw);
    }

    /// <summary>
    /// How many whole Turns a Structure needs at the given rate, rounding any part-Turn up.
    /// </summary>
    /// <remarks>
    /// Rounded up because a Structure is finished at the <b>end</b> of a Turn and usable the Turn after: a build with a tenth of its work left still costs a whole Turn to
    /// finish. This is what the settle response tells the player, so it has to agree with what the construction process will actually do.
    /// </remarks>
    /// <param name="buildPointsRaw">The Structure kind's total work, raw Scale-1,000.</param>
    /// <param name="buildPointsPerTurnRaw">The rate from <see cref="CalculateBuildPointsPerTurn"/>, raw Scale-1,000.</param>
    public static int CalculateTurnsToComplete(long buildPointsRaw, long buildPointsPerTurnRaw)
    {
        if (buildPointsRaw <= 0)
        {
            // Nothing to build: finished at the end of the Turn it was founded in.
            return 1;
        }

        long rateRaw = Math.Max(buildPointsPerTurnRaw, MINIMUM_BUILD_POINTS_PER_TURN_RAW);

        // Ceiling division, written without floating point so it cannot disagree with the integer arithmetic the process itself does.
        long turns = (buildPointsRaw + rateRaw - 1) / rateRaw;

        return turns > int.MaxValue ? int.MaxValue : (int)turns;
    }

    /// <summary>Keeps a rate from reaching zero, which would be a Structure that never finishes.</summary>
    private static long ApplyFloor(long workRaw)
    {
        return workRaw < MINIMUM_BUILD_POINTS_PER_TURN_RAW ? MINIMUM_BUILD_POINTS_PER_TURN_RAW : workRaw;
    }
}
