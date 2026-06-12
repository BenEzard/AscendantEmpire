using AscendantEmpire.Domain.Entities.Maps.CampaignMaps;
using AscendantEmpire.Domain.Enums.Maps;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AscendantEmpire.Domain.Entities.Maps.BattleMaps;

/// <summary>
/// A single tile within a BattleMap.
///
/// The BattleMap grid is BattleMapSize × BattleMapSize (configured per game on GameIndex).
/// Each tile has:
///   - AltitudeLevel: terrain height in the battle (-1 = below water, 0–4 = above water)
///   - BiomeTypeId:   the local biome (may differ from the parent CampaignMap tile's biome
///                    to create fine-grained variety within the battle area)
///
/// The primary key is NOT auto-generated.  IDs are assigned sequentially starting from
/// BattleMapIndex.BattleMapOffsetId, ordered by (YCoord, XCoord).
/// This mirrors the pattern used by CampaignMapGallery.
/// </summary>
public class BattleMap
{
    /// <summary>Primary key — assigned manually at generation time.</summary>
    [Key, DatabaseGenerated(DatabaseGeneratedOption.None)]
    public int Id { get; set; }

    /// <summary>FK to battle.BattleMapIndex.</summary>
    public int BattleMapIndexId { get; set; }

    /// <summary>X position within the battle map (0-based).</summary>
    public int XCoord { get; set; }

    /// <summary>Y position within the battle map (0-based).</summary>
    public int YCoord { get; set; }

    /// <summary>
    /// Terrain altitude for this tile.
    /// Stored as int to match the BattleMapAltitude enum values (-1 to 4).
    /// Adjacent tiles may differ by more than 1, representing cliffs.
    /// </summary>
    public int AltitudeLevel { get; set; }

    /// <summary>FK to map.CampaignBiomes.</summary>
    public BiomeType BiomeTypeId { get; set; }

    // ── Navigation properties ────────────────────────────────────────────────

    [ForeignKey(nameof(BattleMapIndexId))]
    public BattleMapIndex? Index { get; set; }

    [ForeignKey(nameof(BiomeTypeId))]
    public CampaignBiome? Biome { get; set; }
}
