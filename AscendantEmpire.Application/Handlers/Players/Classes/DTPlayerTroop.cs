using AscendantEmpire.FrontendContracts.Units;

namespace AscendantEmpire.Application.Handlers.Players.Classes;

public class DTPlayerTroop : IPlayerTroop
{
    public int CampaignMapTileId { get; set; }

    public int? BattleMapTileId { get; set; }

    public int CampaignXCoord { get; set; }

    public int CampaignYCoord { get; set; }

    public int SightRange { get; set; }

    public required string MapImagePath { get; set; }
}
