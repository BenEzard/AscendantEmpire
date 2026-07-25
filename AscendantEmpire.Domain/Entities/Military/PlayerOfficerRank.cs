using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Enums.Maps;

using System.ComponentModel.DataAnnotations;

namespace AscendantEmpire.Domain.Entities.Military;

/// <summary>
/// A single officer rank (seniority level) for one player, within one movement domain.
/// </summary>
/// <remarks>
/// <para>
/// Ranks are configured per player: titles are editable and individual levels can be switched off. They
/// are segmented by <see cref="MovementDomain"/> so the same underlying level reads "General" for Land and
/// "Admiral" for Water. See <c>docs/design/units/military-organisation.md</c>.
/// </para>
/// <para>
/// <see cref="MaxSoldiers"/> is a <b>hard ceiling</b>, not a formation definition — an organisation-structure
/// tier mapped to this rank may never be sized beyond it. A null ceiling means "no soldier cap of its own"
/// (the top rank, whose reach is governed by its maximum number of Units instead).
/// </para>
/// </remarks>
public class PlayerOfficerRank
{
    public const int TITLE_MAX_LENGTH = 100;

    /// <summary>
    /// The highest level a player's rank ladder may reach. One more than the maximum organisation tier
    /// depth (see <see cref="PlayerOrganisationStructureTier.MAX_TIERS"/>) so a maximally-deep formation
    /// always has a rank spare for its single top commander.
    /// </summary>
    public const int MAX_LEVEL = PlayerOrganisationStructureTier.MAX_TIERS + 1;

    public int Id { get; set; }

    /// <summary>
    /// The player this rank configuration belongs to. Player ids are globally unique, so the player already
    /// identifies the game — no separate GameId column is carried (matching the other per-player tables).
    /// </summary>
    public int PlayerId { get; set; }

    /// <summary>
    /// The movement domain this rank commands within (Land, Water, etc.). Ranks are configured independently
    /// per domain, which is also what drives the domain-specific title.
    /// </summary>
    public DomainType MovementDomain { get; set; }

    /// <summary>
    /// 1-based seniority level (1 = most junior). Maps positionally onto an organisation-structure tier.
    /// </summary>
    public int Level { get; set; }

    /// <summary>
    /// The display title for this rank/domain (e.g. "General" for Land, "Admiral" for Water). Player-editable.
    /// </summary>
    [MaxLength(TITLE_MAX_LENGTH)]
    public required string Title { get; set; }

    /// <summary>
    /// Hard ceiling on the number of soldiers a tier commanded at this rank may hold. Null means the rank has
    /// no soldier ceiling of its own (the top rank).
    /// </summary>
    public int? MaxSoldiers { get; set; }

    /// <summary>
    /// The most specialists a formation led at this rank may have attached.
    /// </summary>
    public int MaxAttachedSpecialists { get; set; }

    /// <summary>
    /// Whether this level is currently in use for the player/domain. In-use levels form a contiguous prefix
    /// from level 1 (turning a level off cascades to every level above it — see the Military Advisor UI).
    /// </summary>
    public bool InUse { get; set; }

    public Player Player { get; set; } = null!;
}
