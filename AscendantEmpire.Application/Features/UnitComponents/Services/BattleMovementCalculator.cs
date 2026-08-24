namespace AscendantEmpire.Application.Features.UnitComponents.Services;

/// <summary>
/// Derives a unit template's battle-map movement points from the power of its body and the weight of everything that body carries.
/// </summary>
/// <remarks>
/// <para>The formula is <c>MAX(0, Body.Power - SUM(Weight of fitted components))</c>. A heavier loadout buys fewer tiles per turn, which is the
/// design lever the unit-design system leans on: kit a soldier out and they slow down.</para>
/// <para>The body's <em>own</em> weight is deliberately excluded. Power is what the body can move with once it is already carrying itself, so
/// subtracting the body's weight as well would charge for it twice. Only the components fitted into the body's slots subtract.</para>
/// <para>Every value here is raw fixed-point using the Scale 1,000 convention of <c>ScaledAmount</c> (1,000 raw = 1.0), matching how power and
/// weight are stored on the entities. The arithmetic is a plain subtraction, so it stays exact at that scale with no conversion needed — a
/// half-tile result such as 6.5 is simply 6,500 raw.</para>
/// <para>Pure and static, like <see cref="SlotAssemblyValidator"/> and <see cref="SlotRuleEvaluator"/>, so template compilation and any future
/// caller share one definition of the formula, and it can be unit-tested without a database.</para>
/// </remarks>
public static class BattleMovementCalculator
{
    /// <summary>
    /// The lowest movement a template can derive to. An over-loaded design is pinned to a standstill rather than going negative.
    /// </summary>
    public const long MINIMUM_BATTLEFIELD_MOVEMENT_POINTS_RAW = 0;

    /// <summary>
    /// Calculates the battle-map movement points for a body of the given power carrying the given weight of fitted components.
    /// </summary>
    /// <param name="bodyPowerRaw">The body's power, raw Scale-1,000 (7,000 = 7.0).</param>
    /// <param name="carriedWeightRaw">
    /// The summed weight of the components fitted into the body, raw Scale-1,000. Excludes the body's own weight — see the remarks on
    /// <see cref="BattleMovementCalculator"/>.
    /// </param>
    /// <returns>The derived movement points, raw Scale-1,000, never below <see cref="MINIMUM_BATTLEFIELD_MOVEMENT_POINTS_RAW"/>.</returns>
    public static long CalculateBattlefieldMovementPoints(long bodyPowerRaw, long carriedWeightRaw)
    {
        long derivedMovementPointsRaw = bodyPowerRaw - carriedWeightRaw;

        // An over-loaded design (or a body with no power authored at all) would otherwise go negative, which
        // no consumer of the snapshot expects.
        if (derivedMovementPointsRaw < MINIMUM_BATTLEFIELD_MOVEMENT_POINTS_RAW)
        {
            return MINIMUM_BATTLEFIELD_MOVEMENT_POINTS_RAW;
        }

        return derivedMovementPointsRaw;
    }
}
