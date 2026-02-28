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
    /// Database id (CampaignMapGalleryIndexId) for correlation / debugging.
    /// </summary>
    public required int DbId { get; init; }

    public required BiomeType OriginalBiomeType { get; init; }
    public required CampaignMapGeographyType OriginalGeographyType { get; init; }
}