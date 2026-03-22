using AscendantEmpire.Domain.Enums;
using AscendantEmpire.Domain.Enums.Maps;
using AscendantEmpire.Domain.Enums.Structures;

namespace AscendantEmpire.BackendContracts.Players.GetPlayerStructures;

public record PlayerStructure
{
    public int StructureInstanceId { get; init; }
    public RecordType RecordTypeId { get; init; } = RecordType.Structure;
    public int CampaignMapTileId { get; init; }
    public int? BattleMapTileId { get; init; }
    public int XCoord { get; init; }
    public int YCoord { get; init; }
    public int StructureId { get; init; }
    public TileSlotType SlotType { get; set; }
    public PositionOnTile Position { get; set; }
    public string Name { get; init; } = null!;
    public string Code { get; init; } = null!;
    public double CompletionPercent { get; init; }
    public int OwnerPlayerId { get; init; }
    public int SightRange { get; init; }
    public DetectionDirection DetectionDirection { get; init; }
    public string MapImagePath { get; init; } = null!;
}
