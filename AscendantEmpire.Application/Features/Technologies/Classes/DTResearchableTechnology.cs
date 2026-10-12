using AscendantEmpire.Domain.Enums.Technologies;
using AscendantEmpire.FrontendContracts.Technologies.GetPlayerResearch;
using AscendantEmpire.FrontendContracts.Technologies.GetTechnologies;

namespace AscendantEmpire.Application.Features.Technologies.Classes;

public class DTResearchableTechnology : IResearchableTechnology
{
    public required int TechnologyId { get; set; }

    public required string Code { get; set; }

    public required string DisplayName { get; set; }

    public string? Description { get; set; }

    public required string IconPath { get; set; }

    public required TechnologyEra Era { get; set; }

    public required TechnologyStatus Status { get; set; }

    public int? DirectedRequired { get; set; }

    public int? ExperienceRequired { get; set; }

    public int? DiffusionRequired { get; set; }

    public required int DirectedPoints { get; set; }

    public required int ExperiencePoints { get; set; }

    public required int DiffusionPoints { get; set; }

    public required IReadOnlyCollection<ICatalogueAcquisitionCost> AcquisitionCosts { get; set; }

    public required IReadOnlyCollection<ICatalogueGrant> Grants { get; set; }

    public required IReadOnlyCollection<ICatalogueExperienceSource> ExperienceSources { get; set; }

    public required bool IsCurrentProject { get; set; }
}
