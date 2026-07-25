namespace AscendantEmpire.BackendContracts.Military.GetOrganisationStructure;

/// <summary>
/// One tier of an organisation structure as shown on the Military Advisor's Organisation page.
/// </summary>
public record OrganisationTierView
{
    /// <summary>1-based tier index, smallest first. Maps positionally onto officer rank.</summary>
    public required int TierIndex { get; init; }

    /// <summary>Customisable, cosmetic tier label (e.g. "Company").</summary>
    public required string Name { get; init; }

    /// <summary>Tier 1: base soldier count. Tiers above: fan-out of the tier below.</summary>
    public required int Multiplier { get; init; }
}
