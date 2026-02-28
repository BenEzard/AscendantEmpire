using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Handlers.Maps.Biomes;
using AscendantEmpire.Application.Handlers.Maps.CampaignMaps;
using AscendantEmpire.Application.Handlers.Maps.Geographies;
using AscendantEmpire.Application.Handlers.Maps.MapGallery.Classes;
using AscendantEmpire.BackendContracts.Biomes;
using AscendantEmpire.BackendContracts.CampaignMaps;
using AscendantEmpire.BackendContracts.Geographies;
using AscendantEmpire.BackendContracts.MapGallery.UpdateCampaignMapTiles;
using AscendantEmpire.Domain.Entities.Maps.CampaignMaps;
using AscendantEmpire.Domain.Enums.Maps;

using MediatR;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Handlers.Maps.MapGallery;

public record UpdateCampaignMapTilesRequest(
    int CampaignMapGalleryIndexId,
    IReadOnlyCollection<Coord> Coordinates,
    BiomeType? BiomeType,
    CampaignMapGeographyType? CampaignMapGeographyType
) : IRequest<ServiceResult<int, IReadOnlyCollection<TileUpdateFail>?>>;

internal class UpdateCampaignMapTilesHandler(
    ILogger<UpdateCampaignMapTilesHandler> logger,
    AscendantEmpireContext context,
    IMediator mediator
) : IRequestHandler<UpdateCampaignMapTilesRequest, ServiceResult<int, IReadOnlyCollection<TileUpdateFail>?>>
{
    /// <summary>
    /// When false, it uses ExecuteUpdate; when true, it falls back to loading entities, modifying them, and saving changes for testing purposes.
    /// </summary>
    private bool _TEST_MODE = false;

    public async Task<ServiceResult<int, IReadOnlyCollection<TileUpdateFail>?>> Handle(UpdateCampaignMapTilesRequest request, CancellationToken cancellationToken)
    {
        int tilesUpdated = 0;
        var failures = new List<TileUpdateFail>();

        try
        {
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

            EnhancedCoord[] enhancedCoordinates = MapMethods.EnhanceCoordinates(index.CampaignMapOffsetId, index.Width, request.Coordinates);

            int[] dbIds = [.. enhancedCoordinates.Select(ec => ec.DbId)];

            // If both are being updated (including implicit mutated geography), just apply.
            if (request.BiomeType is not null && mutatedGeographyType is not null)
            {
                if (_TEST_MODE)
                {
                    List<CampaignMapGallery> tilesToUpdateInTestMode = await context.CampaignMapGallery
                        .Where(t => t.CampaignMapGalleryIndexId == request.CampaignMapGalleryIndexId)
                        .Where(t => dbIds.Contains(t.CampaignMapGalleryId))
                        .ToListAsync(cancellationToken);
                    
                    foreach (CampaignMapGallery tile in tilesToUpdateInTestMode)
                    {
                        tile.BiomeTypeId = request.BiomeType.Value;
                        tile.CampaignMapGeographyTypeId = mutatedGeographyType.Value;
                    }

                    tilesUpdated = await context.SaveChangesAsync(cancellationToken);
                }
                else
                    tilesUpdated = await context.CampaignMapGallery
                    .Where(t => t.CampaignMapGalleryIndexId == request.CampaignMapGalleryIndexId)
                    .Where(t => dbIds.Contains(t.CampaignMapGalleryId))
                    .ExecuteUpdateAsync(s => s
                            .SetProperty(t => t.BiomeTypeId, request.BiomeType.Value)
                            .SetProperty(t => t.CampaignMapGeographyTypeId, mutatedGeographyType.Value),
                        cancellationToken);

                return ServiceResult<int, IReadOnlyCollection<TileUpdateFail>?>.Succeed(tilesUpdated);
            }

            // Otherwise, only one property is being updated and we need per-tile validation.
            List<CampaignMapGallery> tilesToUpdate = await context.CampaignMapGallery
                .Where(t => t.CampaignMapGalleryIndexId == request.CampaignMapGalleryIndexId)
                .Where(t => dbIds.Contains(t.CampaignMapGalleryId))
                .ToListAsync(cancellationToken);

            // Changing Biome
            if (request.BiomeType is not null) // change biome; validate vs current geography
            {
                List<IGrouping<CampaignMapGeographyType, CampaignMapGallery>> tilesByGeography =
                    [.. tilesToUpdate.GroupBy(t => t.CampaignMapGeographyTypeId)];

                CampaignMapBiome requestBiome =
                    validationResult.SuccessValue.Biomes.First(b => b.BiomeType == request.BiomeType);

                IReadOnlyCollection<CampaignMapGeography> geographies = validationResult.SuccessValue.Geographies;

                ServiceResult<int[]?, IReadOnlyCollection<DTTileUpdateFail>?> validateBiomeResult =
                    ValidateBiomeChange(requestBiome, tilesByGeography, geographies);

                if (validateBiomeResult.SuccessValue?.Length > 0)
                {
                    tilesUpdated = await context.CampaignMapGallery
                        .Where(t => t.CampaignMapGalleryId == request.CampaignMapGalleryIndexId)
                        .Where(t => validateBiomeResult.SuccessValue.Contains(t.CampaignMapGalleryId))
                        .ExecuteUpdateAsync(s => s.SetProperty(t => t.BiomeTypeId, request.BiomeType.Value),
                            cancellationToken);
                }

                if (validateBiomeResult.ErrorValue?.Count > 0)
                {
                    failures.AddRange(validateBiomeResult.ErrorValue.Select(ToContractFail));
                }
            }
            else if (request.CampaignMapGeographyType is not null) // change geography; validate vs current biome
            {
                List<IGrouping<BiomeType, CampaignMapGallery>> tilesByBiome =
                    [.. tilesToUpdate.GroupBy(t => t.BiomeTypeId)];

                CampaignMapGeography requestGeography =
                    validationResult.SuccessValue.Geographies.First(g => g.CampaignMapGeographyType == request.CampaignMapGeographyType);

                IReadOnlyCollection<CampaignMapBiome> biomes = validationResult.SuccessValue.Biomes;

                ServiceResult<int[]?, IReadOnlyCollection<DTTileUpdateFail>?> validateGeographyResult =
                    ValidateGeographyChange(requestGeography, tilesByBiome, biomes);

                if (validateGeographyResult.SuccessValue?.Length > 0)
                {
                    tilesUpdated = await context.CampaignMapGallery
                        .Where(t => t.CampaignMapGalleryId == request.CampaignMapGalleryIndexId)
                        .Where(t => validateGeographyResult.SuccessValue.Contains(t.CampaignMapGalleryId))
                        .ExecuteUpdateAsync(s => s.SetProperty(t => t.CampaignMapGeographyTypeId, request.CampaignMapGeographyType.Value),
                            cancellationToken);
                }

                if (validateGeographyResult.ErrorValue?.Count > 0)
                {
                    failures.AddRange(validateGeographyResult.ErrorValue.Select(ToContractFail));
                }
            }

            return ServiceResult<int, IReadOnlyCollection<TileUpdateFail>?>.PartialSuccess(
                tilesUpdated,
                failures);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while updating campaign map tiles.");
            return ServiceResult<int, IReadOnlyCollection<TileUpdateFail>?>.Fail("An error occurred while updating campaign map tiles.");
        }
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
            .SelectMany(g => g.Select(t => t.CampaignMapGalleryIndexId))
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
                        DbId = t.CampaignMapGalleryIndexId,
                        OriginalBiomeType = t.BiomeTypeId,
                        OriginalGeographyType = t.CampaignMapGeographyTypeId
                    })
                    .DistinctBy(x => x.DbId)
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
            .SelectMany(g => g.Select(t => t.CampaignMapGalleryIndexId))
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
                        DbId = t.CampaignMapGalleryIndexId,
                        OriginalBiomeType = t.BiomeTypeId,
                        OriginalGeographyType = t.CampaignMapGeographyTypeId
                    })
                    .DistinctBy(x => x.DbId)
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