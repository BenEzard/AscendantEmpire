using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Common.Validation;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Game.Handlers;
using AscendantEmpire.Application.Features.Maps.CampaignMaps.Classes;
using AscendantEmpire.Application.Features.Maps.CampaignMaps.Services;
using AscendantEmpire.Application.Features.Structures.Handlers;
using AscendantEmpire.Application.Features.Turns.Services;
using AscendantEmpire.Application.Features.Units.Handlers;
using AscendantEmpire.BackendContracts.CampaignMaps;
using AscendantEmpire.BackendContracts.CampaignMaps.GetPlayerCampaignMap;
using AscendantEmpire.BackendContracts.Games.GetGameContext;
using AscendantEmpire.BackendContracts.Players.GetPlayerStructures;
using AscendantEmpire.BackendContracts.Units.GetPlayerForces;
using AscendantEmpire.Domain.Enums;
using AscendantEmpire.Domain.Enums.Maps;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.Maps.CampaignMaps.Handlers;

public record GetPlayerCampaignMapRequest(int GameId, int PlayerId, bool LoadWholeMap = false)
    : IRequest<ServiceResult<IReadOnlyCollection<ObserverSight>>>;

internal class PlayerCampaignMapRequestValidator : Validator<GetPlayerCampaignMapRequest>
{
    public PlayerCampaignMapRequestValidator()
    {
        RuleFor(r => r.GameId)
            .GreaterThan(0).WithMessage("GameId is required.");
        RuleFor(r => r.PlayerId)
            .GreaterThan(0).WithMessage("PlayerId is required.");
    }
}

