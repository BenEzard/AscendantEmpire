using AscendantEmpire.BackendContracts.MapResources.AddOrUpdateMapResourceType;
using AscendantEmpire.FrontendContracts.MapResources.GetMapResourceCatalogue;

namespace AscendantEmpire.BackendContracts.MapResources.GetMapResourceCatalogue;

/// <summary>
/// The whole MapResource catalogue, categories in order, each with its types in order.
/// </summary>
public record MapResourceCatalogue
{
    public required IReadOnlyCollection<MapResourceCategoryDetail> Categories { get; init; }
}

/// <summary>
/// A MapResourceCategory and the types in it.
/// </summary>
public record MapResourceCategoryDetail
{
    public required int Id { get; init; }

    public required string Code { get; init; }

    public required string DisplayName { get; init; }

    public required int SortOrder { get; init; }

    public required IReadOnlyCollection<MapResourceTypeDetail> Types { get; init; }
}

/// <inheritdoc cref="IMapResourceTypeDetail" />
/// <remarks>
/// Extends the add-or-update record rather than repeating it, so what the manager loads is, field for field, what it saves.
/// </remarks>
public record MapResourceTypeDetail : AddOrUpdateMapResourceTypeDto, IMapResourceTypeDetail
{
    /// <inheritdoc />
    public required int Id { get; init; }
}
