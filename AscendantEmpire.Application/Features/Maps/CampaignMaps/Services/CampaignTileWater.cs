using AscendantEmpire.Domain.Entities.Maps.CampaignMaps;
using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.Application.Features.Maps.CampaignMaps.Services;

/// <summary>
/// The one rule for whether a campaign map tile is water.
/// </summary>
/// <remarks>
/// Shared by movement (which domains may enter a tile) and gathering (whether a tile offers Fish or Forage and Hunt), so a tile a land Force is refused as water can never
/// also feed it as land.
/// </remarks>
internal static class CampaignTileWater
{
    /// <summary>
    /// Whether a tile with this biome and geography is water.
    /// </summary>
    /// <remarks>
    /// Both the biome and the geography record this - Coastal and Ocean carry <see cref="CampaignBiome.IsWater"/>, and the geography has its own deep and shallow water
    /// types - and the map generator keeps the two consistent. Either being water is enough, so a hand-edited map that sets only one of them still counts as water rather
    /// than quietly letting a land Force walk out to sea.
    /// </remarks>
    public static bool IsWater(bool biomeIsWater, CampaignMapGeographyType geography)
    {
        return biomeIsWater || geography is CampaignMapGeographyType.DeepWater or CampaignMapGeographyType.ShallowWater;
    }
}
