namespace AscendantEmpire.FrontendContracts.Player;

public interface IPlayerStructure
{
    int CampaignMapTileId { get; }
    int? BattleMapTileId { get; }
    int XCoord { get; }
    int YCoord { get; }
    int StructureId { get; }
    string Name { get; }
    string Code { get; }
    double CompletionPercent { get; }
    int OwnerPlayerId { get; }
    int SightRange { get; }
}
