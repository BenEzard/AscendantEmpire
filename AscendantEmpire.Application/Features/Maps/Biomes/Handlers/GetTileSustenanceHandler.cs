using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Common.Validation;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Maps.Biomes.Classes;
using AscendantEmpire.Application.Features.Maps.Biomes.Services;
using AscendantEmpire.Application.Features.Maps.CampaignMaps.Classes;
using AscendantEmpire.Application.Features.Maps.CampaignMaps.Services;
using AscendantEmpire.Application.Features.Maps.Depletion.Classes;
using AscendantEmpire.Application.Features.Maps.Depletion.Services;
using AscendantEmpire.Application.Helpers.ScaledNumerics;
using AscendantEmpire.BackendContracts.Biomes;
using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Enums.Maps;
using AscendantEmpire.Domain.Enums.Maps;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.Maps.Biomes.Handlers;

public record GetTileSustenanceRequest(
    int GameId,
    int PlayerId,
    int X,
    int Y
) : IRequest<ServiceResult<TileSustenance>>;

internal class GetTileSustenanceRequestValidator : Validator<GetTileSustenanceRequest>
{
    public GetTileSustenanceRequestValidator()
    {
        RuleFor(x => x.GameId)
            .GreaterThan(0).WithMessage("Game Id is required.");

        RuleFor(x => x.PlayerId)
            .GreaterThan(0).WithMessage("Player Id is required.");

        // The upper bounds depend on the Game's map, so they are checked against its geometry in the handler.
        RuleFor(x => x.X)
            .GreaterThanOrEqualTo(0).WithMessage("X cannot be negative.");

        RuleFor(x => x.Y)
            .GreaterThanOrEqualTo(0).WithMessage("Y cannot be negative.");
    }
}

