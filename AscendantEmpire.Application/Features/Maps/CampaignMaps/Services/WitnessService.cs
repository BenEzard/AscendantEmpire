using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Maps.CampaignMaps.Classes;
using AscendantEmpire.BackendContracts.CampaignMaps;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.Maps.CampaignMaps.Services;

/// <inheritdoc cref="IWitnessService" />
internal class WitnessService(
    ILogger<WitnessService> logger,
    IDbContextFactory<AscendantEmpireContext> dbFactory,
    ISightDataReader sightDataReader,
    ICoalitionResolver coalitionResolver,
    IFieldOfViewService fieldOfViewService) : IWitnessService
{
    /// <inheritdoc />
    public async Task<ServiceResult<IReadOnlyDictionary<long, IReadOnlyCollection<WitnessingCoalition>>>> FindWitnessesAsync(WitnessQuery query, CancellationToken cancelToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        try
        {
            await using AscendantEmpireContext context = await dbFactory.CreateDbContextAsync(cancelToken);

            return await FindWitnessesAsync(context, query, cancelToken);
        }
        catch (Exception ex)
        {
            return LogAndFail(ex, query);
        }
    }

    /// <inheritdoc />
    public async Task<ServiceResult<IReadOnlyDictionary<long, IReadOnlyCollection<WitnessingCoalition>>>> FindWitnessesAsync(
        AscendantEmpireContext context,
        WitnessQuery query,
        CancellationToken cancelToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(query);

        try
        {
            // Refused before anything is read: with a key used twice, one of the answers could never be found, and the caller would silently lose it.
            List<long> duplicateKeys = [.. query.Probes.GroupBy(probe => probe.Key).Where(group => group.Count() > 1).Select(group => group.Key)];

            if (duplicateKeys.Count > 0)
            {
                string error = $"Every witness probe needs its own key, but these are used more than once: {string.Join(", ", duplicateKeys)}.";
                logger.LogWarning(error);
                return ServiceResult<IReadOnlyDictionary<long, IReadOnlyCollection<WitnessingCoalition>>>.Fail(error);
            }

            if (query.Probes.Count == 0)
            {
                return ServiceResult<IReadOnlyDictionary<long, IReadOnlyCollection<WitnessingCoalition>>>.Succeed(new Dictionary<long, IReadOnlyCollection<WitnessingCoalition>>());
            }

            // Who may witness. An excluded Player takes their whole coalition with them: an ally sharing the acting Player's vision watched the same events
            // on the same screen, and has nothing to be told.
            IReadOnlyCollection<VisionCoalition> coalitions = await coalitionResolver.ResolveAsync(context, query.GameId, cancelToken);

            HashSet<int> excludedPlayerIds = [.. query.ExcludedPlayerIds];

            List<VisionCoalition> observingCoalitions = [.. coalitions.Where(coalition => coalition.MemberPlayerIds.Any(excludedPlayerIds.Contains) == false)];

            int[] observingPlayerIds = [.. observingCoalitions.SelectMany(coalition => coalition.MemberPlayerIds).Distinct()];

            if (observingPlayerIds.Length == 0)
            {
                // Nobody left to see anything - a Game of one, say. Every probe still gets its (empty) answer.
                return ServiceResult<IReadOnlyDictionary<long, IReadOnlyCollection<WitnessingCoalition>>>.Succeed(Unseen(query.Probes));
            }

            CampaignMapGeometry? geometry = await sightDataReader.GetGeometryAsync(context, query.GameId, cancelToken);

            if (geometry is null)
            {
                string error = $"Game {query.GameId} was not found while looking for witnesses.";
                logger.LogWarning(error);
                return ServiceResult<IReadOnlyDictionary<long, IReadOnlyCollection<WitnessingCoalition>>>.NotFound(error);
            }

            // The inversion. Nothing further from a probed tile than the furthest any of these Players could see can have seen it, so only a square of that
            // radius round each distinct probed tile is searched for Observers.
            int sightReach = await sightDataReader.GetMaxSightReachAsync(context, query.GameId, observingPlayerIds, query.PlayerTurnId, cancelToken);

            HashSet<int> searchedTileIds =
            [
                .. query.Probes
                    .Select(probe => (probe.Location.X, probe.Location.Y))
                    .Distinct()
                    .SelectMany(location => SightRadius.GetSquare(new Coord(location.X, location.Y), sightReach, geometry))
                    .Select(geometry.GetCampaignMapId)
            ];

            IReadOnlyCollection<SightObserver> candidateObservers = await sightDataReader.GetObserversOnTilesAsync(
                context,
                query.GameId,
                observingPlayerIds,
                query.PlayerTurnId,
                searchedTileIds,
                cancelToken);

            // Each candidate's view is worked out once, by the same service the live map read uses, and reused for every probe it covers.
            ServiceResult<IReadOnlyCollection<ObserverFieldOfView>> fieldsOfViewResult = await fieldOfViewService.GetFieldOfViewAsync(
                context,
                query.GameId,
                geometry,
                candidateObservers,
                cancelToken);

            if (fieldsOfViewResult.HasSucceeded == false)
            {
                const string error = "Error working out what the Observers near the probed tiles could see.";
                logger.LogWarning(error);
                return ServiceResult<IReadOnlyDictionary<long, IReadOnlyCollection<WitnessingCoalition>>>.Fail(fieldsOfViewResult.Errors.Prepend(error));
            }

            // Only probes about a Force need its Visibility; a tile is always seen in full.
            int[] probedTemplateIds = [.. query.Probes.Where(probe => probe.Force is not null).Select(probe => probe.Force!.UnitTemplateId).Distinct()];

            IReadOnlyDictionary<int, long> visibilityByTemplateId = probedTemplateIds.Length == 0
                ? new Dictionary<int, long>()
                : await sightDataReader.GetTemplateVisibilityAsync(context, probedTemplateIds, cancelToken);

            IReadOnlyDictionary<long, IReadOnlyCollection<WitnessingCoalition>> answers = WitnessResolution.Resolve(
                query.PlayerTurnId,
                query.Probes,
                fieldsOfViewResult.SuccessValue!,
                observingCoalitions,
                visibilityByTemplateId,
                geometry);

            return ServiceResult<IReadOnlyDictionary<long, IReadOnlyCollection<WitnessingCoalition>>>.Succeed(answers);
        }
        catch (Exception ex)
        {
            return LogAndFail(ex, query);
        }
    }

    /// <summary>
    /// An empty answer for every probe.
    /// </summary>
    private static Dictionary<long, IReadOnlyCollection<WitnessingCoalition>> Unseen(IEnumerable<WitnessProbe> probes)
    {
        return probes.ToDictionary(probe => probe.Key, IReadOnlyCollection<WitnessingCoalition> (_) => []);
    }

    private ServiceResult<IReadOnlyDictionary<long, IReadOnlyCollection<WitnessingCoalition>>> LogAndFail(Exception ex, WitnessQuery query)
    {
        const string error = "An error occurred while looking for witnesses.";
        logger.LogError(ex, "{Error} Game Id {GameId}, Player Turn Id {PlayerTurnId}.", error, query.GameId, query.PlayerTurnId);
        return ServiceResult<IReadOnlyDictionary<long, IReadOnlyCollection<WitnessingCoalition>>>.Fail(error + " Exception: " + ex.Message);
    }
}
