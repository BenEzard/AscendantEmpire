using AscendantEmpire.Domain.Enums.Structures;

using System.ComponentModel.DataAnnotations;

namespace AscendantEmpire.Domain.Entities.Structures;

public class Structure
{
    public const int CODE_MAX_LENGTH = 50;
    public const int NAME_MAX_LENGTH = 100;

    public int Id { get; set; }

    [MaxLength(CODE_MAX_LENGTH)]
    public required string Code { get; set; }

    [MaxLength(NAME_MAX_LENGTH)]
    public required string Name { get; set; }

    /// <summary>
    /// The type of slot this Structure can be built in. 
    /// </summary>
    public required TileSlotType RequiresSlotType { get; set; }

    /// <summary>
    /// The possible positions this Structure can be built in on a Tile.
    /// </summary>
    public required PositionOnTile AllowedPositions { get; set; }

    public required bool CanBeBuiltOnCampaignMap { get; set; }
    public required bool CanBeBuiltOnBattleMap { get; set; }

    /// <summary>
    /// Denotes that building this Structure on a Campaign Map will also trigger its construction on the Battle Map.
    /// </summary>
    public required bool DualMapBuildTrigger { get; set; }

    public required int SightRange { get; set; }
}
