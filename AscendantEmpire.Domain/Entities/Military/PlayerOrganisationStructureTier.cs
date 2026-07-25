using System.ComponentModel.DataAnnotations;

namespace AscendantEmpire.Domain.Entities.Military;

/// <summary>
/// One tier of a <see cref="PlayerOrganisationStructure"/> — a level in the formation hierarchy.
/// </summary>
/// <remarks>
/// <para>
/// Tiers run from tier 1 (the smallest formation) up to at most <see cref="MAX_TIERS"/>. Tier index maps
/// positionally onto officer rank, so rank is not stored here.
/// </para>
/// </remarks>
public class PlayerOrganisationStructureTier
{
    public const int NAME_MAX_LENGTH = 100;

    /// <summary>
    /// The deepest a structure may go. The rank ladder is one taller than this (see
    /// <see cref="PlayerOfficerRank.MAX_LEVEL"/>).
    /// </summary>
    public const int MAX_TIERS = 5;

    public int Id { get; set; }

    public int StructureId { get; set; }

    /// <summary>
    /// 1-based tier index, smallest (1) to largest. Maps positionally onto the officer rank of the same level.
    /// </summary>
    public int TierIndex { get; set; }

    /// <summary>
    /// Customisable, cosmetic tier label (e.g. "Company", "Squadron"). Purely a name — the game concept is
    /// the tier index, not this string.
    /// </summary>
    [MaxLength(NAME_MAX_LENGTH)]
    public required string Name { get; set; }

    /// <summary>
    /// For tier 1, the base number of soldiers in the smallest formation. For tiers above 1, the fan-out —
    /// how many of the tier directly below group into one formation of this tier.
    /// </summary>
    /// <remarks>
    /// A tier's absolute soldier size is the product of the multipliers from tier 1 up to and including it
    /// (e.g. base 10, then fan-outs 5, 2 → sizes 10, 50, 100). Size is therefore derived, never stored.
    /// </remarks>
    public int Multiplier { get; set; }

    public PlayerOrganisationStructure Structure { get; set; } = null!;
}
