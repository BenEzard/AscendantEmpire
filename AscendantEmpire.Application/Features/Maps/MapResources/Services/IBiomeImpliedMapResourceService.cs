using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Features.Maps.MapResources.Classes;
using AscendantEmpire.Domain.Entities.Maps.CampaignMaps;
using AscendantEmpire.Domain.Enums.Maps;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.Maps.MapResources.Services;

/// <summary>
/// Keeps the MapResources a gallery tile's Biome implies - Timber on a Forest, Jungle or Woodland tile - in step with the tile's terrain.
/// </summary>
/// <remarks>
/// <para>
/// Two callers, two ways of writing. Generating a map places for every tile of the map at once, which is a bulk insert. The map editor's Biome brush places
/// and removes for a handful of tiles, inside the same transaction as the tile write, and has to tell the author which tiles held out. So the plan is read
/// separately from being applied: see <see cref="PlanPaintAsync"/> and <see cref="ApplyPaintPlan"/>.
/// </para>
/// <para>
/// Which Biomes imply what is read from the MapResourceType's habitats, so a habitat added in the MapResource Type Manager takes effect with no code change.
/// Nothing here fails a caller: a map's tiles are correct with or without their implied MapResources. See
/// <c>docs/design/campaign-map/map-resources.md</c>.
/// </para>
/// </remarks>
internal interface IBiomeImpliedMapResourceService
{
    /// <summary>
    /// Places the MapResources their Biomes imply on every tile of one freshly generated map.
    /// </summary>
    /// <remarks>
    /// Advisory: every failure is returned rather than thrown, because a generated map's tiles are already written and correct without them, and an author
    /// can place what is missing by hand.
    /// </remarks>
    /// <returns>The number of placements created.</returns>
    Task<ServiceResult<int>> PlaceForGeneratedMapAsync(int campaignMapGalleryIndexId, CancellationToken cancellationToken);

    /// <summary>
    /// What a paint would do to the painted tiles' implied MapResources. Reads only; writes nothing.
    /// </summary>
    /// <param name="paintedTiles">The tiles about to be painted, as they are now.</param>
    /// <param name="paintedBiome">The Biome being painted, or <see langword="null"/> when only the Geography changes.</param>
    /// <param name="paintedGeography">The Geography being painted, or <see langword="null"/> when only the Biome changes.</param>
    /// <remarks>
    /// Decides against the terrain each tile is becoming, not the terrain it has, so the caller can act on the answer before it writes anything.
    /// </remarks>
    Task<ServiceResult<BiomeImpliedResourcePlan>> PlanPaintAsync(
        IReadOnlyCollection<CampaignMapGallery> paintedTiles,
        BiomeType? paintedBiome,
        CampaignMapGeographyType? paintedGeography,
        CancellationToken cancellationToken);

    /// <summary>
    /// Adds and removes a plan's placements, leaving the save to the caller.
    /// </summary>
    /// <remarks>
    /// Deliberately does not save or commit. The caller writes the tiles and the placements in one transaction, so a half-applied paint cannot leave a tile
    /// holding a MapResource its Biome does not imply, and must therefore own both.
    /// </remarks>
    Task ApplyPaintPlanAsync(BiomeImpliedResourcePlan plan, CancellationToken cancellationToken);
}
