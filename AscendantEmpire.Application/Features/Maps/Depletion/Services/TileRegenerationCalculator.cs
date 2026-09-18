using AscendantEmpire.Application.Helpers.ScaledNumerics;

namespace AscendantEmpire.Application.Features.Maps.Depletion.Services;

/// <summary>
/// Works out a tile's new <c>Taken</c> amount for one sustenance resource at the end of a Round, following "Replenishment" in <c>docs/design/campaign-map.md</c>.
/// </summary>
/// <remarks>
/// <para>
/// Two rules, applied in order:
/// <list type="number">
///   <item><description>Regrow, every Round, whether or not the tile was fed on: <c>Taken = MAX(0, Taken − Regeneration Percent × Base)</c>. A fixed share of <c>Base</c>
///   comes back each Round, capped at <c>Base</c>.</description></item>
///   <item><description>Nobody fed on the tile this Round: <c>Taken</c> is at most <c>0.9 × Base</c>. A tile left alone always recovers to at least 10% of <c>Base</c>, and
///   keeps whichever of the two is more generous.</description></item>
/// </list>
/// </para>
/// <para>
/// Regrowth pays off anything a stronger Player took above <c>Base</c> first, so a weaker Player sees nothing on such a tile until <c>Taken</c> drops back below
/// <c>Base</c>. Rule 2 forgives that surplus, but only on a tile nobody fed on: applied while a Force is still there, it would hand a stronger Player their surplus back every
/// Round on a tile they are camping on.
/// </para>
/// <para>
/// Rounding always goes against the tile, in thousandths: the regrowth is rounded down and <c>0.9 × Base</c> is rounded up, so a tile never gets back a thousandth more than the
/// rules allow.
/// </para>
/// <para>
/// Pure and database-free, so the rules are tested on their own; the end-of-Round process does the reading and writing and calls this per tile and resource.
/// </para>
/// </remarks>
internal static class TileRegenerationCalculator
{
    /// <summary>
    /// The share of <c>Base</c> a tile left alone recovers to at least: 10%, expressed as the divisor that takes it off <c>Base</c>.
    /// </summary>
    private const long _REFILL_DIVISOR = 10;

    /// <summary>
    /// The new amount taken of one resource from a tile, after the end-of-Round regeneration.
    /// </summary>
    /// <param name="taken">How much of the resource has been taken from the tile, by anyone. Never negative.</param>
    /// <param name="baseValue">The tile's base value for the resource. Never negative. A resource with no base is not regenerated, so the caller skips it.</param>
    /// <param name="regenerationPercent">The share of <paramref name="baseValue"/> that regrows each Round, as a fraction (0.25 is 25%). Between 0 and 1.</param>
    /// <param name="fedOn">Whether any Force fed on the tile this Round.</param>
    /// <returns>The new amount taken. Never negative.</returns>
    public static ScaledAmount Regenerate(ScaledAmount taken, ScaledAmount baseValue, decimal regenerationPercent, bool fedOn)
    {
        if (taken < ScaledAmount.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(taken), "An amount taken cannot be negative.");
        }

        if (baseValue < ScaledAmount.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(baseValue), "A base value cannot be negative.");
        }

        if (regenerationPercent < 0m || regenerationPercent > 1m)
        {
            throw new ArgumentOutOfRangeException(nameof(regenerationPercent), regenerationPercent, "A Regeneration Percent must be between 0 and 1.");
        }

        // Rule 1: regrow a fixed share of Base, rounded down so the tile never gets back more than the percentage allows. Taken above Base (from a stronger Player) is paid
        // off first, like any other Taken.
        long regrowthRaw = (long)Math.Floor(baseValue.RawValue * regenerationPercent);

        ScaledAmount regrownTaken = taken - ScaledAmount.FromRaw(regrowthRaw);

        // Regrowth is capped at Base, which is Taken never going below 0.
        if (regrownTaken < ScaledAmount.Zero)
        {
            regrownTaken = ScaledAmount.Zero;
        }

        // A tile someone fed on this Round only regrows: Rule 2 would hand a stronger Player camping on it their surplus back.
        if (fedOn)
        {
            return regrownTaken;
        }

        // Rule 2: 0.9 × Base, rounded up - Base less 10% of it rounded down. Rounding up leaves more taken, so the refill never exceeds 10%.
        ScaledAmount refillTaken = baseValue - baseValue.Divide(_REFILL_DIVISOR);

        // A tile left alone keeps whichever leaves less taken: the regrowth, or the 10% floor.
        return regrownTaken < refillTaken ? regrownTaken : refillTaken;
    }
}
