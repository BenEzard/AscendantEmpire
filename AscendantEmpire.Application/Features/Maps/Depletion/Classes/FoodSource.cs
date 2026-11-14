using AscendantEmpire.Application.Helpers.ScaledNumerics;

namespace AscendantEmpire.Application.Features.Maps.Depletion.Classes;

/// <summary>
/// One thing on a tile that a Force can eat: what it is, how much of its Asset is there, and how much of each Asset must be consumed to meet one unit of need.
/// </summary>
/// <remarks>
/// <para>
/// Kept apart from <see cref="FoodSourceKey"/> because the amounts taken are keyed by the source they came from, and a value holding an amount cannot serve
/// as that key - two readings of one source would not compare equal.
/// </para>
/// <param name="Available">How much of the Asset the source holds, in Asset units.</param>
/// <param name="AmountPerNeedUnit">
/// What one unit of the need costs in this Asset, raw fixed-point from the required <c>SupplySet</c>'s member row. Something which satisfies less requires more (e.g. 2,000 twice as much)
/// </param>
/// </remarks>
internal readonly record struct FoodSource(FoodSourceKey Key, ScaledAmount Available, long AmountPerNeedUnit)
{
    /// <summary>
    /// What the source is worth in need units - individuals fed for one Turn - which is the common measure the need is split by.
    /// </summary>
    /// <remarks>
    /// The divisor is the set member's price, which <c>SupplySetValidation</c> guarantees above zero, so this cannot divide by nothing. Int128 because an
    /// Asset amount already in thousandths is scaled by a thousand again before the division.
    /// </remarks>
    public ScaledAmount ContributionInNeedUnits => ScaledAmount.FromRaw((long)((Int128)Available.RawValue * ScaledAmount.Scale / AmountPerNeedUnit));
}
