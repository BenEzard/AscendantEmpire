using AscendantEmpire.Domain.Enums.Maps;

using System.ComponentModel.DataAnnotations;

namespace AscendantEmpire.Domain.Entities.Maps.BattleMaps;

/// <summary>
/// Identifies one generated BattleMap: the 50×50 (or configurable) zoomed-in view of a
/// single CampaignMap tile.
///
/// BattleMaps are generated lazily (only when a battle is first triggered on that campaign
/// tile) and stored here for reuse in subsequent battles on the same tile.
///
/// The generation is fully deterministic: given the master seed from the parent
/// CampaignMapGalleryIndex.PropertiesJson and the CampaignTileX/Y coordinates, the same
/// BattleMap is always produced.  GenerationSeed stores the derived local seed used for
/// this specific tile so individual tiles can be re-generated in isolation if needed.
/// </summary>
public class BattleMapGalleryIndex
{
    /// <summary>Primary key.</summary>
    public int Id { get; set; }

    /// <summary>
    /// The campaign map this battle map belongs to.
    /// FK to mapgallery.CampaignMapIndex.
    /// </summary>
    public int CampaignMapGalleryIndexId { get; set; }

    /// <summary>X coordinate of the parent CampaignMapGallery tile.</summary>
    public int CampaignTileX { get; set; }

    /// <summary>Y coordinate of the parent CampaignMapGallery tile.</summary>
    public int CampaignTileY { get; set; }

    /// <summary>
    /// Width of the battle map in tiles (always equal to Height — battle maps are square).
    /// </summary>
    public int Width { get; set; }

    /// <summary>Height of the battle map in tiles.</summary>
    public int Height { get; set; }

    /// <summary>
    /// The derived local seed used to generate this specific battle map.
    /// Computed from the campaign map's master seed + tile coordinates.
    /// Stored here so a single tile can be independently re-generated or debugged.
    /// </summary>
    public long GenerationSeed { get; set; }

    /// <summary>Status of the battle map generation.</summary>
    public MapCreationStatus GenerationStatus { get; set; }

    /// <summary>Human-readable status message.</summary>
    [MaxLength(500)]
    public string Status { get; set; } = string.Empty;

    /// <summary>Error message if GenerationStatus == InError.</summary>
    [MaxLength(2000)]
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Starting BattleMapGalleryId for the first tile of this battle map.
    /// Tiles are stored contiguously: tile (x,y) is at BattleMapOffsetId + (y * Width + x).
    /// </summary>
    public int BattleMapOffsetId { get; set; }
}
