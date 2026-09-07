using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Maps.MapGallery.Classes;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.Maps.MapGallery.Handlers;

public record PutCampaignMapStartPositionItem(
    int XCoord,
    int YCoord,
    string? DisplayName,
    int? LocationScore);

public record PutCampaignMapStartPositionsRequest(
    int CampaignMapGalleryIndexId,
    IReadOnlyCollection<PutCampaignMapStartPositionItem> StartPositions)
    : IRequest<ServiceResult<IReadOnlyCollection<DTCampaignMapStartPosition>>>;

internal class PutCampaignMapStartPositionsHandler(
    ILogger<PutCampaignMapStartPositionsHandler> logger,
    AscendantEmpireContext context)
    : IRequestHandler<PutCampaignMapStartPositionsRequest, ServiceResult<IReadOnlyCollection<DTCampaignMapStartPosition>>>
{
    private const int _MAX_START_POSITIONS = 20;

    public async Task<ServiceResult<IReadOnlyCollection<DTCampaignMapStartPosition>>> Handle(
        PutCampaignMapStartPositionsRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            ServiceResult<LoadedStartPositionContext> validationResult = await ValidateAsync(request, cancellationToken);
            if (!validationResult.HasSucceeded || validationResult.SuccessValue is null)
            {
                return ServiceResult<IReadOnlyCollection<DTCampaignMapStartPosition>>.Fail(validationResult.Errors);
            }

            LoadedStartPositionContext loaded = validationResult.SuccessValue;

            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

            // Replace-all semantics keep the endpoint simple for the small authored collection size.
            await context.CampaignMapStartPositions
                .Where(x => x.CampaignMapGalleryIndexId == request.CampaignMapGalleryIndexId)
                .ExecuteDeleteAsync(cancellationToken);

            List<Domain.Entities.Maps.CampaignMaps.CampaignMapStartPosition> entitiesToInsert = [.. request.StartPositions.Select(item => new Domain.Entities.Maps.CampaignMaps.CampaignMapStartPosition
            {
                CampaignMapGalleryIndexId = request.CampaignMapGalleryIndexId,
                XCoord = item.XCoord,
                YCoord = item.YCoord,
                DisplayName = string.IsNullOrWhiteSpace(item.DisplayName) ? null : item.DisplayName.Trim(),
                LocationScore = item.LocationScore
            })];

            if (entitiesToInsert.Count > 0)
            {
                context.CampaignMapStartPositions.AddRange(entitiesToInsert);
                await context.SaveChangesAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);

            List<DTCampaignMapStartPosition> response = [.. entitiesToInsert
                .OrderBy(x => x.YCoord)
                .ThenBy(x => x.XCoord)
                .ThenBy(x => x.Id)
                .Select(x => new DTCampaignMapStartPosition
                {
                    Id = x.Id,
                    CampaignMapGalleryIndexId = x.CampaignMapGalleryIndexId,
                    XCoord = x.XCoord,
                    YCoord = x.YCoord,
                    DisplayName = x.DisplayName,
                    LocationScore = x.LocationScore
                })];

            return ServiceResult<IReadOnlyCollection<DTCampaignMapStartPosition>>.Succeed(response);
        }
        catch (Exception ex)
        {
            const string error = "An error occurred while updating Campaign Map start positions.";
            logger.LogError(ex, error + " MapId: {MapId}", request.CampaignMapGalleryIndexId);
            return ServiceResult<IReadOnlyCollection<DTCampaignMapStartPosition>>.InternalServerError(error);
        }
    }

    private async Task<ServiceResult<LoadedStartPositionContext>> ValidateAsync(
        PutCampaignMapStartPositionsRequest request,
        CancellationToken cancellationToken)
    {
        if (request.StartPositions is null)
        {
            return ServiceResult<LoadedStartPositionContext>.Fail("StartPositions is required.");
        }

        if (request.StartPositions.Count > _MAX_START_POSITIONS)
        {
            return ServiceResult<LoadedStartPositionContext>.Fail($"A map cannot have more than {_MAX_START_POSITIONS} start positions.");
        }

        Domain.Entities.Maps.CampaignMaps.CampaignMapGalleryIndex? map = await context.CampaignMapGalleryIndex
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.CampaignMapGalleryIndexId, cancellationToken);

        if (map is null)
        {
            return ServiceResult<LoadedStartPositionContext>.NotFound("Campaign Map Gallery Index not found.");
        }

        HashSet<(int XCoord, int YCoord)> seenCoordinates = [];
        foreach (PutCampaignMapStartPositionItem startPosition in request.StartPositions)
        {
            if (startPosition.XCoord < 0 || startPosition.XCoord >= map.Width || startPosition.YCoord < 0 || startPosition.YCoord >= map.Height)
            {
                return ServiceResult<LoadedStartPositionContext>.Fail($"Start position ({startPosition.XCoord}, {startPosition.YCoord}) is outside the map bounds.");
            }

            if (!seenCoordinates.Add((startPosition.XCoord, startPosition.YCoord)))
            {
                return ServiceResult<LoadedStartPositionContext>.Fail($"Duplicate start position coordinates are not allowed: ({startPosition.XCoord}, {startPosition.YCoord}).");
            }
        }

        if (seenCoordinates.Count == 0)
        {
            return ServiceResult<LoadedStartPositionContext>.Succeed(new LoadedStartPositionContext(map));
        }

        int[] xCoords = [.. seenCoordinates.Select(x => x.XCoord).Distinct()];
        int[] yCoords = [.. seenCoordinates.Select(x => x.YCoord).Distinct()];

        // Load only the candidate tiles, with the biome flags and geography cost that gate placement, so each submitted coordinate can be checked
        // deterministically.
        List<StartPositionTileCandidate> candidateTiles = await context.CampaignMapGallery
            .AsNoTracking()
            .Where(x => x.CampaignMapGalleryIndexId == request.CampaignMapGalleryIndexId)
            .Where(x => xCoords.Contains(x.XCoord) && yCoords.Contains(x.YCoord))
            .Select(x => new StartPositionTileCandidate(
                x.XCoord,
                x.YCoord,
                x.Biome.IsWater,
                x.Biome.IsTraversable,
                x.Geography.MovementCost))
            .ToListAsync(cancellationToken);

        Dictionary<(int XCoord, int YCoord), StartPositionTileCandidate> tileLookup = candidateTiles
            .ToDictionary(x => (x.XCoord, x.YCoord));

        foreach ((int xCoord, int yCoord) in seenCoordinates)
        {
            if (!tileLookup.TryGetValue((xCoord, yCoord), out StartPositionTileCandidate? tile))
            {
                return ServiceResult<LoadedStartPositionContext>.Fail($"Start position ({xCoord}, {yCoord}) does not reference a valid campaign map tile.");
            }

            if (tile.BiomeIsWater || !tile.BiomeIsTraversable)
            {
                return ServiceResult<LoadedStartPositionContext>.Fail($"Start position ({xCoord}, {yCoord}) must be on a non-water traversable biome.");
            }

            // The biome can be perfectly habitable while the geography on top of it is not — a grassland mountain, for instance.
            if (tile.GeographyMovementCost >= Domain.Entities.Maps.CampaignMaps.CampaignGeography.IMPASSABLE_MOVEMENT_COST)
            {
                return ServiceResult<LoadedStartPositionContext>.Fail(
                    $"Start position ({xCoord}, {yCoord}) cannot be on impassable geography (movement cost {tile.GeographyMovementCost:0.##}).");
            }
        }

        return ServiceResult<LoadedStartPositionContext>.Succeed(new LoadedStartPositionContext(map));
    }

    private sealed record LoadedStartPositionContext(Domain.Entities.Maps.CampaignMaps.CampaignMapGalleryIndex Map);

    /// <summary>
    /// A single candidate Tile, flattened to just the Biome and Geography values that gate whether a start position may sit on it.
    /// </summary>
    /// <remarks>
    /// The Biome and the Geography each carry their own Movement Cost, so the Geography one is named for its source to keep the two apart.
    /// </remarks>
    private sealed record StartPositionTileCandidate(
        int XCoord,
        int YCoord,
        bool BiomeIsWater,
        bool BiomeIsTraversable,
        decimal GeographyMovementCost);
}
