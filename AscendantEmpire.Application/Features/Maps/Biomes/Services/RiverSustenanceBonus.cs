using AscendantEmpire.Application.Helpers.ScaledNumerics;
using AscendantEmpire.BackendContracts.CampaignMaps.GetPlayerCampaignMap;
using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.Application.Features.Maps.Biomes.Services;

/// <summary>
/// What the rivers on a land tile's edges add to its sustenance values.
/// </summary>
/// <remarks>
/// The bonuses stack per edge. 
/// </para>
/// <para>
/// <b>Land only.</b> A water tile is returned unchanged: it already fishes its own biome, and a river mouth is not meant to make the sea richer.
/// </para>
/// <para>
/// Applied to a Player's <i>effective</i> value, after <see cref="SustenanceValueCalculator.CalculateEffectiveValue"/> has folded in their own modifiers, 
/// so the percentage compounds with those rather than summing into their total the way two Player modifiers do. 
/// It is also applied to a biome's regeneration base, so a riverbank regrows the fish it just gave up.
/// </para>
/// </remarks>
internal static class RiverSustenanceBonus
{
    /// <summary>
    /// The percentage each river edge adds to Forage, Hunt and Fodder, whatever the river's width.
    /// </summary>
    private static readonly ScaledPercent _PERCENT_BONUS_PER_EDGE = ScaledPercent.FromWholePercent(20);

    /// <summary>
    /// The flat Fish a single Medium river edge adds.
    /// </summary>
    private static readonly ScaledAmount _MEDIUM_RIVER_FISH = ScaledAmount.FromWhole(15);

    /// <summary>
    /// The flat Fish a single Large river edge adds.
    /// </summary>
    private static readonly ScaledAmount _LARGE_RIVER_FISH = ScaledAmount.FromWhole(30);

    /// <summary>
    /// Adds whatever the tile's rivers are worth to one of its sustenance values.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A <see langword="null"/> value means the biome does not have the resource, and stays <see langword="null"/> for Forage, Hunt and Fodder: a river makes grazing better,
    /// it does not create it. <b>Fish is the deliberate exception</b> - its bonus is flat, and it is the one case that gives a value to a resource the biome does not have,
    /// because putting fish on dry land is the whole point of the rule.
    /// </para>
    /// <para>
    /// The result is floored at 0, so a value already driven negative by penalties cannot come back out of a river as a negative one.
    /// </para>
    /// </remarks>
    /// <param name="value">The value to adjust - a Player's effective value, or a biome's regeneration base. <see langword="null"/> when the biome has no such resource.</param>
    /// <param name="resource">Which resource <paramref name="value"/> belongs to.</param>
    /// <param name="rivers">The tile's four edges, or <see langword="null"/> when they are not known, which is read as no rivers.</param>
    /// <returns>The adjusted value, or <see langword="null"/> where there is nothing to adjust and no Fish to add.</returns>
    internal static ScaledAmount? Apply(ScaledAmount? value, SustenanceResource resource, TileRivers? rivers, bool isWater)
    {
        // A water tile fishes its own biome, and nothing a river does changes that.
        if (isWater || rivers is null || rivers.HasAny == false)
        {
            return value;
        }

        ScaledAmount? adjustedValue = resource switch
        {
            SustenanceResource.Forage or SustenanceResource.Hunt or SustenanceResource.Fodder => AddEdgePercentage(value, rivers),
            SustenanceResource.Fish => AddEdgeFish(value, rivers),

            // A resource outside the enum is nothing this can speak for.
            _ => value
        };

        // Only a value that existed - or one Fish has just created - can be floored; a resource the tile does not have stays absent.
        return adjustedValue is null || adjustedValue.Value >= ScaledAmount.Zero ? adjustedValue : ScaledAmount.Zero;
    }

    /// <summary>
    /// Adds whatever the tile's rivers are worth to a raw stored value, for the callers that hold a biome's base as a <see cref="long"/>.
    /// </summary>
    /// <remarks>
    /// The same rule as the main overload; only the shape of the input differs. The end-of-Round regrowth and the support forecast both read their base out of a biome's
    /// scaled <c>Base*Value</c> column, and both must regrow a riverbank by what the river put there.
    /// </remarks>
    /// <param name="rawValue">The value to adjust, scaled by <see cref="ScaledAmount.Scale"/>, or <see langword="null"/> when the biome has no such resource.</param>
    /// <param name="resource">Which resource <paramref name="rawValue"/> belongs to.</param>
    /// <param name="rivers">The tile's four edges, or <see langword="null"/> when they are not known, which is read as no rivers.</param>
    internal static ScaledAmount? Apply(long? rawValue, SustenanceResource resource, TileRivers? rivers, bool isWater)
    {
        return Apply(rawValue is null ? null : ScaledAmount.FromRaw(rawValue.Value), resource, rivers, isWater);
    }

    /// <summary>
    /// Adds the percentage every river edge is worth, summed rather than compounded across the edges.
    /// </summary>
    private static ScaledAmount? AddEdgePercentage(ScaledAmount? value, TileRivers rivers)
    {
        // A biome with no Hunt is not given Hunt by a river, and the same holds for Forage and Fodder.
        if (value is null)
        {
            return null;
        }

        int edgeCount = CountRiverEdges(rivers);

        // Summed into one percentage and applied once, so two edges are +40% rather than 1.2 x 1.2.
        ScaledPercent totalPercent = ScaledPercent.FromRaw(_PERCENT_BONUS_PER_EDGE.RawValue * edgeCount);

        return value.Value.AddBonus(totalPercent);
    }

    /// <summary>
    /// Adds the flat Fish every river edge is worth, giving a dry tile a Fish value where it had none.
    /// </summary>
    private static ScaledAmount? AddEdgeFish(ScaledAmount? value, TileRivers rivers)
    {
        ScaledAmount fishBonus = GetEdgeFish(rivers.North) + GetEdgeFish(rivers.East) + GetEdgeFish(rivers.South) + GetEdgeFish(rivers.West);

        if (fishBonus == ScaledAmount.Zero)
        {
            return value;
        }

        // The one place a null becomes a number: the river is what gives the tile its Fish.
        return (value ?? ScaledAmount.Zero) + fishBonus;
    }

    /// <summary>
    /// The flat Fish one edge is worth, by the width of the river on it.
    /// </summary>
    private static ScaledAmount GetEdgeFish(RiverWidth width)
    {
        return width switch
        {
            RiverWidth.Medium => _MEDIUM_RIVER_FISH,
            RiverWidth.Large => _LARGE_RIVER_FISH,
            _ => ScaledAmount.Zero
        };
    }

    /// <summary>
    /// How many of the tile's four edges carry a river, of any width.
    /// </summary>
    private static int CountRiverEdges(TileRivers rivers)
    {
        int edgeCount = 0;

        foreach (RiverWidth width in (RiverWidth[])[rivers.North, rivers.East, rivers.South, rivers.West])
        {
            if (width != RiverWidth.None)
            {
                edgeCount++;
            }
        }

        return edgeCount;
    }
}
