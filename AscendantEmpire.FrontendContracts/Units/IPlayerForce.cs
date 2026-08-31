namespace AscendantEmpire.FrontendContracts.Units;

public interface IPlayerForce
{
    int CampaignMapTileId { get; }
    int? BattleMapTileId { get; }
    int CampaignXCoord { get; }
    int CampaignYCoord { get; }
    int SightRange { get; }
    string MapImagePath { get; }
}
