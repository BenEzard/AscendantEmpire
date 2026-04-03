using AscendantEmpire.Domain.Enums;

namespace AscendantEmpire.BackendContracts.CampaignMaps.GetPlayerCampaignMap;

public record ObservedTroop
{
    public int Id { get; init; }
    public RecordType RecordType { get; init; }
    public int UnitDesignId { get; init; }
    public int Health { get; init; }
}
