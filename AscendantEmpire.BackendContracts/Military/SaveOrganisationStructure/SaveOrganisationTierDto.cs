namespace AscendantEmpire.BackendContracts.Military.SaveOrganisationStructure;

/// <summary>
/// One tier in a save of a player's organisation structure for a tag.
/// </summary>
public record SaveOrganisationTierDto
{
    /// <summary>1-based tier index, smallest first. Must run contiguously from 1.</summary>
    public required int TierIndex { get; init; }

    public required string Name { get; init; }

    /// <summary>Tier 1: base soldier count. Tiers above: fan-out of the tier below.</summary>
    public required int Multiplier { get; init; }
}
