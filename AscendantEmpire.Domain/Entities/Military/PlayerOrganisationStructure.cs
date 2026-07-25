using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Entities.UnitDesigns;

namespace AscendantEmpire.Domain.Entities.Military;

/// <summary>
/// A player's organisation structure — the shape (tier list) of a formation — bound to a single
/// <c>MilitaryOrganisationalGroup</c> <see cref="Tag"/>.
/// </summary>
/// <remarks>
/// <para>
/// The tag is the structure's identity/name (e.g. "Infantry", "Settler"); each player has at most one
/// structure per tag. The tier sizes are authored by the player; rank is derived positionally from tier
/// index. See <c>docs/design/units/military-organisation.md</c>.
/// </para>
/// </remarks>
public class PlayerOrganisationStructure
{
    public int Id { get; set; }

    /// <summary>
    /// The player this structure belongs to. Player ids are globally unique so no separate GameId is carried.
    /// </summary>
    public int PlayerId { get; set; }

    /// <summary>
    /// The <c>MilitaryOrganisationalGroup</c> tag this structure is bound to (its identity/name). One structure
    /// per tag per player.
    /// </summary>
    public int TagId { get; set; }

    public Player Player { get; set; } = null!;
    public Tag Tag { get; set; } = null!;

    /// <summary>
    /// The 1..5 tiers of this structure, smallest (tier 1) to largest.
    /// </summary>
    public ICollection<PlayerOrganisationStructureTier> Tiers { get; set; } = [];
}
