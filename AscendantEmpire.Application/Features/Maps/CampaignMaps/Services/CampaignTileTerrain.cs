using AscendantEmpire.Domain.Entities.Maps.CampaignMaps;

namespace AscendantEmpire.Application.Features.Maps.CampaignMaps.Services;

/// <summary>
/// The one rule for each fact a campaign map tile's biome and geography flags combine to state.
/// </summary>
/// <remarks>
/// Movement, gathering, sustenance and the per-Round territory pass all read these rather than a copy, so they cannot drift apart about the same tile.
/// </remarks>
internal static class CampaignTileTerrain
{
    /// <summary>
    /// Whether a tile with this biome and geography is water.
    /// </summary>
    public static bool IsWater(bool biomeIsWater, bool geographyIsWater)
    {
        return biomeIsWater || geographyIsWater;
    }

    /// <summary>
    /// Whether nothing can enter a tile with this biome and geography, whatever its movement domain.
    /// </summary>
    public static bool IsImpassable(bool biomeIsTraversable, bool geographyIsTraversable)
    {
        return biomeIsTraversable == false || geographyIsTraversable == false;
    }

    /// <summary>
    /// Whether it is the ground itself that cannot be crossed, rather than what is painted on it.
    /// </summary>
    public static bool GeographyIsImpassable(bool geographyIsTraversable)
    {
        return geographyIsTraversable == false;
    }
}
