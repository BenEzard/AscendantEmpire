using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.ForceOrders.Movement.Strategies;
using AscendantEmpire.Application.Features.Maps.CampaignMaps.Classes;
using AscendantEmpire.BackendContracts.CampaignMaps;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.Maps.CampaignMaps.Services;

/// <inheritdoc cref="IFieldOfViewService" />
internal class FieldOfViewService(
    ILogger<FieldOfViewService> logger,
    IDbContextFactory<AscendantEmpireContext> dbFactory,
    ISightDataReader sightDataReader,
    IEnumerable<IMovementDomainStrategy> movementDomainStrategies) : IFieldOfViewService
{
    /// <summary>
    /// Every registered movement domain strategy, materialised once. The blinded reveal asks them what an Observer could not have walked onto; an empty set
    /// is not an error, it just reveals nothing.
    /// </summary>
    private readonly IReadOnlyCollection<IMovementDomainStrategy> _movementDomainStrategies = [.. movementDomainStrategies];

    /// <inheritdoc />
    public async Task<ServiceResult<IReadOnlyCollection<ObserverFieldOfView>>> GetPlayerFieldOfViewAsync(int gameId, int playerId, CampaignMapGeometry geometry, CancellationToken cancelToken = default)
    {
        ArgumentNullException.ThrowIfNull(geometry);

        try
        {
            await using AscendantEmpireContext context = await dbFactory.CreateDbContextAsync(cancelToken);

            IReadOnlyCollection<SightObserver> observers = await sightDataReader.GetPlayerObserversAsync(context, gameId, playerId, cancelToken);

            return await GetFieldOfViewAsync(context, gameId, geometry, observers, cancelToken);
        }
        catch (Exception ex)
        {
            const string error = "An error occurred while working out what a Player's Observers can see.";
            logger.LogError(ex, "{Error} Game Id {GameId}, Player Id {PlayerId}.", error, gameId, playerId);
            return ServiceResult<IReadOnlyCollection<ObserverFieldOfView>>.Fail(error + " Exception: " + ex.Message);
        }
    }

    /// <inheritdoc />
    public async Task<ServiceResult<IReadOnlyCollection<ObserverFieldOfView>>> GetFieldOfViewAsync(
        AscendantEmpireContext context,
        int gameId,
        CampaignMapGeometry geometry,
        IReadOnlyCollection<SightObserver> observers,
        CancellationToken cancelToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(geometry);
        ArgumentNullException.ThrowIfNull(observers);

        try
        {
            // A negative Sight is not an Observer at all - not even of its own tile - so it is dropped before any terrain is read for it.
            List<SightObserver> activeObservers = [.. observers.Where(observer => observer.BaseSightRange >= 0)];

            if (activeObservers.Count == 0)
            {
                return ServiceResult<IReadOnlyCollection<ObserverFieldOfView>>.Succeed([]);
            }

            // Pass 1: the tiles the Observers stand on. Their Biome and elevation decide how far each can see, so nothing else can be known until these are in.
            IReadOnlyDictionary<(int X, int Y), SightTerrain> occupiedTerrain = await sightDataReader.GetTerrainAsync(
                context,
                gameId,
                activeObservers.Select(observer => geometry.GetCampaignMapId(observer.Location)),
                cancelToken);

            Dictionary<(int X, int Y), SightTerrain> terrainByCoord = new(occupiedTerrain);

            // Pass 2: everything within each Observer's resolved range, and the neighbours of any that are blinded, less what pass 1 already brought back.
            HashSet<int> footprintTileIds = [];

            foreach (SightObserver observer in activeObservers)
            {
                if (terrainByCoord.TryGetValue((observer.Location.X, observer.Location.Y), out SightTerrain? occupiedTile) == false)
                {
                    // Not reachable in play: every Force and Structure stands on a tile of its own Game. It is logged rather than thrown so one bad row costs
                    // that Observer its view, not the whole Player their map.
                    logger.LogWarning(
                        "Observer {RecordType} {ObserverId} stands at {Location}, which is not a tile of Game {GameId}; it sees nothing.",
                        observer.RecordType,
                        observer.Id,
                        observer.Location,
                        gameId);
                    continue;
                }

                foreach (Coord coord in FieldOfView.GetFootprint(observer, occupiedTile, geometry))
                {
                    if (terrainByCoord.ContainsKey((coord.X, coord.Y)) == false)
                    {
                        footprintTileIds.Add(geometry.GetCampaignMapId(coord));
                    }
                }
            }

            if (footprintTileIds.Count > 0)
            {
                IReadOnlyDictionary<(int X, int Y), SightTerrain> footprintTerrain = await sightDataReader.GetTerrainAsync(context, gameId, footprintTileIds, cancelToken);

                foreach (KeyValuePair<(int X, int Y), SightTerrain> entry in footprintTerrain)
                {
                    terrainByCoord[entry.Key] = entry.Value;
                }
            }

            // The rules themselves. An Observer that sees nothing is one whose own tile was missing above; anything genuinely observing sees at least that tile.
            List<ObserverFieldOfView> fieldsOfView =
            [
                .. activeObservers
                    .Select(observer => new ObserverFieldOfView(observer, FieldOfView.ComputeSeenTiles(observer, terrainByCoord, geometry, _movementDomainStrategies)))
                    .Where(view => view.SeenTiles.Count > 0)
            ];

            return ServiceResult<IReadOnlyCollection<ObserverFieldOfView>>.Succeed(fieldsOfView);
        }
        catch (Exception ex)
        {
            const string error = "An error occurred while working out what a set of Observers can see.";
            logger.LogError(ex, "{Error} Game Id {GameId}.", error, gameId);
            return ServiceResult<IReadOnlyCollection<ObserverFieldOfView>>.Fail(error + " Exception: " + ex.Message);
        }
    }
}
