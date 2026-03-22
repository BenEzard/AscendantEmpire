using AscendantEmpire.Domain.Enums;

namespace AscendantEmpire.FrontendContracts.CampaignMaps;

public interface ITroopStub
{
    public int Id { get; }
    public RecordType RecordType { get; }
    public int UnitDesignId { get; }
}