namespace AscendantEmpire.BackendContracts.Units.GetPlayerTroops;

public class PlayerTroop
{
    public int Id { get; set; }
    public int PlayerId { get; set; }
    public int CampaignMapTileId { get; set; }
    public int? BattleMapTileId { get; set; }
    public int CampaignXCoord { get; set; }
    public int CampaignYCoord { get; set; }
    public int SightRange { get; set; }
    public required string MapImagePath { get; set; }
}
