using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Handlers.Game;
using AscendantEmpire.Application.Handlers.Maps.CampaignMaps.Extensions;
using AscendantEmpire.BackendContracts.CampaignMaps;
using AscendantEmpire.BackendContracts.CampaignMaps.GetPlayerCampaignMap;
using AscendantEmpire.BackendContracts.Games.GetOpenGames;
using AscendantEmpire.Domain.Enums.Maps;

using FluentValidation;

using MediatR;

using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using System.Data;
using System.Text.Json;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Handlers.Maps.CampaignMaps;

public record GetPlayerCampaignMapRequest(Guid GameGuid, Guid PlayerGuid, IReadOnlyCollection<MapObserver>? Observers, bool LoadWholeMap = false) 
    : IRequest<ServiceResult<IReadOnlyCollection<ObserverSight>>>;

internal class PlayerCampaignMapRequestValidator : AbstractValidator<GetPlayerCampaignMapRequest>
{
    public PlayerCampaignMapRequestValidator()
    {
        RuleFor(r => r.GameGuid)
            .NotEmpty().WithMessage("GameGuid is required.");
        RuleFor(r => r.PlayerGuid)
            .NotEmpty().WithMessage("PlayerGuid is required.");
        RuleFor(r => r.GameGuid)
            .Must(guid => guid != Guid.Empty)
            .WithMessage("GameGuid must be a valid non-empty GUID.");
        RuleFor(r => r.PlayerGuid)
            .Must(guid => guid != Guid.Empty)
            .WithMessage("PlayerGuid must be a valid non-empty GUID.");

        When(r => r.Observers is not null, () =>
        {
            RuleFor(r => r.Observers)
                .Must(observers => observers.Select(o => o.TemporaryObserverId).All(id => id > 0))
                .WithMessage("All Observers must have TemporaryObserverId > 0.");

            RuleFor(r => r.Observers)
                .Must(observers => observers.Select(o => o.TemporaryObserverId).Distinct().Count() == observers.Count)
                .WithMessage("All Observers must have unique TemporaryObserverId values.");

            RuleForEach(r => r.Observers)
                .Must(o => o.Location.IsValid())
                .WithMessage("Observer Location X and Y must be >= 0.");

            RuleForEach(r => r.Observers)
                .Must(o => o.SightRange >= -1)
                .WithMessage("Observer SightRange must be >= -1.");
        });
    }
}

