using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Enums.Maps;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AscendantEmpire.Domain.Entities.Maps.BattleMaps;

/// <summary>
/// Identifies one generated BattleMap: the zoomed-in view of a single CampaignMap tile
/// for a specific game instance.
///
/// BattleMaps are generated lazily (only when a battle is first triggered on a campaign
/// tile) and stored here for reuse in subsequent battles on the same tile within the
/// same game.
///
/// Each game generates its own battle maps independently — two games on the same
/// campaign map will produce different biome distributions because the biome diversity
/// pass uses GameIndex.GenerationSeed.  Terrain altitude is still derived from the
/// shared campaign Perlin field, ensuring seamless edges between adjacent battle tiles.
/// </summary>
public class BattleMapIndex
{
    /// <summary>Primary key.</summary>
    public int Id { get; set; }

    /// <summary>
    /// The game CampaignMap tile this battle map was generated for.
    /// FK to game.CampaignMap.
    /// </summary>
    public int CampaignMapId { get; set; }

    /// <summary>
    /// Starting BattleMap Id for the first tile of this battle map.
    /// Tiles are stored contiguously: tile (x,y) is at BattleMapOffsetId + (y * Size + x),
    /// where Size is GameIndex.BattleMapSize.
    /// </summary>
    public int BattleMapOffsetId { get; set; }

    /// <summary>Status of the battle map generation.</summary>
    public MapCreationStatus GenerationStatus { get; set; }

    /// <summary>Human-readable status message.</summary>
    [MaxLength(500)]
    public string Status { get; set; } = string.Empty;

    /// <summary>Error message if GenerationStatus == InError.</summary>
    [MaxLength(2000)]
    public string? ErrorMessage { get; set; }

    // ── Navigation properties ────────────────────────────────────────────────

    [ForeignKey(nameof(CampaignMapId))]
    public CampaignMap? CampaignMap { get; set; }
}
