using AscendantEmpire.BackendContracts.CampaignMaps;
using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.BackendContracts.MapGallery.UpdateCampaignMapTiles;

/// <summary>
/// Precise per-tile revision details so the UI can restore the original values.
/// </summary>
public sealed record class TileUpdateFailTile
{
    public required Coord Coord { get; init; }

    /// <summary>
    /// The tile's own database id (<c>CampaignMapGalleryId</c>), for correlation and debugging.
    /// </summary>
    /// <remarks>
    /// The tile, not the map it belongs to: one of these per tile left as it was.
    /// </remarks>
    public required int DbId { get; init; }

    public required BiomeType OriginalBiomeType { get; init; }
    public required CampaignMapGeographyType OriginalGeographyType { get; init; }
}