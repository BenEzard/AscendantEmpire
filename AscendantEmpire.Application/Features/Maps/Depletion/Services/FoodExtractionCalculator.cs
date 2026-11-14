using AscendantEmpire.Application.Features.Maps.Depletion.Classes;
using AscendantEmpire.Application.Helpers.ScaledNumerics;

namespace AscendantEmpire.Application.Features.Maps.Depletion.Services;

/// <summary>
/// Works out how much of each of a tile's food sources a Force's need takes.
/// </summary>
/// <remarks>
/// <para>
/// <list type="bullet">
///   <item><description><b>Enough food:</b> the need is split across the sources in proportion to how many individuals each can feed, as in the design
///   doc's scenario (400 Forage and 200 Hunt feeding 100 takes 66.667 and 33.333). The split is in thousandths by
///   <see cref="ProportionalSplitter"/>, so the shares sum exactly to the need and no source gives more than it has.</description></item>
///   <item><description><b>Not enough:</b> everything is taken, and the rest of the need is the shortfall.</description></item>
/// </list>
/// </para>
/// <para>
/// The split is by what each source <b>feeds</b> rather than by how much of it there is, because the sources are priced apart: a thousand berries at 1.67
/// feed fewer individuals than a thousand of anything at 1.0, and splitting by raw amount would send the same share to both and leave the berries short.
/// Each share is then multiplied back by that source's own price to give the Asset amount taken.
/// </para>
/// <para>
/// That last multiplication rounds down, so a tile can lose a thousandth less than the Forces ate. The need is what must come out exact - a Force fed 0.999
/// of a ration would take casualties - and the tile's loss is the figure that may absorb the rounding.
/// </para>
/// <para>
/// Pure and database-free. Which sources a tile offers, and what is available of each, are <see cref="TileFoodSources"/>' to decide.
/// </para>
/// </remarks>
internal static class FoodExtractionCalculator
{
    /// <summary>
    /// Feeds <paramref name="need"/> from <paramref name="sources"/>.
    /// </summary>
    /// <param name="need">The food needed, in need units. Must not be negative.</param>
    /// <param name="sources">What the tile offers. None may hold a negative amount.</param>
    public static FoodExtraction Extract(ScaledAmount need, IReadOnlyCollection<FoodSource> sources)
    {
        if (need < ScaledAmount.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(need), "A food need cannot be negative.");
        }

        ScaledAmount totalContribution = sources.Aggregate(ScaledAmount.Zero, (sum, source) => sum + source.ContributionInNeedUnits);

        // The tile cannot cover the need, so the Forces strip it bare and go short by the difference.
        if (totalContribution <= need)
        {
            return new FoodExtraction(
                sources.ToDictionary(source => source.Key, source => source.Available),
                totalContribution,
                need - totalContribution);
        }

        // The tile can cover the need, so it is split in proportion to how many individuals each source feeds.
        Dictionary<FoodSourceKey, long> sharesRaw = ProportionalSplitter.Split(
            need.RawValue,
            sources.ToDictionary(source => source.Key, source => source.ContributionInNeedUnits.RawValue));

        Dictionary<FoodSourceKey, ScaledAmount> taken = sources.ToDictionary(
            source => source.Key,
            source => ToAssetAmount(sharesRaw[source.Key], source.AmountPerNeedUnit));

        return new FoodExtraction(taken, need, ScaledAmount.Zero);
    }

    /// <summary>
    /// What a share of the need costs in the source's own Asset.
    /// </summary>
    /// <remarks>
    /// Int128 because a share already in thousandths is multiplied by a price that is itself in thousandths before the scale comes back out.
    /// </remarks>
    private static ScaledAmount ToAssetAmount(long shareRaw, long amountPerUnit)
    {
        return ScaledAmount.FromRaw((long)((Int128)shareRaw * amountPerUnit / ScaledAmount.Scale));
    }
}
