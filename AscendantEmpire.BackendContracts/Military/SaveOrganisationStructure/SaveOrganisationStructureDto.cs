namespace AscendantEmpire.BackendContracts.Military.SaveOrganisationStructure;

/// <summary>
/// Save (PUT) the calling player's organisation structure for one MilitaryOrganisationalGroup tag. The tag
/// is taken from the route; the whole tier set is replaced with the tiers supplied here.
/// </summary>
public record SaveOrganisationStructureDto
{
    /// <summary>The tiers, smallest (tier 1) to largest. 1..5 tiers, indices contiguous from 1.</summary>
    public required IReadOnlyCollection<SaveOrganisationTierDto> Tiers { get; init; }
}
