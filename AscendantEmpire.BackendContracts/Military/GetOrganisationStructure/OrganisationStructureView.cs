using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.BackendContracts.Military.GetOrganisationStructure;

/// <summary>
/// A player's organisation hierarchy for a single MilitaryOrganisationalGroup tag.
/// </summary>
/// <remarks>
/// When the player has not yet customised a hierarchy for the tag, the endpoint returns a default (unsaved)
/// one with <see cref="IsDefault"/> = true, so the page always has something to render and save.
/// </remarks>
public record OrganisationStructureView
{
    /// <summary>The tag this hierarchy is bound to.</summary>
    public required int TagId { get; init; }

    public required string TagCode { get; init; }

    public required string TagDisplayName { get; init; }

    /// <summary>The tag's movement domain, or null if the tag has no domain assigned.</summary>
    public DomainType? MovementDomain { get; init; }

    /// <summary>
    /// The maximum number of tiers this hierarchy may have — derived from the calling player's in-use officer
    /// ranks in the tag's domain (one fewer than the rank count, floored at 1). When the tag has no domain it
    /// is the absolute tier maximum.
    /// </summary>
    public required int MaxTiers { get; init; }

    /// <summary>
    /// The in-use officer rank titles for the tag's domain, in level order (index 0 = level 1). Lets the page
    /// show each tier's commanding rank. Empty when the tag has no domain.
    /// </summary>
    public required IReadOnlyList<string> RankTitles { get; init; }

    /// <summary>True when these tiers are the code default rather than a saved hierarchy.</summary>
    public required bool IsDefault { get; init; }

    /// <summary>The tiers, smallest (tier 1) to largest.</summary>
    public required IReadOnlyCollection<OrganisationTierView> Tiers { get; init; }
}