internal class GetPlayerCampaignMapHandler(
    ILogger<GetPlayerCampaignMapHandler> logger,
    IMediator mediator,
    IDbContextFactory<AscendantEmpireContext> dbFactory,
    IServiceScopeFactory scopeFactory
    ) : IRequestHandler<GetPlayerCampaignMapRequest, ServiceResult<IReadOnlyCollection<ObserverSight>>>
{
    public async Task<ServiceResult<IReadOnlyCollection<ObserverSight>>> Handle(GetPlayerCampaignMapRequest request, CancellationToken cancelToken)
    {
        List<ObserverSight> observers = [];
        Dictionary<int, MapObserverSight> mapSightByTemporaryId = [];

        try
        {
            // Validate the request object
            var validator = new PlayerCampaignMapRequestValidator();

            if (validator.Validate(request) is not { IsValid: true })
            {
                var errors = validator.Validate(request).Errors.Select(e => e.ErrorMessage);
                const string error = "Validation failed for GetPlayerCampaignMapRequest.";
                logger.LogWarning("{Error} Errors: {Errors}", error, string.Join("; ", errors));
                return ServiceResult<IReadOnlyCollection<ObserverSight>>.Fail(errors.Prepend(error));
            }

            // Load the game info
            ServiceResult<IReadOnlyCollection<OpenGame>> gameInfoResult = await mediator.Send(new GetOpenGamesRequest(request.GameGuid, request.PlayerGuid), cancelToken);

            if (gameInfoResult.HasSucceeded == false)
            {
                const string error = "Error getting game info while trying to get a Player's view of the Campaign Map";
                logger.LogWarning(error);
                return ServiceResult<IReadOnlyCollection<ObserverSight>>.Fail(gameInfoResult.Errors.Prepend(error));
            }

            OpenGame? gameInfo = gameInfoResult.SuccessValue!.SingleOrDefault();

            if (gameInfo == null)
            {
                const string error = "Game not found while trying to get a Player's view of the Campaign Map";
                logger.LogWarning(error);
                return ServiceResult<IReadOnlyCollection<ObserverSight>>.NotFound(error);
            }

            OpenGamePlayer thisPlayer = gameInfo.Players.Single();

            if (request.Observers is not null)
            {
                // Add the DbIds to the Observers
                List<EnhancedMapObserver> enhancedObservers = EnhanceObserverCoordinates(mapOffset: thisPlayer.CampaignMapOffsetId, gameInfo.MapWidth, request.Observers);

                // Load the Occupied Tiles first, to see if there are any Bonuses/Penalties on the Occupied Tile that will affect the Sight Radius
                enhancedObservers = await UpdateObserverSightValuesAsync(enhancedObservers, cancelToken);

                // Calculate the MAXIMUM Sight Radius for each Observer
                ServiceResult<IDictionary<int, MapObserverOriginLocation>> radiusDetails = await mediator.Send(new GetCampaignMapTileRadiusRequest(
                    request.GameGuid, 
                    request.PlayerGuid, 
                    MapObservers: enhancedObservers), cancelToken);

                cancelToken.ThrowIfCancellationRequested();

                // Load all of the Player Observed Tiles which *MAY* be visible within the Sight Radius of the Observers
                // (Exception: the Occupied Tile, which is loaded from the World Map directly)
                IReadOnlyCollection<PlayerCampaignMapTileForSight> tiles = await GetTilesForSightBigRequestAsync(radiusDetails.SuccessValue!.Values.SelectMany(o => o.GetDbIds()), cancelToken);

                cancelToken.ThrowIfCancellationRequested();

                // Filter down the Tiles based on those that can be seen; excluding those from the radius which are blocked by Geography or Biome features
                List<MapObserverSight> mapSight = DetermineMapSight(radiusDetails.SuccessValue, tiles.ToDictionary(t => t.PlayerCampaignMapId));
                mapSightByTemporaryId = mapSight.ToDictionary(ms => ms.TemporaryObserverId);

                List<int> playerCampaignMapIds = mapSight.GetAllDbIds();

                if (playerCampaignMapIds.Count > 0)
                {
                    // Note that these operations must occur in SEQUENCE.
                    await using AscendantEmpireContext context = await dbFactory.CreateDbContextAsync(cancelToken);

                    // Get a summary of the number of Tiles which are newly discovered *FOR THE PLAYER* by Biome
                    // TODO: Use this data to add Biome expertise points to the Player's Faction
                    IReadOnlyCollection<PlayerDiscoveredTileSummary> newlyDiscoveredTilesForPlayer = await CountNewlyDiscoveredTilesByBiomeAsync(context, playerCampaignMapIds, cancelToken);

                    int turnId = 1; // TODO replace/update when turn system is in place

                    // Update the PlayerCampaignMap with Biome/Geography info, and FirstSeen/LastSeen turn info
                    Task updatePlayerMapTask = UpdatePlayerCampaignMapFromCampaignMapAsync(playerCampaignMapIds, turnId, cancelToken);

                    // Update the CampaignMap with FirstSeenByPlayer/FirstSeenOnTurn info
                    // TODO: Use this data to add to Exploration achievements for the Player
                    Task<int> updateCampaignMap = UpdateCampaignMapFirstSeenAsync(playerCampaignMapIds, thisPlayer.PlayerId, turnId, cancelToken);

                    await Task.WhenAll(updatePlayerMapTask, updateCampaignMap);

                    int firstDiscoveredByPlayer = updateCampaignMap.Result;
                }
            }

            if (request.LoadWholeMap)
            {
                List<InternalPlayerObservedTile> fetchPlayerView = await GetPlayerTilesInScopeAsync(gameInfo, thisPlayer.PlayerId, cancelToken);

                observers.Add(new ObserverSight()
                {
                    TemporaryObserverId = 0,
                    Location = new Coord { X = 0, Y = 0 },
                    SeenTiles = [.. from masterTile in fetchPlayerView
                                    // TODO improve by excluding already discovered Tiles
                                    //where playerCampaignMapIds.Contains(new Coord { X = masterTile.XCoord, Y = masterTile.YCoord }).GetDbId(thisPlayer.CampaignMapOffsetId, gameInfo.MapWidth)) == false
                                 select new ObservedCampaignTile
                                 {
                                     XCoord = masterTile.XCoord,
                                     YCoord = masterTile.YCoord,
                                     BiomeTypeId = masterTile.BiomeTypeId,
                                     CampaignMapGeographyTypeId = masterTile.GeographyTypeId,
                                 }]
                });
            }
            else
            {
                observers.AddRange([.. request.Observers.Select( o => new ObserverSight
                {
                    TemporaryObserverId = o.TemporaryObserverId,
                    Location = o.Location,
                    SeenTiles = [.. mapSightByTemporaryId[o.TemporaryObserverId].SeenTiles
                        .Select(st => new ObservedCampaignTile
                        {
                            XCoord = st.Coord.X,
                            YCoord = st.Coord.Y,
                            BiomeTypeId = st.BiomeType,
                            CampaignMapGeographyTypeId = st.GeographyType
                        })]
                })]);
            }
        }
        catch (Exception ex)
        {
            const string error = "An error occurred while retrieving the Player's Campaign Map.";
            logger.LogError(ex, error);
            return ServiceResult<IReadOnlyCollection<ObserverSight>>.Fail(error);
        }

        return ServiceResult<IReadOnlyCollection<ObserverSight>>.Succeed(observers);
    }

    /// <summary>
    /// Count the number of Tiles which haven't been seen yet by the Player, by the actual CampaignMap Biome.
    /// </summary>
    private static async Task<IReadOnlyCollection<PlayerDiscoveredTileSummary>> CountNewlyDiscoveredTilesByBiomeAsync(
        AscendantEmpireContext context,
        IEnumerable<int> playerCampaignMapIds,
        CancellationToken cancelToken = default)
    {
        ArgumentNullException.ThrowIfNull(playerCampaignMapIds);
        
        if (playerCampaignMapIds.Any() == false)
        {
            return [];
        }

        return await (from pm in context.PlayerCampaignMaps.AsNoTracking()
            join cm in context.CampaignMaps.AsNoTracking()
                on pm.CampaignMapId equals cm.CampaignMapId
            where playerCampaignMapIds.Contains(pm.PlayerCampaignMapId)
               && pm.FirstSeenOnTurnId == null
            group pm by cm.BiomeTypeId into g
            select new PlayerDiscoveredTileSummary
            {
                BiomeType = g.Key,
                Count = g.Count()
            }).ToListAsync(cancelToken);
    }

    /// <summary>
    /// Update the PlayerCampaignMap entries to reflect the current state of the CampaignMap entries.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Copy BiomeTypeId + CampaignMapGeographyTypeId from CampaignMap</item>
    /// <item>Set FirstSeenOnTurnId if null</item>
    /// <item>Set LastSeenOnTurnId always</item>
    /// </list>
    /// </remarks>
    private async Task UpdatePlayerCampaignMapFromCampaignMapAsync(
        IEnumerable<int> playerCampaignMapIds,
        int turnId,
        CancellationToken cancelToken = default)
    {
        ArgumentNullException.ThrowIfNull(playerCampaignMapIds);

        if (playerCampaignMapIds.Any() == false)
        {
            return;
        }

        await using AscendantEmpireContext context = await dbFactory.CreateDbContextAsync(cancelToken);

        // Correlated subqueries for copying values from CampaignMap.
        // This translates to SQL with scalar subqueries and works well for set-based updates.
        await context.PlayerCampaignMaps
            .Where(pm => playerCampaignMapIds.Contains(pm.PlayerCampaignMapId))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(
                    pm => pm.BiomeTypeId,
                    pm => context.CampaignMaps
                             .Where(cm => cm.CampaignMapId == pm.CampaignMapId)
                             .Select(cm => cm.BiomeTypeId)
                             .First())
                .SetProperty(
                    pm => pm.CampaignMapGeographyTypeId,
                    pm => context.CampaignMaps
                             .Where(cm => cm.CampaignMapId == pm.CampaignMapId)
                             .Select(cm => cm.CampaignMapGeographyTypeId)
                             .First())
                .SetProperty(
                    pm => pm.FirstSeenOnTurnId,
                    pm => pm.FirstSeenOnTurnId ?? turnId)
                .SetProperty(
                    pm => pm.LastSeenOnTurnId,
                    pm => turnId),
                cancelToken);
    }

    private record class PlayerDiscoveredTileSummary 
    {
        public BiomeType BiomeType { get; init; }
        public int Count { get; init; }
    }

    /// <summary>
    /// Update CampaignMap "first discoverer" fields for specified Tiles.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Set FirstSeenOnTurnId, if null</item>
    /// <item>Set FirstSeenByPlayerId, if null</item>
    /// </list>
    /// </remarks>
    /// <returns>Number of Tiles updated.</returns>
    public async Task<int> UpdateCampaignMapFirstSeenAsync(
        IEnumerable<int> playerCampaignMapIds,
        int playerId,
        int turnId,
        CancellationToken cancelToken = default)
    {
        ArgumentNullException.ThrowIfNull(playerCampaignMapIds);

        if (playerCampaignMapIds.Any() == false)
        {
            return 0;
        }

        await using AscendantEmpireContext context = await dbFactory.CreateDbContextAsync(cancelToken);

        var cmIdsQuery =
            context.PlayerCampaignMaps
               .Where(pm => playerCampaignMapIds.Contains(pm.PlayerCampaignMapId))
               .Select(pm => pm.CampaignMapId)
               .Distinct();

        return await context.CampaignMaps
            .Where(cm => cmIdsQuery.Contains(cm.CampaignMapId)
                      && cm.FirstSeenOnTurnId == null)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(cm => cm.FirstSeenOnTurnId, cm => cm.FirstSeenOnTurnId ?? turnId)
                .SetProperty(cm => cm.FirstSeenByPlayerId, cm => cm.FirstSeenByPlayerId ?? playerId),
                cancelToken);
    }

    private async Task<List<InternalPlayerObservedTile>> GetPlayerTilesInScopeAsync(OpenGame gameInfo, int playerId, CancellationToken cancelToken)
    {
        using var scope = scopeFactory.CreateScope();
        using var newContext = scope.ServiceProvider.GetRequiredService<AscendantEmpireContext>();

        return await newContext.PlayerCampaignMaps
            .Where(pcm => pcm.GameId == gameInfo.GameId)
            .Where(pcm => pcm.PlayerId == playerId)
            .Select(cm => new InternalPlayerObservedTile
            {
                CampaignMapId = cm.CampaignMapId,
                XCoord = cm.XCoord,
                YCoord = cm.YCoord,
                BiomeTypeId = cm.BiomeTypeId,
                GeographyTypeId = cm.CampaignMapGeographyTypeId,
                FirstSeenByPlayerOnTurnId = cm.FirstSeenOnTurnId,
                LastSeenByPlayerOnTurnId = cm.LastSeenOnTurnId,
            })
            .ToListAsync(cancelToken);
    }

    /// <summary>
    /// Enhances the coordinates of the Map Observers by adding the db id.
    /// </summary>
    private static List<EnhancedMapObserver> EnhanceObserverCoordinates(
        int mapOffset,
        int mapWidth,
        IReadOnlyCollection<MapObserver> observers)
    {
        return [.. (from mx in observers
                     select new EnhancedMapObserver()
                     {
                         TemporaryObserverId = mx.TemporaryObserverId,
                         Location = mx.Location,
                         DbId = mx.Location.GetDbId(mapOffset, mapWidth),
                         SightRange = mx.SightRange
                     })];
    }


    /// <summary>
    /// Update the Observer Sight values based on the occupied Tiles bonuses or penalties.
    /// </summary>
    /// <returns>The Observer collection, updated</returns>
    private async Task<List<EnhancedMapObserver>> UpdateObserverSightValuesAsync(
        List<EnhancedMapObserver> enhancedObservers,
        CancellationToken cancelToken)
    {
        // Get the list of dbIds
        int[] dbIds = [.. enhancedObservers.Select(uc => uc.DbId)]; 

        await using var context = await dbFactory.CreateDbContextAsync(cancelToken);

        var occupiedTiles = await context.PlayerCampaignMaps
            .AsNoTracking()
            .Include(t => t.Biome)
            .Include(t => t.Geography)
            .Include(t => t.CampaignMap)
            .Where(t => dbIds.Contains(t.PlayerCampaignMapId))
            .Select(t => new
            {
                t.PlayerCampaignMapId,
                // Get the Tiles where the Observers are. This must be from the WORLD map, not the Player map, because the Player may not have seen the Tile yet
                // (e.g. at the start of the game).
                t.CampaignMap.Biome.DoesBlockSight,
                t.CampaignMap.Geography.RelativeElevation
            })
            .ToListAsync(cancelToken);

        // Regenerate the MapViewer request information with updated Sight values
        List<EnhancedMapObserver> data = [.. (from o in enhancedObservers
                   join t in occupiedTiles on o.DbId equals t.PlayerCampaignMapId
                   select new EnhancedMapObserver()
                   {
                        TemporaryObserverId = o.TemporaryObserverId,
                        DbId = o.DbId,
                        Location = o.Location,
                        SightRange = t.DoesBlockSight 
                            ? 0 // if DoesBlockSight, apply the penalty (SightRange reduced to 0)
                            : t.RelativeElevation <= 0
                                ? o.SightRange // if RelativeElevation <= 0, no change
                                : o.SightRange + t.RelativeElevation // otherwise add the bonus
                   })];

        return data;
    }

    private async Task<IReadOnlyCollection<PlayerCampaignMapTileForSight>> GetTilesForSightBigRequestAsync(
       IEnumerable<int> tileIds,
       CancellationToken cancelToken)
    {
        await using var context = await dbFactory.CreateDbContextAsync(cancelToken);

        string jsonIds = JsonSerializer.Serialize(tileIds.Distinct());

        // Raw SQL just to extract valid IDs
        string idQuery = @"SELECT Id FROM OPENJSON(@json) WITH (Id INT '$')";

        var filteredIds = await context.Database
            .SqlQueryRaw<int>(idQuery, new SqlParameter("@json", jsonIds))
            .ToListAsync(cancelToken);

        if (filteredIds.Count == 0)
        {
            return [];
        }

        // Now retrieve the tiles with related data
        List<PlayerCampaignMapTileForSight> tiles = await context.PlayerCampaignMaps
            .Where(t => filteredIds.Contains(t.PlayerCampaignMapId))
            .Include(t => t.Biome)
            .Include(t => t.Geography)
            .Include(t => t.CampaignMap)
            .AsNoTracking()
            .Select(pcmt => new PlayerCampaignMapTileForSight
            {
                PlayerCampaignMapId = pcmt.PlayerCampaignMapId,
                XCoord = pcmt.XCoord,
                YCoord = pcmt.YCoord,
                CampaignMapId = pcmt.CampaignMapId,
                PlayerBiomeType = pcmt.Biome.BiomeTypeId,
                BiomeType = pcmt.CampaignMap.BiomeTypeId,
                BiomeDoesBlockSight = pcmt.CampaignMap.Biome.DoesBlockSight,
                PlayerGeographyType = pcmt.Geography.CampaignMapGeographyTypeId,
                GeographyType = pcmt.CampaignMap.CampaignMapGeographyTypeId,
                RelativeElevation = pcmt.CampaignMap.Geography.RelativeElevation,
                PlayerFirstSeenOnTurnId = pcmt.FirstSeenOnTurnId,
                PlayerLastSeenOnTurnId = pcmt.LastSeenOnTurnId,
                FirstSeenOnTurnId = pcmt.CampaignMap.FirstSeenOnTurnId,
                FirstSeenByPlayerId = pcmt.CampaignMap.FirstSeenByPlayerId
            })
            .ToListAsync(cancelToken);

        return [.. tiles];
    }

    private record EnhancedMapObserver : MapObserver
    {
        public int DbId { get; set; }
    }

    private class InternalMasterObservedTile
    {
        public int CampaignMapId { get; set; }
        public int XCoord { get; set; }
        public int YCoord { get; set; }
        public BiomeType BiomeTypeId { get; set; }
        public CampaignMapGeographyType GeographyTypeId { get; set; }
        public int? FirstSeenByPlayerId { get; set; }
        public int? LastVisitedByPlayerId { get; set; }
        public int? LastVisitedOnTurnId { get; set; }
    }

    private class InternalPlayerObservedTile
    {
        public int CampaignMapId { get; set; }
        public int XCoord { get; set; }
        public int YCoord { get; set; }
        public BiomeType BiomeTypeId { get; set; }
        public CampaignMapGeographyType GeographyTypeId { get; set; }
        public int? FirstSeenByPlayerOnTurnId { get; set; }
        public int? LastSeenByPlayerOnTurnId { get; set; }
    }

    private class PlayerCampaignMapTileForSight
    {
        public int PlayerCampaignMapId { get; set; }
        public int XCoord { get; set; }
        public int YCoord { get; set; }
        public int CampaignMapId { get; set; }
        public BiomeType PlayerBiomeType { get; set; }
        public BiomeType BiomeType { get; set; }
        public bool BiomeDoesBlockSight { get; set; }
        public CampaignMapGeographyType PlayerGeographyType { get; set; }
        public CampaignMapGeographyType GeographyType { get; set; }
        public int RelativeElevation { get; set; }
        public int? PlayerFirstSeenOnTurnId { get; set; }
        public int? PlayerLastSeenOnTurnId { get; set; }
        public int? FirstSeenOnTurnId { get; set; }
        public int? FirstSeenByPlayerId { get; set; }
    }

    /// <summary>
    /// Determines which tiles are visible to each Observer based on their Sight Radius and the Tiles' properties.
    /// </summary>
    private static List<MapObserverSight> DetermineMapSight(
        IDictionary<int, MapObserverOriginLocation> radiusDetails,
        IDictionary<int, PlayerCampaignMapTileForSight> tiles)
    {
        List<MapObserverSight> allObservers = [];

        foreach (var origin in radiusDetails.Values)
        {
            tiles.TryGetValue(origin.OccupiedCoord.DbId, out PlayerCampaignMapTileForSight? tile);

            var observer = new MapObserverSight
            {
                TemporaryObserverId = origin.TemporaryObserverId,
                OccupiedCoord = origin.OccupiedCoord,
                SeenTiles = [new SeenTile
                {
                    Id = tile.PlayerCampaignMapId,
                    Coord = new Coord { X = tile.XCoord, Y = tile.YCoord },
                    BiomeType = tile.BiomeType,
                    GeographyType = tile.GeographyType
                }]
            };

            allObservers.Add(observer);

            // Nothing to do if no sight range or no directions
            if (origin.SightRange <= 0)
            {
                continue;
            }

            if (origin.SightChildren == null || origin.SightChildren.Count == 0)
            {
                continue;
            }

            // We need the origin tile for elevation comparisons on the first step.
            tiles.TryGetValue(origin.OccupiedCoord.DbId, out PlayerCampaignMapTileForSight? originTile);

            // Prevent duplicates in case your graph converges or repeats.
            HashSet<int> seenPlayerCampaignMapIds = [];
            HashSet<int> visitedDbIds = [];

            foreach (var firstStep in origin.SightChildren)
            {
                WalkSightBranch(
                    node: firstStep,
                    parentTile: originTile,
                    remainingRange: origin.SightRange,
                    observer: observer,
                    tiles: tiles,
                    seenPlayerCampaignMapIds: seenPlayerCampaignMapIds,
                    visitedDbIds: visitedDbIds);
            }
        }

        return allObservers;
    }

    private static void WalkSightBranch(
        SightCoord node,
        PlayerCampaignMapTileForSight? parentTile,
        int remainingRange,
        MapObserverSight observer,
        IDictionary<int, PlayerCampaignMapTileForSight> tiles,
        HashSet<int> seenPlayerCampaignMapIds,
        HashSet<int> visitedDbIds)
    {
        if (remainingRange <= 0)
        {
            return;
        }

        // Prevent cycles / repeated traversal of the same node in this observer’s walk.
        if (visitedDbIds.Add(node.DbId) == false)
        {
            return;
        }

        if (tiles.TryGetValue(node.DbId, out PlayerCampaignMapTileForSight? tile) == false)
        {
            return;
        }

        // Mark this tile visible
        if (seenPlayerCampaignMapIds.Add(tile.PlayerCampaignMapId))
        {
            observer.SeenTiles.Add(new SeenTile
            {
                Id = tile.PlayerCampaignMapId,
                Coord = new Coord { X = tile.XCoord, Y = tile.YCoord },
                BiomeType = tile.BiomeType,
                GeographyType = tile.GeographyType
            });
        }

        bool blocksSight = tile.BiomeDoesBlockSight;
        bool elevationBlocks = parentTile != null && tile.RelativeElevation > parentTile.RelativeElevation;

        if (blocksSight || elevationBlocks)
        {
            return;
        }

        int nextRemaining = remainingRange - 1;
        if (nextRemaining <= 0)
        {
            return;
        }

        if (node.Children == null || node.Children.Count == 0)
        {
            return;
        }

        foreach (var child in node.Children)
        {
            WalkSightBranch(
                node: child,
                parentTile: tile,
                remainingRange: nextRemaining,
                observer: observer,
                tiles: tiles,
                seenPlayerCampaignMapIds: seenPlayerCampaignMapIds,
                visitedDbIds: visitedDbIds);
        }
    }

    public class MapObserverSight
    {
        public required int TemporaryObserverId { get; set; }
        public required EnhancedCoord OccupiedCoord { get; set; }
        public required List<SeenTile> SeenTiles { get; set; }
    }

    public class SeenTile
    {
        public required int Id { get; set; }
        public required ICoord Coord { get; set; }
        public required BiomeType BiomeType { get; set; }
        public required CampaignMapGeographyType GeographyType { get; set; }
    }
}
