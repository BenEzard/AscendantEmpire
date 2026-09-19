namespace AscendantEmpire.FrontendContracts.MapResources.GalleryPlacements;

/// <summary>
/// Every MapResource placed on one gallery map.
/// </summary>
public interface IGalleryMapResourceCollection
{
    int CampaignMapGalleryIndexId { get; }

    IReadOnlyCollection<IGalleryMapResource> MapResources { get; }
}

/// <summary>
/// A MapResource placed on a gallery map tile, which every game created from the map starts with.
/// </summary>
public interface IGalleryMapResource
{
    int Id { get; }

    int XCoord { get; }

    int YCoord { get; }

    /// <summary>
    /// Which of the tile's two places it takes: 1 or 2.
    /// </summary>
    byte Slot { get; }

    int MapResourceTypeId { get; }

    /// <summary>
    /// The starting quantity the map editor chose, as a whole number, or <see langword="null"/> to roll it when a game is created. Only for a type that holds a
    /// single quantity.
    /// </summary>
    int? InitialQuantity { get; }

    /// <summary>
    /// The Distribution scenario the map editor chose, by its number within the type, or <see langword="null"/> to pick one when a game is created. Only for a
    /// Distribution.
    /// </summary>
    int? DistributionScenarioNumber { get; }
}

/// <summary>
/// A MapResource to place on a gallery map tile. It takes the tile's first free slot.
/// </summary>
public interface IPlaceGalleryMapResourceDto
{
    int XCoord { get; }

    int YCoord { get; }

    int MapResourceTypeId { get; }
}

/// <summary>
/// The specifics of a placed MapResource the map editor can set. Each left empty is decided when a game is created.
/// </summary>
public interface IUpdateGalleryMapResourceDto
{
    /// <inheritdoc cref="IGalleryMapResource.InitialQuantity" />
    int? InitialQuantity { get; }

    /// <inheritdoc cref="IGalleryMapResource.DistributionScenarioNumber" />
    int? DistributionScenarioNumber { get; }
}
