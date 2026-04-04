using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Entities.UnitDesigns;

namespace AscendantEmpire.Domain.Entities.Units;

public class TroopInstance
{
    public int Id { get; set; }
    
    public int GameId { get; set; }
    public int PlayerId { get; set; }

    public int CampaignMapTileId { get; set; }

    /// <summary>
    /// Only populated for Battle Maps.
    /// </summary>
    public int? BattleMapTileId { get; set; }

    public int UnitDesignId { get; set; }

    /// <summary>
    /// The total number of troops in this troop at creation.
    /// </summary>
    public int InitialStrength { get; set; }

    /// <summary>
    /// The number of troops currently injured in this troop.
    /// </summary>
    public int InjuredCount { get; set; }

    /// <summary>
    /// The number of troops currently healthy in this troop.
    /// </summary>
    public int HealthyCount { get; set; }

    public GameIndex Game { get; set; } = null!;
    public Player Player { get; set; } = null!;
    public CompiledDesignStats Stats { get; set; } = null!;
    public CampaignMap CampaignMap { get; set; } = null!;
}
