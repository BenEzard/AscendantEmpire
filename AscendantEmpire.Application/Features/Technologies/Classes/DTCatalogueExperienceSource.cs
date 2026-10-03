using AscendantEmpire.FrontendContracts.Technologies.GetTechnologies;

namespace AscendantEmpire.Application.Features.Technologies.Classes;

public class DTCatalogueExperienceSource : ICatalogueExperienceSource
{
    public required int ExperienceTypeId { get; set; }

    public required string ExperienceTypeCode { get; set; }

    public required string ExperienceTypeDisplayName { get; set; }
}
