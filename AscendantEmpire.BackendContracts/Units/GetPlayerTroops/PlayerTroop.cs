using AscendantEmpire.Domain.Enums;

namespace AscendantEmpire.BackendContracts.Units.GetPlayerTroops;

public class PlayerTroop
{
    public int UnitInstanceId { get; set; }
    public RecordType RecordType { get; set; }
    public int PlayerId { get; set; }
    public int CampaignMapTileId { get; set; }
    public int? BattleMapTileId { get; set; }
    public int CampaignXCoord { get; set; }
    public int CampaignYCoord { get; set; }
    public int UnitTemplateId { get; set; }
    public int SightRange { get; set; }
    public int HealthyTroops { get; set; }
    public int InjuredTroops { get; set; }
    public int InitialStrength { get; set; }
    public required string MapImagePath { get; set; }
}
