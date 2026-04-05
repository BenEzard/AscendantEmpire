using AscendantEmpire.Domain.Enums;

namespace AscendantEmpire.FrontendContracts.CampaignMaps;

public interface ITroopStub
{
    public int Id { get; }
    public RecordType RecordType { get; }
    public int CommanderId { get; init; }
    public int HealthyTroops { get; }
    public int InjuredTroops { get; }
    public int InitialStrength { get; set; }
    public int UnitDesignId { get; }
}