using AscendantEmpire.Domain.Enums;
using AscendantEmpire.FrontendContracts.CampaignMaps;

namespace AscendantEmpire.Application.Features.Maps.CampaignMaps.Classes;

public class DTForceStub : IForceStub
{
    public int Id { get; set; }
    public RecordType RecordType { get; set; }
    public int OwnerPlayerId { get; init; }
    public int HealthyTroops { get; set; }
    public int InjuredTroops { get; set; }
    public int InitialStrength { get; set; }
    public int UnitTemplateId { get; set; }
    public bool? HasCampaignMovementRemaining { get; set; }
    public decimal? CampaignMovementRemaining { get; set; }
    public decimal? CampaignMovementPoints { get; set; }
    public decimal? BattleMovementRemaining { get; set; }
}
