using AscendantEmpire.Domain.Enums;

namespace AscendantEmpire.BackendContracts.CampaignMaps.GetPlayerCampaignMap;

public record ObservedTroop
{
    public int Id { get; init; }
    public RecordType RecordType { get; init; }
    public int UnitDesignId { get; init; }
    public int HealthyTroops { get; init; }
    public int InjuredTroops { get; init; }

    /// <summary>
    /// The total number of troops when it was first created.
    /// </summary>
    public int InitialStrength { get; init; }
}
