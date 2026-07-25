namespace AscendantEmpire.BackendContracts.Military.GetOrganisationTags;

/// <summary>
/// The response envelope for "get the MilitaryOrganisationalGroup tags" (the Organisation combo box feed).
/// </summary>
public record GetOrganisationTagsResult
{
    public required IReadOnlyCollection<OrganisationTagView> Tags { get; init; }
}
