using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Maps.Biomes.Handlers;
using AscendantEmpire.Application.Features.Maps.CampaignMaps.Services;
using AscendantEmpire.Application.Features.Maps.Geographies.Handlers;
using AscendantEmpire.Application.Features.Maps.MapGallery.Classes;
using AscendantEmpire.Application.Features.Maps.MapResources.Classes;
using AscendantEmpire.Application.Features.Maps.MapResources.Services;
using AscendantEmpire.Application.Helpers;
using AscendantEmpire.BackendContracts.Biomes;
using AscendantEmpire.BackendContracts.CampaignMaps;
using AscendantEmpire.BackendContracts.Geographies;
using AscendantEmpire.BackendContracts.MapGallery.UpdateCampaignMapTiles;
using AscendantEmpire.Domain.Entities.Maps.CampaignMaps;
using AscendantEmpire.Domain.Enums.Maps;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.Maps.MapGallery.Handlers;

public record UpdateCampaignMapTilesRequest(
    int CampaignMapGalleryIndexId,
    IReadOnlyCollection<Coord> Coordinates,
    BiomeType? BiomeType,
    CampaignMapGeographyType? CampaignMapGeographyType
) : IRequest<ServiceResult<int, IReadOnlyCollection<TileUpdateFail>?>>;

internal class UpdateCampaignMapTilesHandler(
    ILogger<UpdateCampaignMapTilesHandler> logger,
    AscendantEmpireContext context,
    IMediator mediator,
    IBiomeImpliedMapResourceService impliedMapResources
) : IRequestHandler<UpdateCampaignMapTilesRequest, ServiceResult<int, IReadOnlyCollection<TileUpdateFail>?>>
{
    public async Task<ServiceResult<int, IReadOnlyCollection<TileUpdateFail>?>> Handle(UpdateCampaignMapTilesRequest request, CancellationToken cancellationToken)
    {
        var failures = new List<TileUpdateFail>();

        // If Biome is Coastal or Ocean and Geography is not provided, default the Geography.
        CampaignMapGeographyType? mutatedGeographyType = ForceGeographyUpdate(request.BiomeType, request.CampaignMapGeographyType);

        // Validate request + load required data.
        ServiceResult<DTLoadedData> validationResult = await ValidateRequestAsync(
            coordinates: request.Coordinates,
            campaignMapGalleryId: request.CampaignMapGalleryIndexId,
            biomeType: request.BiomeType,
            geographyType: mutatedGeographyType,
            cancellationToken: cancellationToken);

        if (validationResult.HasSucceeded == false || validationResult.SuccessValue is null)
        {
            return ServiceResult<int, IReadOnlyCollection<TileUpdateFail>?>.Fail(validationResult.Errors);
        }

        DTCampaignMapGalleryIndex index = validationResult.SuccessValue.Index;

        // A paint that would strand an authored start position on water, untraversable ground or impassable geography is refused outright rather
        // than silently invalidating it. Checked here so it covers all three write paths below.
        ServiceResultBase startPositionCheck = await ValidateStartPositionsRemainValidAsync(
            request: request,
            loaded: validationResult.SuccessValue,
            mutatedGeographyType: mutatedGeographyType,
            cancellationToken: cancellationToken);

        if (startPositionCheck.HasSucceeded == false)
        {
            return ServiceResult<int, IReadOnlyCollection<TileUpdateFail>?>.Fail(startPositionCheck.Errors);
        }

        EnhancedCoord[] enhancedCoordinates = MapMethods.EnhanceCoordinates(index.CampaignMapOffsetId, index.Width, request.Coordinates);

        // Deduplicated because a brush stroke can name the same coordinate twice, and every id below is counted as well as matched.
        int[] dbIds = [.. enhancedCoordinates.Select(ec => ec.DbId).Distinct()];

        // Loaded for every branch, not just the ones needing per-tile validation: the MapResources a Biome implies are decided from each tile's current
        // terrain and the terrain it is becoming.
        //
        // Untracked deliberately. The tiles are written below through ExecuteUpdate, which goes straight to the database and leaves the change tracker
        // untouched - so a tracked tile read again in the same context would still report the terrain it had before an earlier paint in that context.
        List<CampaignMapGallery> paintedTiles = await context.CampaignMapGallery
            .AsNoTracking()
            .Where(t => t.CampaignMapGalleryIndexId == request.CampaignMapGalleryIndexId)
            .Where(t => dbIds.Contains(t.CampaignMapGalleryId))
            .ToListAsync(cancellationToken);

        // Which of the painted tiles may take the change. Only a single-property paint needs per-tile validation: when both Biome and Geography are
        // given, the combination was already checked for every tile at once.
        int[] candidateIds = SelectCandidateTiles(
            request: request,
            loaded: validationResult.SuccessValue,
            mutatedGeographyType: mutatedGeographyType,
            paintedTiles: paintedTiles,
            failures: failures);

        // What the paint would do to those tiles' implied MapResources, worked out before anything is written so a tile whose placement holds out can be
        // left alone rather than painted and then reported.
        BiomeImpliedResourcePlan impliedPlan = await PlanImpliedMapResourcesAsync(
            request: request,
            mutatedGeographyType: mutatedGeographyType,
            paintedTiles: paintedTiles,
            candidateIds: candidateIds,
            cancellationToken: cancellationToken);

        HashSet<int> tilesHoldingOut = [.. impliedPlan.Refusals
            .Where(refusal => refusal.Reason == BiomeImpliedResourceRefusal.AuthorIntentBlocksRemoval)
            .Select(refusal => refusal.CampaignMapGalleryId)];

        int[] idsToWrite = [.. candidateIds.Where(id => tilesHoldingOut.Contains(id) == false)];

        if (tilesHoldingOut.Count > 0)
        {
            failures.Add(ToContractFail(BuildHoldOutFailure(paintedTiles, tilesHoldingOut)));
        }

        int tilesUpdated = await WritePaintAsync(
            request: request,
            mutatedGeographyType: mutatedGeographyType,
            idsToWrite: idsToWrite,
            impliedPlan: impliedPlan,
            cancellationToken: cancellationToken);

        // A clean paint answers 200, a paint that left tiles behind answers 206. Branches used to disagree on this, always reporting partial success even
        // when nothing failed.
        return failures.Count == 0
            ? ServiceResult<int, IReadOnlyCollection<TileUpdateFail>?>.Succeed(tilesUpdated)
            : ServiceResult<int, IReadOnlyCollection<TileUpdateFail>?>.PartialSuccess(tilesUpdated, failures);
    }

    /// <summary>
    /// Picks the painted tiles the change may be applied to, adding a failure group for any the terrain rules turn down.
    /// </summary>
    /// <remarks>
    /// A paint giving both Biome and Geography needs no per-tile check: the pair was validated once for the whole request, so every painted tile qualifies.
    /// A paint giving only one of the two has to be judged against each tile's existing other half, which is what can turn individual tiles down.
    /// </remarks>
    private static int[] SelectCandidateTiles(
        UpdateCampaignMapTilesRequest request,
        DTLoadedData loaded,
        CampaignMapGeographyType? mutatedGeographyType,
        List<CampaignMapGallery> paintedTiles,
        List<TileUpdateFail> failures)
    {
        if (request.BiomeType is not null && mutatedGeographyType is not null)
        {
            return [.. paintedTiles.Select(tile => tile.CampaignMapGalleryId)];
        }

        if (request.BiomeType is not null)
        {
            List<IGrouping<CampaignMapGeographyType, CampaignMapGallery>> tilesByGeography =
                [.. paintedTiles.GroupBy(tile => tile.CampaignMapGeographyTypeId)];

            CampaignMapBiome requestBiome = loaded.Biomes.First(biome => biome.BiomeType == request.BiomeType);

            ServiceResult<int[]?, IReadOnlyCollection<DTTileUpdateFail>?> validateBiomeResult =
                ValidateBiomeChange(requestBiome, tilesByGeography, loaded.Geographies);

            if (validateBiomeResult.ErrorValue?.Count > 0)
            {
                failures.AddRange(validateBiomeResult.ErrorValue.Select(ToContractFail));
            }

            return validateBiomeResult.SuccessValue ?? [];
        }

        if (request.CampaignMapGeographyType is not null)
        {
            List<IGrouping<BiomeType, CampaignMapGallery>> tilesByBiome = [.. paintedTiles.GroupBy(tile => tile.BiomeTypeId)];

            CampaignMapGeography requestGeography =
                loaded.Geographies.First(geography => geography.CampaignMapGeographyType == request.CampaignMapGeographyType);

            ServiceResult<int[]?, IReadOnlyCollection<DTTileUpdateFail>?> validateGeographyResult =
                ValidateGeographyChange(requestGeography, tilesByBiome, loaded.Biomes);

            if (validateGeographyResult.ErrorValue?.Count > 0)
            {
                failures.AddRange(validateGeographyResult.ErrorValue.Select(ToContractFail));
            }

            return validateGeographyResult.SuccessValue ?? [];
        }

        // Unreachable: ValidateRequestAsync refuses a request naming neither. Returning nothing rather than throwing keeps a future caller honest.
        return [];
    }

    /// <summary>
    /// Works out what the paint would do to the candidate tiles' implied MapResources, and logs the tiles that are simply too full to gain one.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A failure to read the plan does not block the paint: the tiles are the author's edit, and the MapResources their Biomes imply are a convenience on
    /// top of it. The paint goes ahead against an empty plan.
    /// </para>
    /// <para>
    /// Runs for a Geography-only paint too. Timber's habitats are all Biome-wide today, so such a paint changes nothing and the plan comes back empty - but a
    /// habitat naming a Geography is exactly what <c>MapResourceHabitat.CampaignMapGeographyTypeId</c> exists for, and one added in the MapResource Type
    /// Manager has to be honoured here without a code change.
    /// </para>
    /// </remarks>
    private async Task<BiomeImpliedResourcePlan> PlanImpliedMapResourcesAsync(
        UpdateCampaignMapTilesRequest request,
        CampaignMapGeographyType? mutatedGeographyType,
        List<CampaignMapGallery> paintedTiles,
        int[] candidateIds,
        CancellationToken cancellationToken)
    {
        List<CampaignMapGallery> candidateTiles = [.. paintedTiles.Where(tile => candidateIds.Contains(tile.CampaignMapGalleryId))];

        ServiceResult<BiomeImpliedResourcePlan> planResult = await impliedMapResources.PlanPaintAsync(
            paintedTiles: candidateTiles,
            paintedBiome: request.BiomeType,
            paintedGeography: mutatedGeographyType,
            cancellationToken: cancellationToken);

        if (planResult.HasSucceeded == false || planResult.SuccessValue is null)
        {
            logger.LogWarning(
                "Could not work out the MapResources the painted tiles' Biomes imply for Campaign Map Gallery ID {CampaignMapGalleryId}; the paint went ahead "
                    + "without them. Errors: {Errors}",
                request.CampaignMapGalleryIndexId, string.Join(Environment.NewLine, planResult.Errors));

            return BiomeImpliedResourcePlan.None;
        }

        BiomeImpliedResourcePlan plan = planResult.SuccessValue;

        int fullTiles = plan.Refusals.Count(refusal => refusal.Reason == BiomeImpliedResourceRefusal.NoFreeSlot);

        if (fullTiles > 0)
        {
            // Not a failure: those tiles are painted, and only an automatic convenience did not happen on them. Reporting them through Failures would make the
            // editor revert tiles the database did change. If the editor ever wants to show "painted, but no Timber", that belongs on a new advisory
            // collection on the response, not here.
            logger.LogInformation(
                "{FullTileCount} painted tiles of Campaign Map Gallery ID {CampaignMapGalleryId} gained no Biome-implied MapResource because both of their "
                    + "slots were already taken.",
                fullTiles, request.CampaignMapGalleryIndexId);
        }

        return plan;
    }

    /// <summary>
    /// Describes the tiles left unpainted because the MapResource on them was authored in the map editor.
    /// </summary>
    /// <remarks>
    /// One group, because the reason is the same for all of them. Each tile carries its real <c>CampaignMapGalleryId</c> and the terrain it still has, which
    /// is what lets the editor put it back as it was - and it genuinely still has it, because these are the tiles that were not written.
    /// </remarks>
    private static DTTileUpdateFail BuildHoldOutFailure(List<CampaignMapGallery> paintedTiles, HashSet<int> tilesHoldingOut)
    {
        DTTileUpdateFailTile[] heldOutTiles = [.. paintedTiles
            .Where(tile => tilesHoldingOut.Contains(tile.CampaignMapGalleryId))
            .OrderBy(tile => tile.YCoord)
            .ThenBy(tile => tile.XCoord)
            .Select(tile => new DTTileUpdateFailTile
            {
                Coord = new Coord(tile.XCoord, tile.YCoord),
                DbId = tile.CampaignMapGalleryId,
                OriginalBiomeType = tile.BiomeTypeId,
                OriginalGeographyType = tile.CampaignMapGeographyTypeId
            })];

        return new DTTileUpdateFail
        {
            ErrorMessage = "These tiles keep the MapResource their Biome implies, because the map editor pinned a scenario or set a quantity on it, so their "
                + "Biome was left unchanged. Remove that MapResource from them first, then paint.",
            FailedTiles = heldOutTiles
        };
    }

    /// <summary>
    /// Writes the painted tiles and their implied MapResources together.
    /// </summary>
    /// <remarks>
    /// One transaction, because this is now two writes: the tiles go through <c>ExecuteUpdateAsync</c>, which takes effect immediately, and the placements go
    /// through the change tracker. Failing between the two would leave a tile holding a MapResource its new Biome does not imply, or missing one its new Biome
    /// does, with nothing to put it right. All the reading and deciding is done by this point, so the transaction only spans the writes.
    /// </remarks>
    /// <returns>The number of tiles written.</returns>
    private async Task<int> WritePaintAsync(
        UpdateCampaignMapTilesRequest request,
        CampaignMapGeographyType? mutatedGeographyType,
        int[] idsToWrite,
        BiomeImpliedResourcePlan impliedPlan,
        CancellationToken cancellationToken)
    {
        if (idsToWrite.Length == 0 && impliedPlan.IsEmpty)
        {
            return 0;
        }

        await using IDbContextTransaction transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        int tilesUpdated = 0;

        if (idsToWrite.Length > 0)
        {
            IQueryable<CampaignMapGallery> tilesToWrite = context.CampaignMapGallery
                .Where(t => t.CampaignMapGalleryIndexId == request.CampaignMapGalleryIndexId)
                .Where(t => idsToWrite.Contains(t.CampaignMapGalleryId));

            if (request.BiomeType is not null && mutatedGeographyType is not null)
            {
                tilesUpdated = await tilesToWrite.ExecuteUpdateAsync(s => s
                        .SetProperty(t => t.BiomeTypeId, request.BiomeType.Value)
                        .SetProperty(t => t.CampaignMapGeographyTypeId, mutatedGeographyType.Value),
                    cancellationToken);
            }
            else if (request.BiomeType is not null)
            {
                tilesUpdated = await tilesToWrite.ExecuteUpdateAsync(s => s.SetProperty(t => t.BiomeTypeId, request.BiomeType.Value), cancellationToken);
            }
            else if (mutatedGeographyType is not null)
            {
                tilesUpdated = await tilesToWrite.ExecuteUpdateAsync(
                    s => s.SetProperty(t => t.CampaignMapGeographyTypeId, mutatedGeographyType.Value),
                    cancellationToken);
            }
        }

        await impliedMapResources.ApplyPaintPlanAsync(impliedPlan, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return tilesUpdated;
    }

    /// <summary>
    /// Maps a <see cref="DTTileUpdateFail"/> data transfer object to a <see cref="TileUpdateFail"/> contract model, aggregating failed tile information for use in the application layer.
    /// </summary>
    /// <param name="tile">The <see cref="DTTileUpdateFail"/> instance containing details about failed tile updates to be converted.</param>
    /// <returns>A <see cref="TileUpdateFail"/> object representing the failed tile update information, including error messages and details about each failed tile.</returns>
    private static TileUpdateFail ToContractFail(DTTileUpdateFail tile)
    {
        TileUpdateFailTile[] tiles = [.. tile.FailedTiles.Select(t => new TileUpdateFailTile
        {
            Coord = t.Coord,
            DbId = t.DbId,
            OriginalBiomeType = t.OriginalBiomeType,
            OriginalGeographyType = t.OriginalGeographyType
        })];

        return new TileUpdateFail
        {
            ErrorMessage = tile.ErrorMessage,
            FailedTiles = tiles
        };
    }

    private sealed class DTLoadedData
    {
        public required DTCampaignMapGalleryIndex Index { get; set; }
        public required IReadOnlyCollection<CampaignMapBiome> Biomes { get; set; }
        public required IReadOnlyCollection<CampaignMapGeography> Geographies { get; set; }
    }

    /// <summary>
    /// Validates the request parameters for updating Campaign Map Tiles and ensures that the specified biome and geography types are valid as a combination.
    /// </summary>
    /// <remarks>Validation includes checking that the campaign map gallery exists and is complete, that the
    /// provided biome and geography types are valid, and that their combination is allowed. If validation fails, the
    /// result contains descriptive error messages. This method does not update any data; it only performs
    /// validation.</remarks>
    /// <returns>A <see cref="ServiceResult{DTLoadedData}"/> indicating whether the request is valid. Returns a failed result
    /// with error messages if validation fails; otherwise, returns a successful result containing the loaded campaign
    /// map data.</returns>
    private async Task<ServiceResult<DTLoadedData>> ValidateRequestAsync(
        IReadOnlyCollection<Coord> coordinates,
        int campaignMapGalleryId,
        BiomeType? biomeType,
        CampaignMapGeographyType? geographyType,
        CancellationToken cancellationToken)
    {
        if (coordinates is null || coordinates.Count == 0)
        {
            return ServiceResult<DTLoadedData>.Fail("No coordinates provided in request to update Campaign Map Tiles.");
        }

        if (biomeType is null && geographyType is null)
        {
            return ServiceResult<DTLoadedData>.Fail("At least one of BiomeType or GeographyType must be provided in request to update Campaign Map Tiles.");
        }

        Task<ServiceResult<DTCampaignMapGalleryIndex>> fetchIndex =
            mediator.Send(new GetCampaignMapGalleryIndexRequest(campaignMapGalleryId), cancellationToken);

        Task<ServiceResult<IReadOnlyCollection<CampaignMapBiome>>> fetchBiomes =
            mediator.Send(new GetCampaignMapBiomesRequest(), cancellationToken);

        Task<ServiceResult<IReadOnlyCollection<CampaignMapGeography>>> fetchGeography =
            mediator.Send(new GetCampaignMapGeographiesRequest(), cancellationToken);

        await Task.WhenAll(fetchIndex, fetchBiomes, fetchGeography);

        ServiceResult<DTCampaignMapGalleryIndex> indexResult = await fetchIndex;
        ServiceResult<IReadOnlyCollection<CampaignMapBiome>> biomesResult = await fetchBiomes;
        ServiceResult<IReadOnlyCollection<CampaignMapGeography>> geographyResult = await fetchGeography;

        if (indexResult.HasSucceeded == false || indexResult.SuccessValue is null)
        {
            logger.LogWarning(
                "Failed to retrieve Campaign Map Gallery Index for ID {CampaignMapGalleryId}. Error: {Error}",
                campaignMapGalleryId,
                string.Join(Environment.NewLine, indexResult.Errors));

            return ServiceResult<DTLoadedData>.Fail($"Failed to retrieve Campaign Map Gallery Index for ID {campaignMapGalleryId}.");
        }

        if (biomesResult.HasSucceeded == false || biomesResult.SuccessValue is null)
        {
            logger.LogWarning("Failed to retrieve Campaign Map Biomes. Error: {Error}",
                string.Join(Environment.NewLine, biomesResult.Errors));
            return ServiceResult<DTLoadedData>.Fail("Failed to retrieve Campaign Map Biomes.");
        }

        if (geographyResult.HasSucceeded == false || geographyResult.SuccessValue is null)
        {
            logger.LogWarning("Failed to retrieve Campaign Map Geographies. Error: {Error}",
                string.Join(Environment.NewLine, geographyResult.Errors));
            return ServiceResult<DTLoadedData>.Fail("Failed to retrieve Campaign Map Geographies.");
        }

        DTCampaignMapGalleryIndex index = indexResult.SuccessValue;

        if (index.CreationStatus != MapCreationStatus.Complete)
        {
            logger.LogWarning(
                "Cannot update tiles for Campaign Map Gallery ID {CampaignMapGalleryId} because its creation status is {CreationStatus}.",
                campaignMapGalleryId,
                index.CreationStatus);

            return ServiceResult<DTLoadedData>.Fail(
                $"Cannot update tiles for Campaign Map Gallery ID {campaignMapGalleryId} because its creation status is {index.CreationStatus}.");
        }

        IReadOnlyCollection<CampaignMapBiome> biomes = biomesResult.SuccessValue;
        IReadOnlyCollection<CampaignMapGeography> geographies = geographyResult.SuccessValue;

        CampaignMapBiome? requestBiome = null;
        CampaignMapGeography? requestGeography = null;

        if (biomeType is not null)
        {
            requestBiome = biomes.FirstOrDefault(b => b.BiomeType == biomeType);
            if (requestBiome is null)
            {
                logger.LogWarning(
                    "Invalid BiomeType {BiomeType} provided in request for Campaign Map Gallery ID {CampaignMapGalleryId}.",
                    biomeType, campaignMapGalleryId);

                return ServiceResult<DTLoadedData>.Fail($"Invalid BiomeType {biomeType} provided in request.");
            }
        }

        if (geographyType is not null)
        {
            requestGeography = geographies.FirstOrDefault(g => g.CampaignMapGeographyType == geographyType);
            if (requestGeography is null)
            {
                logger.LogWarning(
                    "Invalid GeographyType {GeographyType} provided in request for Campaign Map Gallery ID {CampaignMapGalleryId}.",
                    geographyType, campaignMapGalleryId);

                return ServiceResult<DTLoadedData>.Fail($"Invalid GeographyType {geographyType} provided in request.");
            }
        }

        // Only validate the *combination* if both are present (including mutated implicit geography)
        if (requestBiome is not null && requestGeography is not null)
        {
            ServiceResultBase validityCheck = IsValidBiomeGeographyCombinationForCampaignMap(requestBiome, requestGeography);
            if (validityCheck.HasSucceeded == false)
            {
                logger.LogWarning(
                    "Invalid combination of BiomeType {BiomeType} and GeographyType {GeographyType} provided in request. Errors {err}",
                    biomeType, geographyType, validityCheck.Errors);

                return ServiceResult<DTLoadedData>.Fail(validityCheck.Errors);
            }
        }

        return ServiceResult<DTLoadedData>.Succeed(new DTLoadedData
        {
            Index = index,
            Biomes = biomes,
            Geographies = geographies
        });
    }

    /// <summary>
    /// Refuses a paint that would leave an authored start position sitting on terrain it is not allowed to occupy.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A start position must be on a non-water, traversable Biome whose Geography is passable — the same rule
    /// <see cref="PutCampaignMapStartPositionsHandler"/> enforces when the position is first placed. Without this check the Map Editor could paint
    /// Ocean or Mountainous straight over an existing start position and quietly invalidate it.
    /// </para>
    /// <para>
    /// The whole request is rejected rather than the individual Tiles skipped, so a brush stroke never partially lands and leaves the author guessing
    /// which Tiles took. The author moves the start position first, then paints.
    /// </para>
    /// <para>
    /// The database is only queried when the incoming Biome or Geography is actually hostile to a start position, which is the uncommon case.
    /// </para>
    /// </remarks>
    /// <returns>A success result when the paint is safe; otherwise a failure naming the start positions that block it.</returns>
    private async Task<ServiceResultBase> ValidateStartPositionsRemainValidAsync(
        UpdateCampaignMapTilesRequest request,
        DTLoadedData loaded,
        CampaignMapGeographyType? mutatedGeographyType,
        CancellationToken cancellationToken)
    {
        List<string> reasons = [];

        if (request.BiomeType is not null)
        {
            CampaignMapBiome requestBiome = loaded.Biomes.First(b => b.BiomeType == request.BiomeType);
            if (requestBiome.IsWater || requestBiome.IsTraversable == false)
            {
                reasons.Add($"Biome '{requestBiome.Name}' cannot hold a start position");
            }
        }

        if (mutatedGeographyType is not null)
        {
            CampaignMapGeography requestGeography = loaded.Geographies.First(g => g.CampaignMapGeographyType == mutatedGeographyType);
            if (CampaignTileTerrain.GeographyIsImpassable(requestGeography.IsTraversable))
            {
                reasons.Add($"Geography '{requestGeography.Name}' is impassable and cannot hold a start position");
            }
        }

        // Nothing being painted is hostile to a start position, so there is no need to load them.
        if (reasons.Count == 0)
        {
            return SuccessNoContent();
        }

        HashSet<(int XCoord, int YCoord)> paintedCoordinates = [.. request.Coordinates.Select(c => (c.X, c.Y))];

        int[] xCoords = [.. paintedCoordinates.Select(c => c.XCoord).Distinct()];
        int[] yCoords = [.. paintedCoordinates.Select(c => c.YCoord).Distinct()];

        // The x/y pair of IN clauses matches a bounding box rather than the brush shape, so the results are narrowed to the exact painted coordinates
        // below.
        List<StartPositionCoordinate> startPositionsInBoundingBox = await context.CampaignMapStartPositions
            .AsNoTracking()
            .Where(sp => sp.CampaignMapGalleryIndexId == request.CampaignMapGalleryIndexId)
            .Where(sp => xCoords.Contains(sp.XCoord) && yCoords.Contains(sp.YCoord))
            .Select(sp => new StartPositionCoordinate(sp.XCoord, sp.YCoord, sp.DisplayName))
            .ToListAsync(cancellationToken);

        StartPositionCoordinate[] blockingStartPositions = [.. startPositionsInBoundingBox
            .Where(sp => paintedCoordinates.Contains((sp.XCoord, sp.YCoord)))
            .OrderBy(sp => sp.YCoord)
            .ThenBy(sp => sp.XCoord)];

        if (blockingStartPositions.Length == 0)
        {
            return SuccessNoContent();
        }

        string affected = string.Join(", ", blockingStartPositions.Select(sp => string.IsNullOrWhiteSpace(sp.DisplayName)
            ? $"({sp.XCoord}, {sp.YCoord})"
            : $"{sp.DisplayName} ({sp.XCoord}, {sp.YCoord})"));

        string reason = string.Join(" and ", reasons);

        return ServiceResult.Fail(
            $"{reason}, and this paint covers {(blockingStartPositions.Length == 1 ? "a start position" : "start positions")} at {affected}. " +
            "Move or remove them before painting these tiles.");
    }

    /// <summary>
    /// A start position reduced to the coordinate and label needed to report a paint clash.
    /// </summary>
    private sealed record StartPositionCoordinate(int XCoord, int YCoord, string? DisplayName);

    /// <summary>
    /// Validates whether the specified biome and geography can be combined for use on a Campaign Map.
    /// </summary>
    /// <remarks>The validation checks include paintability, elevation compatibility, and water Biome
    /// restrictions.</remarks>
    /// <returns>A ServiceResultBase indicating whether the biome and geography combination is valid for a campaign map. Returns
    /// a success result if valid; otherwise, returns a failure result with descriptive reasons.</returns>
    private static ServiceResultBase IsValidBiomeGeographyCombinationForCampaignMap(CampaignMapBiome biome, CampaignMapGeography geography)
    {
        var reasons = new List<string>();

        if (biome.IsPaintableOnCampaignMap == false)
        {
            reasons.Add($"Biome '{biome.Name}' cannot be painted on the Campaign Map.");
        }

        if (geography.IsPaintableOnCampaignMap == false)
        {
            reasons.Add($"Geography '{geography.Name}' cannot be painted on the Campaign Map.");
        }

        if (biome.CanBeElevated == false && geography.RelativeElevation > 0)
        {
            reasons.Add($"Biome {biome.Name} cannot be elevated, but {geography.Name} is.");
        }

        if (biome.IsWater && geography.RelativeElevation > 0)
        {
            reasons.Add($"Water biome '{biome.Name}' cannot pair with elevated geography '{geography.Name}' (RelativeElevation must be 0 for water).");
        }

        if (reasons.Count == 0)
        {
            return SuccessNoContent();
        }

        return reasons.Count == 1
            ? ServiceResult.Fail(reasons[0])
            : ServiceResult.Fail($"Invalid biome/geography combination:{Environment.NewLine}- " + string.Join($"{Environment.NewLine}- ", reasons));
    }

    private sealed record DTTileUpdateFailTile
    {
        public required Coord Coord { get; init; }
        public required int DbId { get; init; }

        public required BiomeType OriginalBiomeType { get; init; }
        public required CampaignMapGeographyType OriginalGeographyType { get; init; }
    }

    private sealed record DTTileUpdateFail
    {
        public required string ErrorMessage { get; init; }
        public required IReadOnlyCollection<DTTileUpdateFailTile> FailedTiles { get; init; }
    }

    /// <summary>
    /// Validates whether the specified biome can be applied to the provided map tiles based on their associated geography types, and returns the results of the attempted updates.
    /// </summary>
    /// <returns>A partial success result containing an array of IDs for tiles that were successfully updated, and a collection of failures for tiles that could not be updated due to incompatibility.</returns>
    private static ServiceResult<int[]?, IReadOnlyCollection<DTTileUpdateFail>?> ValidateBiomeChange(
        CampaignMapBiome requestBiome,
        List<IGrouping<CampaignMapGeographyType, CampaignMapGallery>> tilesByGeography,
        IReadOnlyCollection<CampaignMapGeography> geographies)
    {
        List<DTTileUpdateFail> failed = [];

        int[] allIds = [.. tilesByGeography
            .SelectMany(g => g.Select(t => t.CampaignMapGalleryId))
            .Distinct()];

        foreach (var group in tilesByGeography)
        {
            CampaignMapGeographyType geographyType = group.Key;
            CampaignMapGeography geography = geographies.First(g => g.CampaignMapGeographyType == geographyType);

            if (geography.RelativeElevation > 0 && requestBiome.CanBeElevated == false)
            {
                var failedTiles = group
                    .Select(t => new DTTileUpdateFailTile
                    {
                        Coord = new Coord(t.XCoord, t.YCoord),
                        DbId = t.CampaignMapGalleryId,
                        OriginalBiomeType = t.BiomeTypeId,
                        OriginalGeographyType = t.CampaignMapGeographyTypeId
                    })
                    .ToArray();

                failed.Add(new DTTileUpdateFail
                {
                    ErrorMessage = $"Biome '{requestBiome.Name}' cannot be applied to Geography '{geography.Name}' because it cannot be elevated.",
                    FailedTiles = failedTiles
                });
            }
        }

        int[] failedIds = [.. failed.SelectMany(f => f.FailedTiles.Select(t => t.DbId)).Distinct()];
        int[] succeededIds = [.. allIds.Except(failedIds)];

        return ServiceResult<int[]?, IReadOnlyCollection<DTTileUpdateFail>?>.PartialSuccess(succeededIds, failed);
    }

    /// <summary>
    /// Validates a proposed geography change against the specified biomes and tiles, ensuring that biome-specific elevation constraints are enforced.
    /// </summary>
    /// <returns>A partial success result containing the IDs of tiles that passed validation and a collection of failed tile updates, if any.</returns>
    private static ServiceResult<int[]?, IReadOnlyCollection<DTTileUpdateFail>?> ValidateGeographyChange(
        CampaignMapGeography requestGeography,
        List<IGrouping<BiomeType, CampaignMapGallery>> tilesByBiome,
        IReadOnlyCollection<CampaignMapBiome> biomes)
    {
        List<DTTileUpdateFail> failed = [];

        int[] allIds = [.. tilesByBiome
            .SelectMany(g => g.Select(t => t.CampaignMapGalleryId))
            .Distinct()];

        foreach (var group in tilesByBiome)
        {
            BiomeType biomeType = group.Key;
            CampaignMapBiome biome = biomes.First(b => b.BiomeType == biomeType);

            if (biome.CanBeElevated == false && requestGeography.RelativeElevation > 0)
            {
                var failedTiles = group
                    .Select(t => new DTTileUpdateFailTile
                    {
                        Coord = new Coord(t.XCoord, t.YCoord),
                        DbId = t.CampaignMapGalleryId,
                        OriginalBiomeType = t.BiomeTypeId,
                        OriginalGeographyType = t.CampaignMapGeographyTypeId
                    })
                    .ToArray();

                failed.Add(new DTTileUpdateFail
                {
                    ErrorMessage =
                        $"Geography '{requestGeography.Name}' cannot be applied to Biome '{biome.Name}' because it cannot be elevated.",
                    FailedTiles = failedTiles
                });
            }
        }

        int[] failedIds = [.. failed.SelectMany(f => f.FailedTiles.Select(t => t.DbId)).Distinct()];
        int[] succeededIds = [.. allIds.Except(failedIds)];

        return ServiceResult<int[]?, IReadOnlyCollection<DTTileUpdateFail>?>.PartialSuccess(succeededIds, failed);
    }

    /// <summary>
    /// Force a Geography Type update if the Biome Type provided is Coastal or Ocean and no Geography Type is provided, 
    /// or if the Biome Type is Void (which must be Flat geography).
    /// </summary>
    private static CampaignMapGeographyType? ForceGeographyUpdate(BiomeType? biomeType, CampaignMapGeographyType? geographyType)
    {
        CampaignMapGeographyType? mutatedGeographyType = geographyType;
        
        if (mutatedGeographyType is null)
        {
            if (biomeType is BiomeType.Coastal)
            {
                mutatedGeographyType = CampaignMapGeographyType.ShallowWater;
            }
            else if (biomeType is BiomeType.Ocean)
            {
                mutatedGeographyType = CampaignMapGeographyType.DeepWater;
            }
        }

        if (biomeType is BiomeType.Void)
        {
            mutatedGeographyType = CampaignMapGeographyType.Flat;
        }

        return mutatedGeographyType;
    }
}