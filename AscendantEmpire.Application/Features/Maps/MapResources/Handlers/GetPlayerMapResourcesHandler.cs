using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Common.Validation;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Maps.CampaignMaps.Classes;
using AscendantEmpire.Application.Features.Maps.CampaignMaps.Services;
using AscendantEmpire.Application.Features.Maps.MapResources.Services;
using AscendantEmpire.Application.Helpers.ScaledNumerics;
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
/// Three gates deciding different things. <b>Seen</b> decides whether the tile exists for this Player at all: ground they have never laid eyes on is drawn as
/// solid fog, and nothing on it may be disclosed. <b>Discovered</b> is stored, in <c>PlayerKnownMapResource</c>, and decides whether a resource on a seen tile
/// is reported: once a Player has found an instance it stays on their map wherever they go. <b>Observed</b> is derived live from their Observers' vision and
/// never stored, the same as sustenance and the support indicator, and decides only whether a quantity comes with it — a quantity moves as anyone extracts
/// from the tile, so giving one for an unwatched tile would leak what other Players have been doing there.
/// </para>
/// <para>
/// Being seen has to be asked separately because discovery does not imply it: <c>PlayerMapResourceDiscoveryService</c> seeds a row for every instance of a
/// default-discoverable type at setup, so without this gate a new Game would report every berry patch on the map to a Player who has not left their start
/// tile. See <see cref="ReadKnownAsync"/> for why it is not the explored stamp alone.
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

        List<KnownInstance> known = await ReadKnownAsync(context, request, observedTileIds, cancelToken);

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
    /// Every instance the Player has discovered on ground they have seen, with the type values a tile needs to draw it.
    /// </summary>
    /// <remarks>
    /// Not narrowed by whether a tile is watched <i>now</i>: a discovered MapResource stays on the Player's map wherever they are, and it is only its quantity
    /// that waits on an Observer. It <i>is</i> narrowed by whether they have ever seen the tile, which is a different question — a Player cannot be told what
    /// sits on ground they have never been to, and discovery is seeded map-wide at setup, so nothing else would stop a new Game disclosing the whole map's
    /// resources.
    /// <para>
    /// Seen means stamped as explored <b>or</b> in sight at this moment, which is how <c>GetPlayerCampaignMapHandler</c> decides the same thing. The two have
    /// to agree, and the stamp alone does not answer it: <c>FirstSeenOnTurnId</c> is written by the map read itself, so on a Player's first read of a new Game
    /// their start tiles are in plain sight with the stamp not yet written, and a berry patch beside their start position would be withheld from the tile
    /// drawing it.
    /// </para>
    /// <para>
    /// Query syntax for the joins: neither a discovery row nor an instance carries a navigation to what it names, so these are genuine joins between tables
    /// rather than projections through mapped relationships. The same join is written the same way in <c>MapResourceInstantiationService</c>. Exploration
    /// comes in as a subquery instead of a fourth join, so a Player's tile rows cannot multiply the instances they match.
    /// </para>
    /// </remarks>
    /// <param name="observedTileIds">The world-map tile ids the Player's Observers can see right now, which count as seen whatever the stamp says.</param>
    private static async Task<List<KnownInstance>> ReadKnownAsync(
        AscendantEmpireContext context,
        GetPlayerMapResourcesRequest request,
        IReadOnlyCollection<int> observedTileIds,
        CancellationToken cancelToken)
    {
        IQueryable<int> exploredTileIds = context.PlayerCampaignMaps
            .Where(playerTile => playerTile.GameId == request.GameId
                && playerTile.PlayerId == request.PlayerId
                && playerTile.FirstSeenOnTurnId != null)
            .Select(playerTile => playerTile.CampaignMapId);

        return await (
            from known in context.PlayerKnownMapResources.AsNoTracking()
            join instance in context.MapResourceInstances on known.MapResourceInstanceId equals instance.Id
            join type in context.MapResourceTypes on instance.MapResourceTypeId equals type.Id
            where known.GameId == request.GameId
                && known.PlayerId == request.PlayerId
                && (exploredTileIds.Contains(instance.CampaignMapId) || observedTileIds.Contains(instance.CampaignMapId))
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
    /// What the Player is shown for the instance, in whole units, or <see langword="null"/> where it holds no single quantity to estimate.
    /// </summary>
    /// <remarks>
    /// A stratified instance's quantity is in layers the Player has to survey, and a Distribution's is spread across per-Asset amounts. Both are reported
    /// without a number until the work that presents those shapes lands.
    /// <para>
    /// Descaled before it leaves, as sustenance amounts are, so the estimate reaches the client in the units a Player reads rather than in raw scaled ones.
    /// </para>
    /// </remarks>
    private static decimal? EstimateFor(long gameSeed, int playerId, KnownInstance instance)
    {
        if (instance.IsStratified || instance.IsDistribution || instance.Quantity == null)
        {
            return null;
        }

        long? estimate = MapResourceEstimationRules.Estimate(
            gameSeed,
            instance.InstanceId,
            playerId,
            instance.Quantity.Value,
            instance.EstimationFuzzyFactor);

        return estimate == null ? null : ScaledAmount.FromRaw(estimate.Value).ToDecimal();
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
