using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Maps.Biomes.Handlers;
using AscendantEmpire.Application.Features.Maps.CampaignMaps.Classes;
using AscendantEmpire.Application.Features.Maps.MapGallery.Classes;
using AscendantEmpire.BackendContracts.Biomes;
using AscendantEmpire.BackendContracts.CampaignMaps;
using AscendantEmpire.BackendContracts.MapGallery.UpdateCampaignMapRivers;
using AscendantEmpire.Domain.Entities.Maps.CampaignMaps;
using AscendantEmpire.Domain.Enums.Maps;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.Maps.MapGallery.Handlers;

/// <summary>
/// Sets river edges on a gallery map, as the tiles that own them.
/// </summary>
/// <param name="CampaignMapGalleryIndexId">The gallery map to edit.</param>
/// <param name="Changes">The edges to set. Each names the tile that owns it and one of that tile's two edges.</param>
public record UpdateCampaignMapRiversRequest(
    int CampaignMapGalleryIndexId,
    IReadOnlyCollection<RiverEdgeChange> Changes
) : IRequest<ServiceResult<int>>;

/// <summary>
/// Applies a batch of river-edge changes to a gallery map, or refuses the whole batch.
/// </summary>
/// <remarks>
/// <para>
/// <b>All or nothing.</b> The authoring rules in <see cref="RiverNetworkRules"/> are properties of the river network rather than of a single edge, so
/// they can only be judged against the network the batch would produce. Landing half a batch could therefore create a river that none of its
/// individual changes would have been allowed to create, which is why there is no partial success here — unlike
/// <see cref="UpdateCampaignMapTilesHandler"/>, where each tile stands on its own.
/// </para>
/// <para>
/// Written through tracked entities and one <c>SaveChanges</c> rather than <c>ExecuteUpdate</c>: a batch is a handful of edges, the rules need the
/// surrounding network loaded anyway, and it keeps the handler's tests on the in-memory tier.
/// </para>
/// </remarks>
internal class UpdateCampaignMapRiversHandler(
    ILogger<UpdateCampaignMapRiversHandler> logger,
    AscendantEmpireContext context,
    IMediator mediator
) : IRequestHandler<UpdateCampaignMapRiversRequest, ServiceResult<int>>
{
    public async Task<ServiceResult<int>> Handle(UpdateCampaignMapRiversRequest request, CancellationToken cancellationToken)
    {
        try
        {
            ServiceResult<DTCampaignMapGalleryIndex> indexResult =
                await mediator.Send(new GetCampaignMapGalleryIndexRequest(request.CampaignMapGalleryIndexId), cancellationToken);

            if (indexResult.HasSucceeded == false || indexResult.SuccessValue is null)
            {
                logger.LogWarning(
                    "Failed to retrieve Campaign Map Gallery Index {CampaignMapGalleryIndexId} while setting river edges. Error: {Error}",
                    request.CampaignMapGalleryIndexId,
                    string.Join(Environment.NewLine, indexResult.Errors));

                return ServiceResult<int>.NotFound($"Map {request.CampaignMapGalleryIndexId} was not found.");
            }

            DTCampaignMapGalleryIndex index = indexResult.SuccessValue;

            if (index.CreationStatus != MapCreationStatus.Complete)
            {
                return ServiceResult<int>.Fail($"The map is {index.CreationStatus}, not Complete.");
            }

            var geometry = new CampaignMapGeometry(
                CampaignMapOffsetId: index.CampaignMapOffsetId,
                Width: index.Width,
                Height: index.Height,
                WrapsHorizontally: index.HorizontalWrap,
                WrapsVertically: index.VerticalWrap);

            // Coordinates are checked before anything is loaded, because an off-map coordinate would otherwise become a tile that quietly is not there.
            // Negative values are already refused by the validator; this is the other half of the same test.
            List<string> offMapCoordinates =
            [
                .. request.Changes
                    .Where(change => change.X >= geometry.Width || change.Y >= geometry.Height)
                    .Select(change => $"({change.X}, {change.Y})")
                    .Distinct()
            ];

            if (offMapCoordinates.Count > 0)
            {
                return ServiceResult<int>.Fail($"Off the {geometry.Width} x {geometry.Height} map: {string.Join(", ", offMapCoordinates)}.");
            }

            // Every edge that already carries a river, which is what the authoring rules are judged against once the batch has been folded in. Only the
            // tiles that actually have one are read; on any real map that is a small minority of them.
            Dictionary<RiverEdgeKey, RiverWidth> riverEdges = await LoadExistingRiverEdgesAsync(request.CampaignMapGalleryIndexId, cancellationToken);

            Dictionary<RiverEdgeKey, RiverWidth> originalWidths = new(riverEdges);

            ApplyChanges(request.Changes, riverEdges);

            ServiceResult<IReadOnlySet<Coord>> waterResult = await LoadWaterTilesAsync(request.CampaignMapGalleryIndexId, riverEdges.Keys, geometry, cancellationToken);

            if (waterResult.HasSucceeded == false || waterResult.SuccessValue is null)
            {
                return ServiceResult<int>.Fail(waterResult.Errors);
            }

            IReadOnlyCollection<RiverRuleViolation> violations = RiverNetworkRules.Validate(riverEdges, geometry, waterResult.SuccessValue);

            if (violations.Count > 0)
            {
                return Refuse(violations, originalWidths);
            }

            int edgesUpdated = await WriteChangesAsync(request, geometry, cancellationToken);

            return ServiceResult<int>.Succeed(edgesUpdated);
        }
        catch (Exception ex)
        {
            const string error = "The river edges could not be saved.";
            logger.LogError(ex, error + " CampaignMapGalleryIndexId: {CampaignMapGalleryIndexId}", request.CampaignMapGalleryIndexId);
            return ServiceResult<int>.InternalServerError(error);
        }
    }

    /// <summary>
    /// Every edge on the map that currently carries a river, keyed by the tile that owns it.
    /// </summary>
    private async Task<Dictionary<RiverEdgeKey, RiverWidth>> LoadExistingRiverEdgesAsync(int campaignMapGalleryIndexId, CancellationToken cancellationToken)
    {
        var tilesWithRivers = await context.CampaignMapGallery
            .AsNoTracking()
            .Where(tile => tile.CampaignMapGalleryIndexId == campaignMapGalleryIndexId)
            .Where(tile => tile.SouthRiver != RiverWidth.None || tile.EastRiver != RiverWidth.None)
            .Select(tile => new { tile.XCoord, tile.YCoord, tile.SouthRiver, tile.EastRiver })
            .ToListAsync(cancellationToken);

        Dictionary<RiverEdgeKey, RiverWidth> riverEdges = [];

        foreach (var tile in tilesWithRivers)
        {
            if (tile.SouthRiver != RiverWidth.None)
            {
                riverEdges[new RiverEdgeKey(tile.XCoord, tile.YCoord, RiverEdgeSide.South)] = tile.SouthRiver;
            }

            if (tile.EastRiver != RiverWidth.None)
            {
                riverEdges[new RiverEdgeKey(tile.XCoord, tile.YCoord, RiverEdgeSide.East)] = tile.EastRiver;
            }
        }

        return riverEdges;
    }

    /// <summary>
    /// Folds the requested changes into the edge set, so the rules see the network the save would produce rather than the one on disk.
    /// </summary>
    /// <remarks>
    /// A change to <see cref="RiverWidth.None"/> removes the edge entirely, because the rules take an absent edge to mean one with no river on it.
    /// </remarks>
    private static void ApplyChanges(IReadOnlyCollection<RiverEdgeChange> changes, Dictionary<RiverEdgeKey, RiverWidth> riverEdges)
    {
        foreach (RiverEdgeChange change in changes)
        {
            var key = new RiverEdgeKey(change.X, change.Y, change.Edge);

            if (change.Width == RiverWidth.None)
            {
                riverEdges.Remove(key);
            }
            else
            {
                riverEdges[key] = change.Width;
            }
        }
    }

    /// <summary>
    /// Which of the tiles around the network's river edges are water, which is what decides where the rivers meet the sea.
    /// </summary>
    /// <remarks>
    /// Only the tiles <see cref="RiverNetworkRules.GetTilesToCheckForWater"/> asks about are loaded, so this stays proportional to the rivers on the
    /// map rather than to its size. Whether a Biome counts as water is the author's own <c>IsWater</c> flag, read live, so a Biome edited in the Biome
    /// Manager changes what may be a river mouth without anything here needing to know the Biomes by name.
    /// </remarks>
    private async Task<ServiceResult<IReadOnlySet<Coord>>> LoadWaterTilesAsync(
        int campaignMapGalleryIndexId,
        IEnumerable<RiverEdgeKey> riverEdges,
        CampaignMapGeometry geometry,
        CancellationToken cancellationToken)
    {
        IReadOnlyCollection<Coord> coordsAroundRivers = RiverNetworkRules.GetTilesToCheckForWater(riverEdges, geometry);

        if (coordsAroundRivers.Count == 0)
        {
            return ServiceResult<IReadOnlySet<Coord>>.Succeed(new HashSet<Coord>());
        }

        ServiceResult<IReadOnlyCollection<CampaignMapBiome>> biomesResult = await mediator.Send(new GetCampaignMapBiomesRequest(), cancellationToken);

        if (biomesResult.HasSucceeded == false || biomesResult.SuccessValue is null)
        {
            logger.LogWarning("Failed to retrieve Campaign Map Biomes while setting river edges. Error: {Error}", string.Join(Environment.NewLine, biomesResult.Errors));
            return ServiceResult<IReadOnlySet<Coord>>.Fail("The Biome catalogue could not be read.");
        }

        HashSet<BiomeType> waterBiomes = [.. biomesResult.SuccessValue.Where(biome => biome.IsWater).Select(biome => biome.BiomeType)];

        int[] dbIds = [.. coordsAroundRivers.Select(coord => coord.GetDbId(geometry.CampaignMapOffsetId, geometry.Width))];

        var tiles = await context.CampaignMapGallery
            .AsNoTracking()
            .Where(tile => tile.CampaignMapGalleryIndexId == campaignMapGalleryIndexId)
            .Where(tile => dbIds.Contains(tile.CampaignMapGalleryId))
            .Select(tile => new { tile.XCoord, tile.YCoord, tile.BiomeTypeId })
            .ToListAsync(cancellationToken);

        IReadOnlySet<Coord> waterTiles = tiles
            .Where(tile => waterBiomes.Contains(tile.BiomeTypeId))
            .Select(tile => new Coord(tile.XCoord, tile.YCoord))
            .ToHashSet();

        return ServiceResult<IReadOnlySet<Coord>>.Succeed(waterTiles);
    }

    /// <summary>
    /// Writes the batch to the tiles that own its edges.
    /// </summary>
    /// <returns>How many edges actually changed value. An edge already at the requested width is not counted, and costs no write.</returns>
    private async Task<int> WriteChangesAsync(UpdateCampaignMapRiversRequest request, CampaignMapGeometry geometry, CancellationToken cancellationToken)
    {
        int[] dbIds =
        [
            .. request.Changes
                .Select(change => new Coord(change.X, change.Y).GetDbId(geometry.CampaignMapOffsetId, geometry.Width))
                .Distinct()
        ];

        List<CampaignMapGallery> tiles = await context.CampaignMapGallery
            .Where(tile => tile.CampaignMapGalleryIndexId == request.CampaignMapGalleryIndexId)
            .Where(tile => dbIds.Contains(tile.CampaignMapGalleryId))
            .ToListAsync(cancellationToken);

        Dictionary<Coord, CampaignMapGallery> tilesByCoord = tiles.ToDictionary(tile => new Coord(tile.XCoord, tile.YCoord));

        int edgesUpdated = 0;

        foreach (RiverEdgeChange change in request.Changes)
        {
            if (!tilesByCoord.TryGetValue(new Coord(change.X, change.Y), out CampaignMapGallery? tile))
            {
                // The coordinate is on the map but the row is missing, which means the map itself is incomplete rather than the request wrong.
                logger.LogWarning(
                    "No tile at ({X}, {Y}) on Campaign Map Gallery {CampaignMapGalleryIndexId} while setting river edges.",
                    change.X, change.Y, request.CampaignMapGalleryIndexId);
                continue;
            }

            if (change.Edge == RiverEdgeSide.South)
            {
                if (tile.SouthRiver == change.Width)
                {
                    continue;
                }

                tile.SouthRiver = change.Width;
            }
            else
            {
                if (tile.EastRiver == change.Width)
                {
                    continue;
                }

                tile.EastRiver = change.Width;
            }

            edgesUpdated++;
        }

        await context.SaveChangesAsync(cancellationToken);

        return edgesUpdated;
    }

    /// <summary>
    /// Turns the broken rules into a refusal: the reasons for the author to read, and the offending edges for the editor to point at.
    /// </summary>
    /// <remarks>
    /// Each refused edge carries the width it still holds, taken from the map as it was before the batch was folded in, because nothing has been
    /// written and the client's optimistic copy has to go back to exactly that.
    /// </remarks>
    private static ServiceResult<int> Refuse(IReadOnlyCollection<RiverRuleViolation> violations, IReadOnlyDictionary<RiverEdgeKey, RiverWidth> originalWidths)
    {
        RiverEdgeUpdateFail[] refusedEdges =
        [
            .. violations.Select(violation => new RiverEdgeUpdateFail
            {
                X = violation.Edge.X,
                Y = violation.Edge.Y,
                Edge = violation.Edge.Side,
                OriginalWidth = originalWidths.TryGetValue(violation.Edge, out RiverWidth width) ? width : RiverWidth.None,
                ErrorMessage = violation.Reason
            })
        ];

        // Constructed rather than built through a factory because there is no Fail overload carrying detail, and a Conflict would be the wrong status:
        // nothing here clashes with another writer, the edit simply is not a legal river.
        return new ServiceResult<int>
        {
            StatusCode = ServiceResultStatus.GenericError,
            Errors = [.. refusedEdges.Select(edge => edge.ErrorMessage).Distinct()],
            ErrorDetail = new RiverEdgeUpdateFailCollection { RefusedEdges = refusedEdges }
        };
    }
}
