using AscendantEmpire.FrontendContracts.MapResources.GetMapResourceCatalogue;

namespace AscendantEmpire.Application.Features.Maps.MapResources.Classes;

public class DTMapResourceCatalogue : IMapResourceCatalogue
{
    public required IReadOnlyCollection<IMapResourceCategory> Categories { get; set; }
}

public class DTMapResourceCategory : IMapResourceCategory
{
    public required int Id { get; set; }

    public required string Code { get; set; }

    public required string DisplayName { get; set; }

    public required int SortOrder { get; set; }

    /// <remarks>
    /// Carries the handler's <c>MapResourceTypeDetail</c> records as they are: they already implement <see cref="IMapResourceTypeDetail"/>, and copying every field and
    /// child of a type into a parallel class would only add a place for the two to drift.
    /// </remarks>
    public required IReadOnlyCollection<IMapResourceTypeDetail> Types { get; set; }
}
