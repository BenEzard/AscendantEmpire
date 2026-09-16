using AscendantEmpire.Application.Helpers.ScaledNumerics;

namespace AscendantEmpire.Application.Features.Maps.Depletion.Services;

/// <summary>
/// Works out a tile's new <c>Taken</c> amount for one sustenance resource at the end of a Round, following "Replenishment" in <c>docs/design/campaign-map.md</c>.
/// </summary>
/// <remarks>
/// <para>
/// The rules are evaluated in order:
/// <list type="number">
///   <item><description>Fed on this Round and <c>Taken ≥ Base</c>: no change. A bare tile that is being fed on stays bare, for every Player.</description></item>
///   <item><description>Not fed on and <c>Taken &gt; 0.9 × Base</c> (below 10%, bare included): <c>Taken = 0.9 × Base</c>, with no regrowth this Round.</description></item>
///   <item><description>Otherwise regrow: <c>Taken = MAX(0, Taken − (Base − Taken) × (Rate − 1))</c>, the base view multiplied by <c>Rate</c> and capped at <c>Base</c>.</description></item>
/// </list>
/// </para>
/// <para>
/// Rule 1 deliberately does not convert back through <c>Base</c> (<c>Taken = Base − new base view</c>). That would forget anything a stronger Player took above <c>Base</c>, and
/// hand their surplus back every Round on a tile that is meant to be bare.
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
    /// <param name="regenerationRate">The tile's Regeneration Rate. At least 1: a rate below 1 would take food away rather than regrow it.</param>
    /// <param name="fedOn">Whether any Force fed on the tile this Round.</param>
    /// <returns>The new amount taken. Never negative.</returns>
    public static ScaledAmount Regenerate(ScaledAmount taken, ScaledAmount baseValue, decimal regenerationRate, bool fedOn)
    {
        if (taken < ScaledAmount.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(taken), "An amount taken cannot be negative.");
        }

        if (baseValue < ScaledAmount.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(baseValue), "A base value cannot be negative.");
        }

        if (regenerationRate < 1m)
        {
            throw new ArgumentOutOfRangeException(nameof(regenerationRate), regenerationRate, "A Regeneration Rate cannot be below 1.");
        }

        // Rule 1: a bare tile (at base values) that is being fed on stays exactly as it is. Taken may be above Base here, and must stay there.
        if (fedOn && taken >= baseValue)
        {
            return taken;
        }

        // 0.9 × Base, rounded up: Base less 10% of it rounded down. Rounding up leaves more taken, so the refill never exceeds 10%.
        ScaledAmount refillTaken = baseValue - baseValue.Divide(_REFILL_DIVISOR);

        // Rule 2: a tile below 10% (bare, or stripped past Base by a stronger Player, included) that nobody fed on jumps to 10%, and does not also regrow this Round.
        if (fedOn == false && taken > refillTaken)
        {
            return refillTaken;
        }

        // Rule 3: regrow. The base view is not negative here - rule 1 or rule 2 has already handled every Taken above Base - so rounding down is truncation.
        long baseViewRaw = (baseValue - taken).RawValue;
        long regrowthRaw = (long)Math.Floor(baseViewRaw * (regenerationRate - 1m));

        ScaledAmount regrownTaken = taken - ScaledAmount.FromRaw(regrowthRaw);

        // Regrowth is capped at Base, which is Taken never going below 0.
        return regrownTaken < ScaledAmount.Zero ? ScaledAmount.Zero : regrownTaken;
    }
}
