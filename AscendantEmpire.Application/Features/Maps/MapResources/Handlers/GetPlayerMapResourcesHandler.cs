using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Common.Validation;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Maps.CampaignMaps.Classes;
using AscendantEmpire.Application.Features.Maps.CampaignMaps.Services;
using AscendantEmpire.Application.Features.Maps.MapResources.Services;
using AscendantEmpire.BackendContracts.MapResources.GetPlayerMapResources;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.Maps.MapResources.Handlers;

public record GetPlayerMapResourcesRequest(int GameId, int PlayerId) : IRequest<ServiceResult<PlayerMapResources>>;

internal class GetPlayerMapResourcesRequestValidator : Validator<GetPlayerMapResourcesRequest>
{
    public GetPlayerMapResourcesRequestValidator()
    {
        RuleFor(x => x.GameId)
            .GreaterThan(0).WithMessage("Game Id is required.");

        RuleFor(x => x.PlayerId)
            .GreaterThan(0).WithMessage("Player Id is required.");
    }
}

/// <summary>
/// The MapResources one Player has discovered, for drawing on the campaign map, with a quantity for the ones they can see right now.
/// </summary>
/// <remarks>
/// <para>
/// Two gates deciding different things. <b>Discovered</b> is stored, in <c>PlayerKnownMapResource</c>, and decides what is reported at all: once a Player has
/// found an instance it stays on their map wherever they go. <b>Observed</b> is derived live from their Observers' vision and never stored, the same as
/// sustenance and the support indicator, and decides only whether a quantity comes with it — a quantity moves as anyone extracts from the tile, so giving one
/// for an unwatched tile would leak what other Players have been doing there.
/// </para>
/// <para>
/// An unobserved entry is therefore the icon and nothing more. See "What a discovered MapResource shows" in
/// <c>docs/design/campaign-map/map-resources.md</c>.
/// </para>
/// <para>
/// Quantities are estimates, never the truth. See <see cref="MapResourceEstimationRules"/> and "Estimated quantity" in the same document.
/// </para>
/// </remarks>
internal class GetPlayerMapResourcesHandler(
    ILogger<GetPlayerMapResourcesHandler> logger,
    IDbContextFactory<AscendantEmpireContext> dbFactory,
    ISightDataReader sightDataReader,
    IFieldOfViewService fieldOfViewService
) : IRequestHandler<GetPlayerMapResourcesRequest, ServiceResult<PlayerMapResources>>
{
    public async Task<ServiceResult<PlayerMapResources>> Handle(GetPlayerMapResourcesRequest request, CancellationToken cancelToken)
    {
        var validator = new GetPlayerMapResourcesRequestValidator();
        ValidationResult validationResult = validator.Validate(request);

        if (validationResult.IsValid == false)
        {
            IEnumerable<string> errors = validationResult.Errors.Select(e => e.ErrorMessage);
            string error = $"Validation failed for {nameof(GetPlayerMapResourcesRequest)}.";
            logger.LogWarning("{Error} Errors: {Errors}", error, string.Join("; ", errors));

            return ServiceResult<PlayerMapResources>.Fail(errors.Prepend(error));
        }

        await using AscendantEmpireContext context = await dbFactory.CreateDbContextAsync(cancelToken);

        CampaignMapGeometry? geometry = await sightDataReader.GetGeometryAsync(context, request.GameId, cancelToken);

        if (geometry is null)
        {
            string error = $"Game Id {request.GameId} was not found while reading the Player's MapResources.";
            logger.LogWarning("{Error}", error);

            return ServiceResult<PlayerMapResources>.NotFound(error);
        }

        ServiceResult<IReadOnlyCollection<ObserverFieldOfView>> fieldOfViewResult =
            await fieldOfViewService.GetPlayerFieldOfViewAsync(request.GameId, request.PlayerId, geometry, cancelToken);

        if (fieldOfViewResult.HasSucceeded == false)
        {
            const string error = "Error working out what a Player's Observers can see while reading their MapResources.";
            logger.LogWarning(error);

            return ServiceResult<PlayerMapResources>.Fail(fieldOfViewResult.Errors.Prepend(error));
        }

        // Several Observers can see one tile, and they all describe the same world-map tile, so the ids alone are what this read needs from them.
        HashSet<int> observedTileIds = [.. fieldOfViewResult.SuccessValue!.SelectMany(view => view.SeenTiles).Select(tile => tile.CampaignMapId)];

        long gameSeed = await context.GameIndex
            .Where(game => game.Id == request.GameId)
            .Select(game => game.GenerationSeed)
            .SingleAsync(cancelToken);

        List<KnownInstance> known = await ReadKnownAsync(context, request, cancelToken);

        PlayerMapResourceEntry[] entries = [.. known
            .Select(instance => new
            {
                Instance = instance,
                Coord = geometry.GetCoord(instance.CampaignMapId),
                IsObserved = observedTileIds.Contains(instance.CampaignMapId)
            })
            .OrderBy(row => row.Coord.Y)
            .ThenBy(row => row.Coord.X)
            .ThenBy(row => row.Instance.Slot)
            .Select(row => new PlayerMapResourceEntry
            {
                X = row.Coord.X,
                Y = row.Coord.Y,
                Slot = row.Instance.Slot,
                MapResourceTypeId = row.Instance.MapResourceTypeId,
                Code = row.Instance.Code,
                DisplayName = row.Instance.DisplayName,
                IconPath = row.Instance.IconPath,
                IsObserved = row.IsObserved,
                EstimatedQuantity = row.IsObserved ? EstimateFor(gameSeed, request.PlayerId, row.Instance) : null
            })];

        return ServiceResult<PlayerMapResources>.Succeed(new PlayerMapResources { Resources = entries });
    }

    /// <summary>
    /// Every instance the Player has discovered, with the type values a tile needs to draw it.
    /// </summary>
    /// <remarks>
    /// Not narrowed by sight: a discovered MapResource stays on the Player's map wherever they are, and it is only its quantity that waits on an Observer.
    /// Query syntax for the joins: neither a discovery row nor an instance carries a navigation to what it names, so these are genuine joins between tables
    /// rather than projections through mapped relationships. The same join is written the same way in <c>MapResourceInstantiationService</c>.
    /// </remarks>
    private static async Task<List<KnownInstance>> ReadKnownAsync(
        AscendantEmpireContext context,
        GetPlayerMapResourcesRequest request,
        CancellationToken cancelToken)
    {
        return await (
            from known in context.PlayerKnownMapResources.AsNoTracking()
            join instance in context.MapResourceInstances on known.MapResourceInstanceId equals instance.Id
            join type in context.MapResourceTypes on instance.MapResourceTypeId equals type.Id
            where known.GameId == request.GameId
                && known.PlayerId == request.PlayerId
            select new KnownInstance(
                instance.Id,
                instance.CampaignMapId,
                instance.Slot,
                instance.Quantity,
                instance.EstimationFuzzyFactor,
                type.Id,
                type.Code,
                type.DisplayName,
                type.IconPath,
                type.IsStratified,
                type.IsDistribution))
            .ToListAsync(cancelToken);
    }

    /// <summary>
    /// What the Player is shown for the instance, or <see langword="null"/> where it holds no single quantity to estimate.
    /// </summary>
    /// <remarks>
    /// A stratified instance's quantity is in layers the Player has to survey, and a Distribution's is spread across per-Asset amounts. Both are reported
    /// without a number until the work that presents those shapes lands.
    /// </remarks>
    private static long? EstimateFor(long gameSeed, int playerId, KnownInstance instance)
    {
        if (instance.IsStratified || instance.IsDistribution || instance.Quantity == null)
        {
            return null;
        }

        return MapResourceEstimationRules.Estimate(
            gameSeed,
            instance.InstanceId,
            playerId,
            instance.Quantity.Value,
            instance.EstimationFuzzyFactor);
    }

    /// <summary>
    /// One instance a Player has discovered, flattened with the type values its tile needs.
    /// </summary>
    private sealed record KnownInstance(
        int InstanceId,
        int CampaignMapId,
        byte Slot,
        long? Quantity,
        decimal? EstimationFuzzyFactor,
        int MapResourceTypeId,
        string Code,
        string DisplayName,
        string? IconPath,
        bool IsStratified,
        bool IsDistribution);
}
