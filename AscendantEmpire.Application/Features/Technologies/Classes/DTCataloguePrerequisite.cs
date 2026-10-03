using AscendantEmpire.Domain.Enums.Technologies;
using AscendantEmpire.FrontendContracts.Technologies.GetTechnologies;

namespace AscendantEmpire.Application.Features.Technologies.Classes;

public class DTCataloguePrerequisite : ICataloguePrerequisite
{
    public required int PrerequisiteTechnologyId { get; set; }

    public required string PrerequisiteTechnologyCode { get; set; }

    public required string PrerequisiteTechnologyDisplayName { get; set; }

    public required TechnologyPrerequisiteKind PrerequisiteKind { get; set; }
}
