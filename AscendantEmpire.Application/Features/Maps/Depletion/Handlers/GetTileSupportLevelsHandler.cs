using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Common.Validation;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Maps.CampaignMaps.Classes;
using AscendantEmpire.Application.Features.Maps.CampaignMaps.Services;
using AscendantEmpire.Application.Features.Maps.Depletion.Classes;
using AscendantEmpire.Application.Features.Maps.Depletion.Services;
using AscendantEmpire.BackendContracts.Biomes;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.Maps.Depletion.Handlers;

public record GetTileSupportLevelsRequest(
    int GameId,
    int PlayerId,
    IReadOnlyCollection<int> ForceIds
) : IRequest<ServiceResult<TileSupportLevels>>;

internal class GetTileSupportLevelsRequestValidator : Validator<GetTileSupportLevelsRequest>
{
    public GetTileSupportLevelsRequestValidator()
    {
        RuleFor(x => x.GameId)
            .GreaterThan(0).WithMessage("Game Id is required.");

        RuleFor(x => x.PlayerId)
            .GreaterThan(0).WithMessage("Player Id is required.");

        // The levels are for Forces about to move, so there must be some. The tile toolbar's level, for the Forces already on a tile, comes with its sustenance instead.
        RuleFor(x => x.ForceIds)
            .NotEmpty().WithMessage("At least one Force Id is required.");

        RuleForEach(x => x.ForceIds)
            .GreaterThan(0).WithMessage("Force Ids must be greater than 0.");
    }
}

/// <summary>
/// The support level, and the Turns forecast, of every tile in the Player's live sight for the Forces they have selected, for the hover marker.
/// </summary>
/// <remarks>
/// <para>
/// Asked once per selection rather than once per hovered tile. Sight is the expensive part of any tile read - every Observer's footprint and line of sight - so it is
/// worked out once here, and <see cref="ITileSupportEvaluator"/> then covers every seen tile in a fixed number of queries. The client caches the answer and the hover is
/// a lookup.
/// </para>
/// <para>
/// Only tiles in live sight are answered, through <see cref="IFieldOfViewService"/> - the same rules the live map read and the tile toolbar use. A level discloses what
/// is left on a tile, which changes as anyone feeds on it, so a level for an unwatched tile would leak where other Players' Forces have been. Tiles with no level (food,
/// but nobody to feed) are left out of the answer.
/// </para>
/// </remarks>
internal class GetTileSupportLevelsHandler(
    ILogger<GetTileSupportLevelsHandler> logger,
    IDbContextFactory<AscendantEmpireContext> dbFactory,
    ISightDataReader sightDataReader,
    IFieldOfViewService fieldOfViewService,
    ITileSupportEvaluator tileSupportEvaluator
) : IRequestHandler<GetTileSupportLevelsRequest, ServiceResult<TileSupportLevels>>
{
    public async Task<ServiceResult<TileSupportLevels>> Handle(GetTileSupportLevelsRequest request, CancellationToken cancelToken)
    {
        // Validate the request object
        var validator = new GetTileSupportLevelsRequestValidator();
        ValidationResult validationResult = validator.Validate(request);

        if (validationResult.IsValid == false)
        {
            IEnumerable<string> errors = validationResult.Errors.Select(e => e.ErrorMessage);
            string error = $"Validation failed for {nameof(GetTileSupportLevelsRequest)}.";
            logger.LogWarning("{Error} Errors: {Errors}", error, string.Join("; ", errors));

            return ServiceResult<TileSupportLevels>.Fail(errors.Prepend(error));
        }

        await using AscendantEmpireContext context = await dbFactory.CreateDbContextAsync(cancelToken);

        CampaignMapGeometry? geometry = await sightDataReader.GetGeometryAsync(context, request.GameId, cancelToken);

        if (geometry is null)
        {
            string error = $"Game Id {request.GameId} was not found while reading tile support levels.";
            logger.LogWarning("{Error}", error);

            return ServiceResult<TileSupportLevels>.NotFound(error);
        }

        ServiceResult<IReadOnlyCollection<ObserverFieldOfView>> fieldOfViewResult = await fieldOfViewService.GetPlayerFieldOfViewAsync(request.GameId, request.PlayerId, geometry, cancelToken);

        if (fieldOfViewResult.HasSucceeded == false)
        {
            const string error = "Error working out what a Player's Observers can see while reading tile support levels.";
            logger.LogWarning(error);

            return ServiceResult<TileSupportLevels>.Fail(fieldOfViewResult.Errors.Prepend(error));
        }

        // Several Observers can see the same tile. They all describe the same world-map tile, so the first of each will do.
        SightTerrain[] seenTiles = [.. fieldOfViewResult.SuccessValue!
            .SelectMany(view => view.SeenTiles)
            .DistinctBy(tile => tile.CampaignMapId)];

        ServiceResult<IReadOnlyDictionary<int, TileSupport?>> supportResult = await tileSupportEvaluator.EvaluateAsync(
            context,
            request.GameId,
            request.PlayerId,
            seenTiles,
            geometry,
            request.ForceIds,
            cancelToken);

        if (supportResult.HasSucceeded == false)
        {
            // The evaluator only refuses Force ids that are not the Player's, which is the request's fault.
            const string error = "Error working out whether tiles can support a Player's Forces.";
            logger.LogWarning("{Error} Errors: {Errors}", error, string.Join("; ", supportResult.Errors));

            return ServiceResult<TileSupportLevels>.Fail(supportResult.Errors.Prepend(error));
        }

        IReadOnlyDictionary<int, TileSupport?> supportByTileId = supportResult.SuccessValue!;

        TileSupportLevelEntry[] entries = [.. seenTiles
            .Where(tile => supportByTileId[tile.CampaignMapId] is not null)
            .OrderBy(tile => tile.Y)
            .ThenBy(tile => tile.X)
            .Select(tile => new TileSupportLevelEntry
            {
                X = tile.X,
                Y = tile.Y,
                SupportLevel = supportByTileId[tile.CampaignMapId]!.Level,
                Turns = supportByTileId[tile.CampaignMapId]!.Turns
            })];

        return ServiceResult<TileSupportLevels>.Succeed(new TileSupportLevels { Tiles = entries });
    }
}
