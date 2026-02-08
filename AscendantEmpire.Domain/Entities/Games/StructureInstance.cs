using AscendantEmpire.Domain.Enums.Structures;

using System.ComponentModel.DataAnnotations.Schema;

namespace AscendantEmpire.Domain.Entities.Games;

/// <summary>
/// This table contains the WORLD view (source-of-truth) of Structures.
/// </summary>
/// <remarks>Note ownership of a structure on the Campaign Map is determined by the Player who owns the Tile.</remarks>
public class StructureInstance
{
    public int Id { get; set; }

    public int GameId { get; set; }

    public int CampaignMapTileId { get; set; }

    /// <summary>
    /// Will be null for Structures on the Campaign Map, and populated on the Battle Map.
    /// </summary>
    public int? BattleMapTileId { get; set; }

    public int StructureId { get; set; }

    /// <summary>
    /// The type of slot this Structure occupies on the Tile.
    /// </summary>
    /// <remarks>For BattleMaps will be Primary</remarks>
    public TileSlotType SlotType { get; set; }

    /// <summary>
    /// The position of the Structure on the Tile.
    /// </summary>
    /// <remarks>For BattleMaps will be WholeTile</remarks>
    public PositionOnTile Position { get; set; }

    public int OwnedByPlayerId { get; set; }

    public int CreatedOnTurnId { get; set; }
    public double CompletionPercent { get; set; }
    public int? CompletedOnTurnId { get; set; }
    public int? RemovedOnTurnId { get; set; }

    [ForeignKey("GameId")]
    public GameIndex Game { get; set; } = null!;

    [ForeignKey("StructureId")]
    public Structure.Structure Structure { get; set; } = null!;

    [ForeignKey("CampaignMapTileId")]
    public CampaignMap CampaignMap { get; set; } = null!;

    [ForeignKey("OwnedByPlayerId")]
    public Player OwnedBy { get; set; } = null!;
}
