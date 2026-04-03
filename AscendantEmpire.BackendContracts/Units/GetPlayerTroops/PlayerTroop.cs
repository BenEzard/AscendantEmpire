using AscendantEmpire.Domain.Enums;

namespace AscendantEmpire.BackendContracts.Units.GetPlayerTroops;

public class PlayerTroop
{
    public int TroopInstanceId { get; set; }
    public RecordType RecordType { get; set; }
    public int PlayerId { get; set; }
    public int CampaignMapTileId { get; set; }
    public int? BattleMapTileId { get; set; }
    public int CampaignXCoord { get; set; }
    public int CampaignYCoord { get; set; }
    public int UnitDesignId { get; set; }
    public int SightRange { get; set; }
    public int Health { get; set; }
    public required string MapImagePath { get; set; }
}
