using AscendantEmpire.Domain.Enums;
using AscendantEmpire.Domain.Enums.Structures;

namespace AscendantEmpire.BackendContracts.CampaignMaps.GetPlayerCampaignMap;

public record ObservedStructure
{
    public int Id { get; init; }
    public RecordType RecordType { get; init; }
    public int OwnerPlayerId { get; init; }
    public int StructureId { get; init; }
    public PositionOnTile PositionOnTile { get; init; }
    public double CompletionPercent { get; init; }
}
