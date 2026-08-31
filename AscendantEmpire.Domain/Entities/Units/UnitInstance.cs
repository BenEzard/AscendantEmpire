using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Entities.UnitDesigns;

namespace AscendantEmpire.Domain.Entities.Units;

/// <summary>
/// One fielded Unit — a body of soldiers built from a single <see cref="UnitTemplate"/>, standing on one tile.
/// </summary>
/// <remarks>
/// <para>
/// The "Instance" suffix distinguishes the fielded thing from the <see cref="UnitTemplate"/> it was built from; both are Units in the design
/// vocabulary and only one of them exists on a map. A Command — two or more Units under a shared command structure — is the sibling concept and
/// takes no suffix, having no template to be distinguished from.
/// </para>
/// <para>
/// A Unit and a Command are collectively a <b>Force</b>, which is a category rather than a type: see
/// <c>docs/work-in-progress/unit-command-terminology.md</c>.
/// </para>
/// </remarks>
public class UnitInstance
{
    public int Id { get; set; }

    public int GameId { get; set; }
    public int PlayerId { get; set; }

    public int CampaignMapTileId { get; set; }

    /// <summary>
    /// The player's own name for this Unit, or null when it has not been named.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Only populated for Battle Maps.
    /// </summary>
    public int? BattleMapTileId { get; set; }

    /// <summary>
    /// The <see cref="UnitTemplate"/> this Unit was created from. Runtime reads (map image, movement points,
    /// combat stats) come from the template's 1:1 <see cref="CompiledUnitTemplate"/> snapshot.
    /// </summary>
    public int UnitTemplateId { get; set; }

    /// <summary>
    /// The number of individual soldiers this Unit was created with.
    /// </summary>
    public int InitialStrength { get; set; }

    /// <summary>
    /// The number of individual soldiers in this Unit who are currently wounded.
    /// </summary>
    public int InjuredCount { get; set; }

    /// <summary>
    /// The number of individual soldiers in this Unit who are currently unwounded.
    /// </summary>
    public int HealthyCount { get; set; }

    /// <summary>
    /// How many Movement Points (MP) this Unit has remaining for the current turn.
    /// </summary>
    /// <remarks>
    /// Stored as a raw fixed-point integer using the Scale 1,000 convention of <c>ScaledAmount</c> — a stored value of 1,000 represents 1.0 MP.
    /// This matches <c>CompiledUnitTemplate.CampaignMovementPoints</c>, the per-turn allowance it is spent down from, so the two can be compared
    /// directly rather than converting between representations at every call site.
    /// </remarks>
    public long MovementPointsRemaining { get; set; }

    /// <summary>
    /// The <see cref="Units.Command"/> this Unit serves under, or null when it is operating alone.
    /// </summary>
    /// <remarks>
    /// Null is the ordinary case, not an exceptional one: forming a Command costs an officer from a deliberately scarce pool, so most Units are
    /// never in one. Units sharing a tile with a null <c>CommandId</c> are a "unit stack" — together by circumstance rather than under one
    /// commander — and get none of a Command's shared vision or bonuses.
    /// </remarks>
    public int? CommandId { get; set; }

    public GameIndex Game { get; set; } = null!;
    public Player Player { get; set; } = null!;
    public UnitTemplate UnitTemplate { get; set; } = null!;
    public CampaignMap CampaignMap { get; set; } = null!;
    public Command? Command { get; set; }
    public virtual ICollection<ForceOrder> Orders { get; set; } = [];
}
