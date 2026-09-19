using AscendantEmpire.BackendContracts.MapResources.AddOrUpdateMapResourceType;
using AscendantEmpire.FrontendContracts.MapResources.AddOrUpdateMapResourceType;
using AscendantEmpire.FrontendContracts.MapResources.PutMapResourceCatalogue;

namespace AscendantEmpire.BackendContracts.MapResources.PutMapResourceCatalogue;

/// <inheritdoc cref="IPutMapResourceCatalogueDto" />
public record class PutMapResourceCatalogueDto : IPutMapResourceCatalogueDto
{
    // Concrete collections for model binding, exposed through the interface as their interface types - the same arrangement as PutCampaignMapStartPositionsDto.

    /// <inheritdoc cref="IPutMapResourceCatalogueDto.Categories" />
    public IReadOnlyCollection<AddOrUpdateMapResourceCategoryDto> Categories { get; init; } = [];

    /// <inheritdoc cref="IPutMapResourceCatalogueDto.Types" />
    public IReadOnlyCollection<AddOrUpdateMapResourceTypeDto> Types { get; init; } = [];

    IReadOnlyCollection<IAddOrUpdateMapResourceCategoryDto> IPutMapResourceCatalogueDto.Categories => Categories;

    IReadOnlyCollection<IAddOrUpdateMapResourceTypeDto> IPutMapResourceCatalogueDto.Types => Types;
}

/// <inheritdoc cref="IAddOrUpdateMapResourceCategoryDto" />
public record class AddOrUpdateMapResourceCategoryDto : IAddOrUpdateMapResourceCategoryDto
{
    /// <inheritdoc />
    public required string Code { get; init; }

    /// <inheritdoc />
    public required string DisplayName { get; init; }

    /// <inheritdoc />
    public int SortOrder { get; init; }
}
