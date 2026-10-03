using AscendantEmpire.Domain.Enums.Technologies;
using AscendantEmpire.FrontendContracts.Technologies.GetTechnologies;

namespace AscendantEmpire.Application.Features.Technologies.Classes;

public class DTCatalogueTechnology : ICatalogueTechnology
{
    public required int Id { get; set; }

    public required string Code { get; set; }

    public required string DisplayName { get; set; }

    public string? Description { get; set; }

    public required string IconPath { get; set; }

    public required TechnologyEra Era { get; set; }

    public required int SortOrder { get; set; }

    public int? TreeColumn { get; set; }

    public int? TreeRow { get; set; }

    public int? DirectedRequired { get; set; }

    public int? ExperienceRequired { get; set; }

    public int? DiffusionRequired { get; set; }

    public required IReadOnlyCollection<ICatalogueAcquisitionCost> AcquisitionCosts { get; set; }

    public required IReadOnlyCollection<ICataloguePrerequisite> Prerequisites { get; set; }

    public required IReadOnlyCollection<ICatalogueGrant> Grants { get; set; }

    public required IReadOnlyCollection<ICatalogueExperienceSource> ExperienceSources { get; set; }
}
