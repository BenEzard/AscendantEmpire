using AscendantEmpire.Domain.Enums.Technologies;

namespace AscendantEmpire.FrontendContracts.Technologies.GetTechnologies;

/// <summary>
/// One technology in the catalogue, with everything hanging off it.
/// </summary>
public interface ICatalogueTechnology
{
    int Id { get; }

    /// <summary>
    /// The technology's unique code, which prerequisites reference it by.
    /// </summary>
    string Code { get; }

    string DisplayName { get; }

    string? Description { get; }

    string IconPath { get; }

    TechnologyEra Era { get; }

    int SortOrder { get; }

    int? TreeColumn { get; }

    int? TreeRow { get; }

    /// <summary>
    /// Base directed-research points required, or null when this technology does not use the directed bucket.
    /// </summary>
    int? DirectedRequired { get; }

    /// <summary>
    /// Base experience points required, or null when this technology does not use the experience bucket.
    /// </summary>
    int? ExperienceRequired { get; }

    /// <summary>
    /// Base diffusion points required, or null when this technology does not use the diffusion bucket.
    /// </summary>
    int? DiffusionRequired { get; }

    IReadOnlyCollection<ICatalogueAcquisitionCost> AcquisitionCosts { get; }

    IReadOnlyCollection<ICataloguePrerequisite> Prerequisites { get; }

    IReadOnlyCollection<ICatalogueGrant> Grants { get; }

    IReadOnlyCollection<ICatalogueExperienceSource> ExperienceSources { get; }
}