/// <summary>
/// The Forage, Hunt, Fish and Fodder available to a Player on one campaign map tile, for the tile toolbar.
/// </summary>
/// <remarks>
/// <para>
/// Only answers for a tile in the Player's <b>live sight</b>. The amount taken from a tile is Player-neutral and grows whenever anyone feeds on it, so reporting what is left on
/// a tile nobody of theirs is watching would tell the Player where other Players' Forces have been. An unwatched tile comes back as <see cref="TileSustenance.NotObserved"/>
/// (a success, not an error), and the client shows nothing.
/// </para>
/// <para>
/// Sight goes through <see cref="IFieldOfViewService"/>, the same rules the live map read uses, so the toolbar can never disclose a tile the map does not show as in view. The
/// seen tile already carries its world-map biome, so no separate tile read is needed.
/// </para>
/// <para>
/// Values come from <see cref="IPlayerSustenanceValueReader"/> (the Player's effective values), less the tile's <c>Taken</c> amount of each resource from
/// <c>game.CampaignMapDepletion</c>, through <see cref="SustenanceValueCalculator.CalculateAvailableValue"/>. A tile with no depletion row has never been fed on, so
/// nothing has been taken from it.
/// </para>
/// <para>
/// The tile's <see cref="TileSustenance.SupportLevel"/> comes from <see cref="ITileSupportEvaluator"/> with no requested Forces, so the need is only the Player's own Forces
/// already standing on the tile. The hover marker asks the same evaluator about every observed tile at once, through <c>GetTileSupportLevelsHandler</c>.
/// </para>
/// </remarks>
internal class GetTileSustenanceHandler(
    ILogger<GetTileSustenanceHandler> logger,
    IDbContextFactory<AscendantEmpireContext> dbFactory,
    ISightDataReader sightDataReader,
    IFieldOfViewService fieldOfViewService,
    IPlayerSustenanceValueReader sustenanceValueReader,
    ITileSupportEvaluator tileSupportEvaluator
) : IRequestHandler<GetTileSustenanceRequest, ServiceResult<TileSustenance>>
{
    public async Task<ServiceResult<TileSustenance>> Handle(GetTileSustenanceRequest request, CancellationToken cancelToken)
    {
        try
        {
            // Validate the request object
            var validator = new GetTileSustenanceRequestValidator();
            ValidationResult validationResult = validator.Validate(request);

            if (validationResult.IsValid == false)
            {
                IEnumerable<string> errors = validationResult.Errors.Select(e => e.ErrorMessage);
                string error = $"Validation failed for {nameof(GetTileSustenanceRequest)}.";
                logger.LogWarning("{Error} Errors: {Errors}", error, string.Join("; ", errors));

                return ServiceResult<TileSustenance>.Fail(errors.Prepend(error));
            }

            await using AscendantEmpireContext context = await dbFactory.CreateDbContextAsync(cancelToken);

            CampaignMapGeometry? geometry = await sightDataReader.GetGeometryAsync(context, request.GameId, cancelToken);

            if (geometry is null)
            {
                string error = $"Game Id {request.GameId} was not found while reading a tile's sustenance.";
                logger.LogWarning("{Error}", error);

                return ServiceResult<TileSustenance>.NotFound(error);
            }

            // A coordinate off the edge of the map names no tile, and is not wrapped onto one.
            if (request.X >= geometry.Width || request.Y >= geometry.Height)
            {
                string error = $"Tile ({request.X}, {request.Y}) is not on Game Id {request.GameId}'s {geometry.Width} x {geometry.Height} map.";
                logger.LogWarning("{Error}", error);

                return ServiceResult<TileSustenance>.NotFound(error);
            }

            ServiceResult<IReadOnlyCollection<ObserverFieldOfView>> fieldOfViewResult = await fieldOfViewService.GetPlayerFieldOfViewAsync(request.GameId, request.PlayerId, geometry, cancelToken);

            if (fieldOfViewResult.HasSucceeded == false)
            {
                const string error = "Error working out what a Player's Observers can see while reading a tile's sustenance.";
                logger.LogWarning(error);

                return ServiceResult<TileSustenance>.Fail(fieldOfViewResult.Errors.Prepend(error));
            }

            // The tile as one of the Player's Observers sees it, if any does. Several may; they all describe the same world-map tile, so the first will do.
            SightTerrain? seenTile = fieldOfViewResult.SuccessValue!
                .SelectMany(view => view.SeenTiles)
                .FirstOrDefault(tile => tile.X == request.X && tile.Y == request.Y);

            if (seenTile is null)
            {
                return ServiceResult<TileSustenance>.Succeed(TileSustenance.NotObserved);
            }

            PlayerSustenanceValues values = await sustenanceValueReader.GetValuesAsync(context, request.PlayerId, cancelToken);

            // What has been taken from the tile, by anyone. Null when the tile has never been fed on, which DepletionTaken reads as nothing taken.
            CampaignMapDepletion? depletion = await context.CampaignMapDepletions
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.CampaignMapId == seenTile.CampaignMapId, cancelToken);

            // Whether the tile can feed the Forces already on it. No Forces are requested, so the only way this fails is a defect in the evaluator.
            ServiceResult<IReadOnlyDictionary<int, TileSupport?>> supportResult = await tileSupportEvaluator.EvaluateAsync(context, request.GameId, request.PlayerId, [seenTile], [], cancelToken);

            if (supportResult.HasSucceeded == false)
            {
                const string error = "Error working out whether a tile can support the Player's Forces while reading its sustenance.";
                logger.LogWarning(error);

                return ServiceResult<TileSustenance>.Fail(supportResult.Errors.Prepend(error));
            }

            return ServiceResult<TileSustenance>.Succeed(new TileSustenance
            {
                IsObserved = true,
                Forage = GetAvailableValue(values, seenTile.BiomeType, SustenanceResource.Forage, depletion),
                Hunt = GetAvailableValue(values, seenTile.BiomeType, SustenanceResource.Hunt, depletion),
                Fish = GetAvailableValue(values, seenTile.BiomeType, SustenanceResource.Fish, depletion),
                Fodder = GetAvailableValue(values, seenTile.BiomeType, SustenanceResource.Fodder, depletion),
                // The level only: the toolbar colours itself by it, and does not show the Turns the hover marker does.
                SupportLevel = supportResult.SuccessValue![seenTile.CampaignMapId]?.Level
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while handling {RequestName}.", nameof(GetTileSustenanceRequest));

            return ServiceResult<TileSustenance>.Fail("An unexpected error occurred while retrieving a tile's sustenance.");
        }
    }

    /// <summary>
    /// One resource's available amount, presented as a decimal, or <see langword="null"/> when the resource does not exist in the biome.
    /// </summary>
    private static decimal? GetAvailableValue(PlayerSustenanceValues values, BiomeType biome, SustenanceResource resource, CampaignMapDepletion? depletion)
    {
        ScaledAmount? availableValue = SustenanceValueCalculator.CalculateAvailableValue(values.GetValue(biome, resource), DepletionTaken.GetTaken(depletion, resource));

        return availableValue?.ToDecimal();
    }
}
