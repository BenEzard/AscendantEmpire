using AscendantEmpire.FrontendContracts.MapResources.GalleryPlacements;

namespace AscendantEmpire.BackendContracts.MapResources.GalleryPlacements;

/// <inheritdoc cref="IGalleryMapResource" />
public record GalleryMapResource : IGalleryMapResource
{
    /// <inheritdoc />
    public required int Id { get; init; }

    /// <inheritdoc />
    public required int XCoord { get; init; }

    /// <inheritdoc />
    public required int YCoord { get; init; }

    /// <inheritdoc />
    public required byte Slot { get; init; }

    /// <inheritdoc />
    public required int MapResourceTypeId { get; init; }

    /// <inheritdoc />
    public int? InitialQuantity { get; init; }

    /// <inheritdoc />
    public int? DistributionScenarioNumber { get; init; }
}

/// <inheritdoc cref="IPlaceGalleryMapResourceDto" />
public record class PlaceGalleryMapResourceDto : IPlaceGalleryMapResourceDto
{
    /// <inheritdoc />
    public required int XCoord { get; init; }

    /// <inheritdoc />
    public required int YCoord { get; init; }

    /// <inheritdoc />
    public required int MapResourceTypeId { get; init; }
}

/// <inheritdoc cref="IUpdateGalleryMapResourceDto" />
public record class UpdateGalleryMapResourceDto : IUpdateGalleryMapResourceDto
{
    /// <inheritdoc />
    public int? InitialQuantity { get; init; }

    /// <inheritdoc />
    public int? DistributionScenarioNumber { get; init; }
}
