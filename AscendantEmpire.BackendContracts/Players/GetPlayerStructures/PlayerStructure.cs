using AscendantEmpire.Domain.Enums;
using AscendantEmpire.Domain.Enums.Maps;
using AscendantEmpire.Domain.Enums.Structures;

namespace AscendantEmpire.BackendContracts.Players.GetPlayerStructures;

public record PlayerStructure
{
    public int StructureInstanceId { get; init; }
    public RecordType RecordType { get; init; } = RecordType.Structure;
    public int CampaignMapTileId { get; init; }
    public int? BattleMapTileId { get; init; }
    public int XCoord { get; init; }
    public int YCoord { get; init; }
    public int StructureId { get; init; }
    public TileSlotType SlotType { get; set; }
    public PositionOnTile Position { get; set; }
    /// <summary>The kind's name — "Village", "Wooden Watch Tower". Never the name of this particular Structure.</summary>
    public string Name { get; init; } = null!;

    /// <summary>The kind's stable catalogue code, which is what the client's artwork is keyed on.</summary>
    public string Code { get; init; } = null!;

    /// <summary>
    /// The name the owner gave the Settlement standing here.
    /// </summary>
    public string? SettlementName { get; init; }

    public double CompletionPercent { get; init; }
    public int OwnerPlayerId { get; init; }
    public int SightRange { get; init; }
    public DetectionDirection DetectionDirection { get; init; }
    public string MapImagePath { get; init; } = null!;
}
