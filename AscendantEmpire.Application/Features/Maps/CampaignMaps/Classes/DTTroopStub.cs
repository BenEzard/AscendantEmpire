using AscendantEmpire.Domain.Enums;
using AscendantEmpire.FrontendContracts.CampaignMaps;

namespace AscendantEmpire.Application.Features.Maps.CampaignMaps.Classes;

public class DTTroopStub : ITroopStub
{
    public int Id { get; set; }
    public RecordType RecordType { get; set; }
    public int OwnerPlayerId { get; init; }
    public int HealthyTroops { get; set; }
    public int InjuredTroops { get; set; }
    public int InitialStrength { get; set; }
    public int UnitTemplateId { get; set; }
}