/// <summary>
/// A Player's view of the campaign map: what their Observers can see right now, and — on request — the ground they remember.
/// </summary>
/// <remarks>
/// <para>
/// This read owns three things: stamping what the Player just saw as explored, finding the Structures and Forces standing on it, and shaping the response.
/// It does <b>not</b> own the sight rules. Those are computed by <see cref="IFieldOfViewService"/> over <see cref="FieldOfView"/>, which the intelligence
/// witness service uses too, so live vision and intelligence cannot drift apart.
/// </para>
/// <para>
/// See <c>docs/fog-of-war.md</c> for how the response is drawn, and <c>docs/design/campaign-map/vision.md</c> for the vision rules themselves.
/// </para>
/// </remarks>
internal class GetPlayerCampaignMapHandler(
    ILogger<GetPlayerCampaignMapHandler> logger,
    IMediator mediator,
    IDbContextFactory<AscendantEmpireContext> dbFactory,
    IServiceScopeFactory scopeFactory,
    ICampaignTurnService campaignTurnService,
    IFieldOfViewService fieldOfViewService,
    ICampaignRiverReader campaignRiverReader,
    ICampaignOwnershipReader campaignOwnershipReader
    ) : IRequestHandler<GetPlayerCampaignMapRequest, ServiceResult<IReadOnlyCollection<ObserverSight>>>
{
    public async Task<ServiceResult<IReadOnlyCollection<ObserverSight>>> Handle(GetPlayerCampaignMapRequest request, CancellationToken cancelToken)
    {
        List<ObserverSight> observers = [];

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
        ServiceResult<IReadOnlyCollection<GameContext>> gameInfoResult = await mediator.Send(new GetGameContextRequest(request.GameId, request.PlayerId), cancelToken);

        if (gameInfoResult.HasSucceeded == false)
        {
            const string error = "Error getting game info while trying to get a Player's view of the Campaign Map";
            logger.LogWarning(error);
            return ServiceResult<IReadOnlyCollection<ObserverSight>>.Fail(gameInfoResult.Errors.Prepend(error));
        }

        GameContext? gameInfo = gameInfoResult.SuccessValue!.SingleOrDefault();

        if (gameInfo == null)
        {
            const string error = "Game not found while trying to get a Player's view of the Campaign Map";
            logger.LogWarning(error);
            return ServiceResult<IReadOnlyCollection<ObserverSight>>.NotFound(error);
        }

        GameContextPlayer thisPlayer = gameInfo.Players.Single();
        CampaignMapGeometry geometry = CampaignMapGeometry.From(gameInfo);

        // What each of *THIS* Player's Observers - their Structures and Forces - can see. The sight rules live in FieldOfView, shared with the witness
        // service; everything below is only what this read does with the answer.
        ServiceResult<IReadOnlyCollection<ObserverFieldOfView>> fieldOfViewResult = await fieldOfViewService.GetPlayerFieldOfViewAsync(request.GameId, thisPlayer.PlayerId, geometry, cancelToken);

        if (fieldOfViewResult.HasSucceeded == false)
        {
            const string error = "Error working out what a Player's Observers can see while trying to get a Player's view of the Campaign Map";
            logger.LogWarning(error);
            return ServiceResult<IReadOnlyCollection<ObserverSight>>.Fail(fieldOfViewResult.Errors.Prepend(error));
        }

        IReadOnlyCollection<ObserverFieldOfView> fieldsOfView = fieldOfViewResult.SuccessValue!;

        cancelToken.ThrowIfCancellationRequested();

        // The rivers on every Tile an Observer can see. Read once for the whole set rather than per Tile, and separately from the terrain the sight rules
        // return, because a river is not a property of a Tile: it sits on an edge that the Tile above or to the left of it owns.
        IReadOnlyDictionary<Coord, TileRivers> riversByCoord = await GetTileRiversAsync(
            request.GameId,
            [.. fieldsOfView.SelectMany(view => view.SeenTiles).Select(tile => new Coord(tile.X, tile.Y)).Distinct()],
            geometry,
            cancelToken);

        // The world-map id of every Tile in view. Structures and Forces are stored against the world map, so these look them up directly.
        List<int> campaignMapTileIds = [.. fieldsOfView.SelectMany(view => view.SeenTiles).Select(tile => tile.CampaignMapId).Distinct()];

        // Who holds each Tile in sight. Read once for the whole set like the rivers above, and for the Tiles in sight only: a border is current information rather than
        // terrain, so the ground the Player merely remembers reports none - see ObservedCampaignTile.Ownership.
        IReadOnlyDictionary<int, TileOwnership> ownershipByTileId = await GetTileOwnershipAsync(request.GameId, campaignMapTileIds, cancelToken);

        Dictionary<int, List<PlayerStructure>> structuresByTileId = [];
        Dictionary<int, List<PlayerForce>> forcesByTileId = [];

        if (campaignMapTileIds.Count > 0)
        {
            // Get *ALL* Structures and Forces on the observed map (for any Player)
            Task<ServiceResult<IReadOnlyCollection<PlayerStructure>>> fetchAllStructuresTask = mediator.Send(new GetStructuresByLocationRequest(request.GameId, campaignMapTileIds, IncludeBattleMapStructures: false), cancelToken);

            // Forces in battle are included. A Force fighting on a battle map is still standing on its campaign tile - the Battle is fought over that tile - so it is still seen there,
            // and its owner has to be able to click it to reach its Battle button.
            Task<ServiceResult<IReadOnlyCollection<PlayerForce>>> fetchAllForcesTask = mediator.Send(new GetForcesByLocationRequest(request.GameId, campaignMapTileIds, IncludeBattleMapForces: true), cancelToken);

            await Task.WhenAll(fetchAllStructuresTask, fetchAllForcesTask);

            ServiceResult<IReadOnlyCollection<PlayerStructure>> structureResult = await fetchAllStructuresTask;
            ServiceResult<IReadOnlyCollection<PlayerForce>> forceResult = await fetchAllForcesTask;

            if (structureResult.HasSucceeded == false)
            {
                const string error = "Error getting all Structures by location while trying to get a Player's view of the Campaign Map";
                logger.LogWarning(error);
                return ServiceResult<IReadOnlyCollection<ObserverSight>>.Fail(structureResult.Errors.Prepend(error));
            }
            else if (forceResult.HasSucceeded == false)
            {
                const string error = "Error getting all Forces by location while trying to get a Player's view of the Campaign Map";
                logger.LogWarning(error);
                return ServiceResult<IReadOnlyCollection<ObserverSight>>.Fail(forceResult.Errors.Prepend(error));
            }

            structuresByTileId = structureResult.SuccessValue!.GroupBy(s => s.CampaignMapTileId).ToDictionary(g => g.Key, g => g.ToList());
            forcesByTileId = forceResult.SuccessValue!.GroupBy(s => s.CampaignMapTileId).ToDictionary(g => g.Key, g => g.ToList());

            // Translate the world-map tile ids into the player's own map ids, which is what the exploration stamp is written against. Both maps are laid
            // out in the same row-major order, so a tile sits at the same position in each block and the two offsets are all that separate them.
            List<int> playerCampaignMapTileIds = [.. campaignMapTileIds.Select(id => id - gameInfo.CampaignMapOffsetId + thisPlayer.CampaignMapOffsetId)];

            // Note that these operations must occur in SEQUENCE.
            await using AscendantEmpireContext context = await dbFactory.CreateDbContextAsync(cancelToken);

            // Get a summary of the number of Tiles which are newly discovered *FOR THE PLAYER* by Biome
            // TODO: Use this data to add Biome expertise points to the Player's Faction
            IReadOnlyCollection<PlayerDiscoveredTileSummary> newlyDiscoveredTilesForPlayer = await CountNewlyDiscoveredTilesByBiomeAsync(context, playerCampaignMapTileIds, cancelToken);

            // The Turn to stamp on what this Player just saw.
            //
            // Resolved once, and handed to both updates below - they run on separate contexts and must agree about which Turn this read was.
            //
            // TODO: Note this is still a side effect of the *read* rather than of movement. The sight rules no longer live here, but the stamp does, and
            // relocating it onto the movement path is its own piece of work - it changes when ground becomes explored.
            ServiceResult<int> observationTurnResult = await campaignTurnService.GetObservationTurnIdAsync(context, request.GameId, thisPlayer.PlayerId, cancelToken);

            if (observationTurnResult.HasSucceeded == false)
            {
                // Refused before either update below runs, so a Game with no Turn to stamp against gets nothing written to its map rather than a Turn id
                // that means nothing.
                const string error = "Error resolving the Turn to stamp a Player's view of the Campaign Map with";
                logger.LogWarning(error);
                return ServiceResult<IReadOnlyCollection<ObserverSight>>.Fail(observationTurnResult.Errors.Prepend(error));
            }

            int turnId = observationTurnResult.SuccessValue;

            // Update the PlayerCampaignMap with Biome/Geography info, and FirstSeen/LastSeen turn info
            Task updatePlayerMapTask = UpdatePlayerCampaignMapFromCampaignMapAsync(playerCampaignMapTileIds, turnId, cancelToken);

            // Update the CampaignMap with FirstSeenByPlayer/FirstSeenOnTurn info
            // TODO: Use this data to add to Exploration achievements for the Player
            Task<int> updateCampaignMap = UpdateCampaignMapFirstSeenAsync(playerCampaignMapTileIds, thisPlayer.PlayerId, turnId, cancelToken);

            await Task.WhenAll(updatePlayerMapTask, updateCampaignMap);

            int firstDiscoveredByPlayer = updateCampaignMap.Result;
        }

        observers.AddRange([.. fieldsOfView.Select(view => new ObserverSight
        {
            Id = view.Observer.Id,
            RecordType = view.Observer.RecordType,
            Location = view.Observer.Location,
            SeenTiles = [.. view.SeenTiles.Select(seenTile =>
            {
                // structuresByTileId and forcesByTileId are keyed by world-map tile id, which is exactly what the seen tile carries.
                structuresByTileId.TryGetValue(seenTile.CampaignMapId, out List<PlayerStructure>? structuresAtTile);
                forcesByTileId.TryGetValue(seenTile.CampaignMapId, out List<PlayerForce>? forcesAtTile);

                return new ObservedCampaignTile
                {
                    XCoord = seenTile.X,
                    YCoord = seenTile.Y,
                    BiomeTypeId = seenTile.BiomeType,
                    CampaignMapGeographyTypeId = seenTile.GeographyType,
                    // Always explored. A Tile only reaches this branch because an Observer can see it, and seeing it is what explores it — the stamp above
                    // has already written FirstSeenOnTurnId on this very set of Tiles. Reading the flag back off the Tile row instead would report the value
                    // from before that write, so a Tile would come back unexplored on the very read that explored it.
                    IsExplored = true,
                    // Terrain, like the Biome and Geography above, so it rides along with them. Falls back to "no rivers" rather than null: null means
                    // unexplored to the client, and a Tile an Observer is looking at is explored by definition.
                    Rivers = riversByCoord.GetValueOrDefault(new Coord(seenTile.X, seenTile.Y)) ?? TileRivers.None,
                    // Falls back to wilderness rather than null for the same reason the rivers above fall back to None: null means "you are not being told", and a
                    // Tile an Observer is looking at is one the Player is entitled to read the border off. A Tile with no row behind it is absent from the lookup, and
                    // nobody holds a Tile that does not exist.
                    Ownership = ownershipByTileId.GetValueOrDefault(seenTile.CampaignMapId) ?? TileOwnership.Unclaimed,
                    Structures = structuresAtTile?.Select(os => new ObservedStructure
                    {
                        Id = os.StructureInstanceId,
                        RecordType = os.RecordType,
                        OwnerPlayerId = os.OwnerPlayerId,
                        StructureId = os.StructureId,
                        CompletionPercent = os.CompletionPercent,
                        PositionOnTile = os.Position
                    }).ToList() ?? [],
                    Forces = forcesAtTile?.Select(ot => new ObservedForce
                    {
                        Id = ot.UnitInstanceId,
                        RecordType = ot.RecordType,
                        OwnerPlayerId = ot.PlayerId,
                        UnitTemplateId = ot.UnitTemplateId,
                        HealthyTroops = ot.HealthyTroops,
                        InjuredTroops = ot.InjuredTroops,
                        InitialStrength = ot.InitialStrength,
                        // Masked for Forces this player doesn't own: how much movement an enemy has left is intel the vision rules never
                        // granted. Seeing that a Force is standing there is one thing; knowing whether it can still act is another.
                        // The numbers below are masked by the same test and more sharply so — they say exactly how far it could go.
                        HasCampaignMovementRemaining = ot.PlayerId == thisPlayer.PlayerId ? ot.HasCampaignMovementRemaining : null,
                        CampaignMovementRemaining = ot.PlayerId == thisPlayer.PlayerId ? ot.CampaignMovementRemaining : null,
                        CampaignMovementPoints = ot.PlayerId == thisPlayer.PlayerId ? ot.CampaignMovementPoints : null,
                        BattleMovementRemaining = ot.PlayerId == thisPlayer.PlayerId ? ot.BattleMovementRemaining : null,
                    }).ToList() ?? []
                };
            })]
        })]);

        if (request.LoadWholeMap)
        {
            // Get a HashSet of all the coordinates we're already observing, so we don't get them again
            HashSet<(int X, int Y)> alreadyObservedTiles = [.. observers.SelectMany(o => o.SeenTiles).Select(t => (t.XCoord, t.YCoord))];

            List<InternalPlayerObservedTile> fetchPlayerView = await GetPlayerTilesInScopeAsync(gameInfo, thisPlayer.PlayerId, cancelToken);

            // Rivers for the ground the Player remembers, and only that ground. Unexplored Tiles are left out of the request, so they disclose nothing -
            // while an explored Tile beside one still reports the edge the two of them share, because that edge is described by both of them.
            IReadOnlyDictionary<Coord, TileRivers> rememberedRiversByCoord = await GetTileRiversAsync(
                gameInfo.GameId,
                [.. fetchPlayerView
                    .Where(tile => tile.FirstSeenByPlayerOnTurnId != null)
                    .Where(tile => alreadyObservedTiles.Contains((tile.XCoord, tile.YCoord)) == false)
                    .Select(tile => new Coord(tile.XCoord, tile.YCoord))],
                geometry,
                cancelToken);

            var fullMapObserver = new ObserverSight
            {
                Id = 0,
                RecordType = RecordType.None,
                Location = new Coord { X = 0, Y = 0 },
                SeenTiles =
                [
                    .. fetchPlayerView
                    .Where(pt => alreadyObservedTiles.Contains((pt.XCoord, pt.YCoord)) == false) // Exclude Tiles which are already included from the Observers above
                    .Select(masterTile => new ObservedCampaignTile
                    {
                        XCoord = masterTile.XCoord,
                        YCoord = masterTile.YCoord,
                        BiomeTypeId = masterTile.BiomeTypeId,
                        CampaignMapGeographyTypeId = masterTile.GeographyTypeId,
                        // The Player's map holds a row for every Tile in the game from the moment it is set up, so this branch hands back the
                        // whole board — the ground they remember and the ground they have never been to alike. FirstSeenOnTurnId is what
                        // separates the two, and this is the only place in the response where the flag is ever false.
                        IsExplored = masterTile.FirstSeenByPlayerOnTurnId != null,
                        // Null for ground the Player has never been to, which is why the lookup is not defaulted here: an unexplored Tile was never asked
                        // about, and saying "no rivers" would claim knowledge of ground they have not seen.
                        Rivers = rememberedRiversByCoord.GetValueOrDefault(new Coord(masterTile.XCoord, masterTile.YCoord)),
                        Ownership = null, // A border is current information, and the Player has no Observer here to read it off.
                        // Tiles outside an observer's sight are terrain only: the player knows the
                        // ground from having explored it, but not what is standing on it now.
                        Structures = [],
                        Forces = []
                    })
                ]
            };

            // Combine the two sets
            observers = [.. observers, fullMapObserver];
        }

        return ServiceResult<IReadOnlyCollection<ObserverSight>>.Succeed(observers);
    }

    /// <summary>
    /// The four river edges of each of the given Tiles, on a context of this read's own.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A thin wrapper over <see cref="ICampaignRiverReader"/> so both halves of the response - what the Observers see, and the ground the Player remembers -
    /// ask for rivers the same way, on one line each.
    /// </para>
    /// <para>
    /// Only the Tiles handed in are described, which is how fog of war is applied: the callers pass explored ground, and the neighbours whose columns own its
    /// North and West edges are loaded by the reader without ever being reported.
    /// </para>
    /// </remarks>
    private async Task<IReadOnlyDictionary<Coord, TileRivers>> GetTileRiversAsync(
        int gameId,
        IReadOnlyCollection<Coord> coords,
        CampaignMapGeometry geometry,
        CancellationToken cancelToken)
    {
        if (coords.Count == 0)
        {
            return new Dictionary<Coord, TileRivers>();
        }

        await using AscendantEmpireContext context = await dbFactory.CreateDbContextAsync(cancelToken);

        return await campaignRiverReader.GetTileRiversAsync(context, gameId, coords, geometry, cancelToken);
    }

    /// <summary>
    /// Who holds each of the given Tiles, on a context of this read's own.
    /// </summary>
    /// <remarks>
    /// A thin wrapper over <see cref="ICampaignOwnershipReader"/> so the read asks for ownership on one line.
    /// </remarks>
    private async Task<IReadOnlyDictionary<int, TileOwnership>> GetTileOwnershipAsync(
        int gameId,
        IReadOnlyCollection<int> campaignMapIds,
        CancellationToken cancelToken)
    {
        if (campaignMapIds.Count == 0)
        {
            return new Dictionary<int, TileOwnership>();
        }

        await using AscendantEmpireContext context = await dbFactory.CreateDbContextAsync(cancelToken);

        return await campaignOwnershipReader.GetTileOwnershipAsync(context, gameId, campaignMapIds, cancelToken);
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

    private async Task<List<InternalPlayerObservedTile>> GetPlayerTilesInScopeAsync(GameContext gameInfo, int playerId, CancellationToken cancelToken)
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
}
