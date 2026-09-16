using AscendantEmpire.Application.Helpers.ScaledNumerics;
using AscendantEmpire.Domain.Enums.Resources;

namespace AscendantEmpire.Application.Features.Maps.Depletion.Services;

/// <summary>
/// Classifies how well a tile's food can support a food need, for the support indicator.
/// </summary>
/// <remarks>
/// <para>
/// Evaluated in order:
/// <list type="number">
///   <item><description>Nothing available → <see cref="TileSupportLevel.Exhausted"/>, whatever the need. A stripped tile shows as stripped even with no Forces on it.</description></item>
///   <item><description>No need → <see langword="null"/>: there is nobody to feed, so there is nothing to say.</description></item>
///   <item><description>Available ≥ 2 × need → <see cref="TileSupportLevel.Plenty"/>.</description></item>
///   <item><description>Available ≥ need → <see cref="TileSupportLevel.OneTurn"/>.</description></item>
///   <item><description>Otherwise → <see cref="TileSupportLevel.Insufficient"/>.</description></item>
/// </list>
/// </para>
/// <para>
/// The number of Turns is plain <c>available ÷ need</c>. It deliberately ignores regrowth at the end of a Round (CM-40) and other Players feeding on the same tile, so a
/// tile shown as one Turn may in practice last longer, or shorter.
/// </para>
/// <para>
/// Pure and database-free.
/// </para>
/// </remarks>
internal static class ForceSupportCalculator
{
    /// <summary>
    /// How many Turns of need a tile must cover to count as <see cref="TileSupportLevel.Plenty"/>.
    /// </summary>
    private const long _PLENTY_TURNS = 2;

    /// <summary>
    /// Classifies <paramref name="totalAvailable"/> food against <paramref name="need"/>.
    /// </summary>
    /// <param name="need">The food the Forces need for one Turn. Must not be negative.</param>
    /// <param name="totalAvailable">The total available across the tile's pool. Must not be negative.</param>
    public static TileSupportLevel? Classify(ScaledAmount need, ScaledAmount totalAvailable)
    {
        if (need < ScaledAmount.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(need), "A food need cannot be negative.");
        }

        if (totalAvailable < ScaledAmount.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(totalAvailable), "The food available cannot be negative.");
        }

        // First, so a bare tile reads as bare whether or not anyone is standing on it.
        if (totalAvailable == ScaledAmount.Zero)
        {
            return TileSupportLevel.Exhausted;
        }

        if (need == ScaledAmount.Zero)
        {
            return null;
        }

        // TODO: Regrowth at the end of a Round and other Players feeding on the tile are not taken into account; this is plain available ÷ need.
        if (totalAvailable >= need * _PLENTY_TURNS)
        {
            return TileSupportLevel.Plenty;
        }

        if (totalAvailable >= need)
        {
            return TileSupportLevel.OneTurn;
        }

        return TileSupportLevel.Insufficient;
    }
}
