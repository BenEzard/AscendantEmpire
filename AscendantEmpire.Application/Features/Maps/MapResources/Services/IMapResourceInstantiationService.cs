using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.Maps.MapResources.Services;

/// <summary>
/// Creates the MapResource instances one Game starts with, from the placements on the map it was created from.
/// </summary>
internal interface IMapResourceInstantiationService
{
    /// <summary>
    /// Turns every placement on the Game's map into an instance on the Game's own tile, rolling whatever the map editor left unset.
    /// </summary>
    /// <param name="gameId">The Game the instances belong to.</param>
    /// <param name="campaignMapGalleryIndexId">The gallery map the Game was created from, whose placements are read.</param>
    /// <param name="gameSeed">The Game's <c>GenerationSeed</c>, which every roll derives from.</param>
    /// <param name="campaignMapOffsetId">The Game's world-map id for the tile at (0, 0), assigned when its campaign map was generated.</param>
    /// <returns>How many instances were created. A placement that cannot become one is logged and skipped, not failed.</returns>
    Task<ServiceResult<int>> InstantiateForGameAsync(
        int gameId,
        int campaignMapGalleryIndexId,
        long gameSeed,
        int campaignMapOffsetId,
        int mapWidth,
        CancellationToken cancellationToken);
}
