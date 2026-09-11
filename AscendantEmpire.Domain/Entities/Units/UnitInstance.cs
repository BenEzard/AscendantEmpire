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

    /// <summary>
    /// The campaign map tile the Unit is standing on.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>init</c> rather than <c>set</c>, on purpose. A Unit is placed on a tile when it is created, and after that it only ever moves through <c>IForceLifecycleWriter</c>, which records the
    /// move in <see cref="MovementHistory"/> as part of the same save. An init-only setter turns a second writer into a compile error, instead of a move that silently never reaches anybody's
    /// intelligence.
    /// </para>
    /// <para>
    /// It guards the ordinary path rather than every path. The writer itself reaches the property through the change tracker, and so could anything else; a bulk <c>ExecuteUpdateAsync</c> or a
    /// new Unit added straight to the context would get past it too. Those stay conventions rather than compile errors.
    /// </para>
    /// </remarks>
    public int CampaignMapTileId { get; init; }

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
    /// How many campaign-map Movement Points (MP) this Unit has remaining for the current Turn.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Stored as a raw fixed-point integer using the Scale 1,000 convention of <c>ScaledAmount</c> — a stored value of 1,000 represents 1.0 MP.
    /// This matches <c>CompiledUnitTemplate.CampaignMovementPoints</c>, the per-Turn allowance it is replenished from, so the two can be compared
    /// directly rather than converting between representations at every call site.
    /// </para>
    /// <para>
    /// May legitimately sit <b>above</b> that allowance, because unspent movement is carried into the next Turn and added on top of it, and may sit
    /// <b>below zero</b>, because entering a tile needs only half its cost but spends the whole of it. Neither is an error state — see
    /// <c>MovementReplenishment</c> for the rule that reconciles both.
    /// </para>
    /// </remarks>
    public long CampaignMovementRemaining { get; set; }

    /// <summary>
    /// How many battle-map Movement Points (MP) this Unit has remaining in its current Battle.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The same raw Scale 1,000 convention as <see cref="CampaignMovementRemaining"/>, matching <c>CompiledUnitTemplate.BattlefieldMovementPoints</c>.
    /// Held apart from the campaign figure because the two are spent on different maps against different costs, and a Unit drawn into a Battle still
    /// holds the campaign movement it will need once the Battle releases it.
    /// </para>
    /// <para>
    /// Granted when the Unit is fielded. <b>Nothing replenishes it yet</b>: Battle Rounds are not Campaign Rounds, and the battle turn pipeline has no
    /// processes and no ordering enum to hang one on.
    /// </para>
    /// </remarks>
    public long BattleMovementRemaining { get; set; }

    /// <summary>
    /// The Campaign Round in which this Unit was last granted its campaign movement allowance. Zero means it never has been.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The idempotency stamp for the start-of-Turn replenishment. A Player takes exactly one Turn per Campaign Round, so (Unit, Round) is the grant
    /// key: the process only touches Units whose stamp is strictly below the Round being opened, and stamps every Unit it grants.
    /// </para>
    /// <para>
    /// This is load-bearing rather than defensive, because replenishment is <b>not</b> naturally idempotent. A second run within one Round sees a
    /// leftover at or above the allowance, banks nothing, and so silently destroys the carry the first run just granted.
    /// </para>
    /// <para>
    /// Deliberately campaign-qualified. A battle equivalent could not reuse a single <c>int</c>: Battle Round numbers restart with each Battle, so it
    /// would need the Battle's identity alongside the number to tell one Round 3 from another.
    /// </para>
    /// </remarks>
    public int CampaignMovementReplenishedOnRound { get; set; }

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
