using AscendantEmpire.Domain.Enums.Technologies;
using AscendantEmpire.FrontendContracts.Technologies.GetTechnologies;

namespace AscendantEmpire.Application.Features.Technologies.Classes;

public class DTCatalogueGrant : ICatalogueGrant
{
    public required TechnologyGrantTargetType TargetType { get; set; }

    public required int TargetId { get; set; }

    public string? TargetCode { get; set; }

    public string? TargetDisplayName { get; set; }

    public required TechnologyGrantEffect GrantEffect { get; set; }
}
