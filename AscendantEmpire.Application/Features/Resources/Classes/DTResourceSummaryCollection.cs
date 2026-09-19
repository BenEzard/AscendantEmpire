using AscendantEmpire.FrontendContracts.Resources.GetResources;

namespace AscendantEmpire.Application.Features.Resources.Classes;

public class DTResourceSummaryCollection : IResourceSummaryCollection
{
    public required IReadOnlyCollection<IResourceSummary> Resources { get; set; }
}

public class DTResourceSummary : IResourceSummary
{
    public required int Id { get; set; }

    public required string Code { get; set; }

    public required string DisplayName { get; set; }

    public required string MetaCategoryDisplayName { get; set; }
}
