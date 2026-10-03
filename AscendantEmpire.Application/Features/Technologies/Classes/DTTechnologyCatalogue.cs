using AscendantEmpire.FrontendContracts.Technologies.GetTechnologies;

namespace AscendantEmpire.Application.Features.Technologies.Classes;

public class DTTechnologyCatalogue : ITechnologyCatalogue
{
    public required IReadOnlyCollection<ICatalogueTechnology> Technologies { get; set; }
}
