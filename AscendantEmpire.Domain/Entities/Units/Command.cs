using AscendantEmpire.Domain.Entities.Games;

using System.ComponentModel.DataAnnotations;

namespace AscendantEmpire.Domain.Entities.Units;

/// <summary>
/// One or more <see cref="UnitInstance"/>s operating under a shared command structure on a single campaign tile.
/// </summary>
/// <remarks>
/// <para>
/// The military concept, <b>not</b> a CQRS command. Nothing in this solution uses "Command" for a write request — requests are named
/// <c>*Request</c> and handled by <c>*Handler</c> — so the name is free, but it is worth saying once.
/// </para>
/// <para>
/// A Command and a <see cref="UnitInstance"/> are collectively a <b>Force</b>: whatever occupies a tile and moves as one. Force is a category
/// rather than a type, so there is no shared base here — see <c>docs/work-in-progress/unit-command-terminology.md</c>.
/// </para>
/// <para>
/// Forming a Command is always <b>deliberate</b>. A lone Unit is never automatically wrapped in one, because a Command consumes an officer of the
/// <i>(max tiers)+1</i> top rank and that pool is deliberately scarce. A player may still choose to form one around a single Unit; the design
/// permits it and it is simply rarely worth the officer.
/// </para>
/// <para>
/// <b>The commanding officer is deliberately absent.</b> The design gives a Command one Command Commander whose rank sets its maximum total
/// strength, maximum number of Units and maximum attached Specialists — but neither <c>Officer</c> nor <c>Specialist</c> exists as an entity yet
/// (<c>PlayerOfficerRank</c> is per-player rank <i>configuration</i>, not a person), so there is nothing to reference and none of those limits are
/// enforced anywhere. When officers are modelled this entity gains an officer reference and the limits derive from that officer's rank. Adding a
/// bare rank column now would invent a shape the real relationship may not want. See
/// <c>docs/design/units/military-organisation-implementation-notes.md</c>, which parks the officer economy explicitly.
/// </para>
/// </remarks>
public class Command
{
    public const int NAME_MAX_LENGTH = 100;

    public int Id { get; set; }

    public int GameId { get; set; }

    /// <summary>
    /// The player who owns this Command. Named for ownership rather than command, consistently with the rest of the model.
    /// </summary>
    public int OwnerPlayerId { get; set; }

    public int CampaignMapTileId { get; set; }

    /// <summary>
    /// Only populated for Battle Maps, matching <see cref="UnitInstance.BattleMapTileId"/>.
    /// </summary>
    public int? BattleMapTileId { get; set; }

    /// <summary>
    /// The player's own name for this Command, or null when it has not been named.
    /// </summary>
    [MaxLength(NAME_MAX_LENGTH)]
    public string? Name { get; set; }

    public GameIndex Game { get; set; } = null!;
    public Player Player { get; set; } = null!;
    public CampaignMap CampaignMap { get; set; } = null!;

    /// <summary>
    /// The Units serving under this Command.
    /// </summary>
    /// <remarks>
    /// The design requires every member to stand on the same campaign tile as the Command itself. Nothing enforces that yet, because nothing
    /// forms a Command yet — the operation that does will own the rule.
    /// </remarks>
    public virtual ICollection<UnitInstance> Units { get; set; } = [];
}
