using AscendantEmpire.Domain.Enums;
using AscendantEmpire.FrontendContracts.CampaignMaps;

namespace AscendantEmpire.Application.Handlers.Maps.CampaignMaps.Classes;

public class DTTroopStub : ITroopStub
{
    public int Id { get; set; }

    public RecordType RecordType { get; set; }

    public int UnitDesignId { get; set; }
    public int Health { get; set; }
}
