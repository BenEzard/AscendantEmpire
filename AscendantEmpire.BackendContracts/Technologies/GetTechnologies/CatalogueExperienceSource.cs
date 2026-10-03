namespace AscendantEmpire.BackendContracts.Technologies.GetTechnologies;

/// <summary>
/// One kind of experience that fills a technology's experience bucket.
/// </summary>
/// <remarks>
/// Carries no quantity: how many points an action is worth is decided by the action, in proportion to what it did, so it cannot be authored here.
/// </remarks>
public record CatalogueExperienceSource
{
    public required int ExperienceTypeId { get; init; }

    public required string ExperienceTypeCode { get; init; }

    public required string ExperienceTypeDisplayName { get; init; }
}
