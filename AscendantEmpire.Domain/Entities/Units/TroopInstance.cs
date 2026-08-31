using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Entities.UnitDesigns;

namespace AscendantEmpire.Domain.Entities.Units;

public class TroopInstance
{
    public int Id { get; set; }
    
    public int GameId { get; set; }
    public int PlayerId { get; set; }

    public int CampaignMapTileId { get; set; }

    public string? TroopName { get; set; }

    /// <summary>
    /// Only populated for Battle Maps.
    /// </summary>
    public int? BattleMapTileId { get; set; }

    /// <summary>
    /// The <see cref="UnitTemplate"/> this troop was created from. Runtime reads (map image, movement points,
    /// combat stats) come from the template's 1:1 <see cref="CompiledUnitTemplate"/> snapshot.
    /// </summary>
    public int UnitTemplateId { get; set; }

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

    /// <summary>
    /// How many Movement Points (MP) this troop has remaining for the current turn.
    /// </summary>
    /// <remarks>
    /// Stored as a raw fixed-point integer using the Scale 1,000 convention of <c>ScaledAmount</c> — a stored value of 1,000 represents 1.0 MP.
    /// This matches <c>CompiledUnitTemplate.CampaignMovementPoints</c>, the per-turn allowance it is spent down from, so the two can be compared
    /// directly rather than converting between representations at every call site.
    /// </remarks>
    public long MovementPointsRemaining { get; set; }

    public GameIndex Game { get; set; } = null!;
    public Player Player { get; set; } = null!;
    public UnitTemplate UnitTemplate { get; set; } = null!;
    public CampaignMap CampaignMap { get; set; } = null!;
    public virtual ICollection<TroopOrder> Orders { get; set; } = [];
}
