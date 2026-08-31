using AscendantEmpire.FrontendContracts.Player;

namespace AscendantEmpire.Application.Features.Players.Classes;

public class DTPlayerStructure : IPlayerStructure
{
    public int CampaignMapTileId { get; set; }
    public int? BattleMapTileId { get; set; }
    public int XCoord { get; set; }
    public int YCoord { get; set; }
    public int StructureId { get; set; }
    public required string Name { get; set; }
    public required string Code { get; set; }
    public double CompletionPercent { get; set; }
    public int OwnerPlayerId { get; set; }
    public int SightRange { get; set; }
}
