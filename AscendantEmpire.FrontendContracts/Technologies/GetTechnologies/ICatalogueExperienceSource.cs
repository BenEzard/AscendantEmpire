namespace AscendantEmpire.FrontendContracts.Technologies.GetTechnologies;

/// <summary>
/// One kind of experience that fills a technology's experience bucket.
/// </summary>
public interface ICatalogueExperienceSource
{
    int ExperienceTypeId { get; }

    string ExperienceTypeCode { get; }

    string ExperienceTypeDisplayName { get; }
}
