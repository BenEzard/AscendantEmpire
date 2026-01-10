using AcendantEmpire.BackendContracts.Biomes;

using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Handlers.Game;
using AscendantEmpire.Application.Handlers.Maps.Biomes;
using AscendantEmpire.Application.Handlers.Maps.Geographies;
using AscendantEmpire.BackendContracts.CampaignMap;
using AscendantEmpire.BackendContracts.Games.GetOpenGames;
using AscendantEmpire.BackendContracts.Geographies;
using AscendantEmpire.Domain.Enums.Maps;

using MediatR;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using System.Data;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Handlers.Maps.CampaignMaps;

public record GetPlayerCampaignMapRequest(Guid GameGuid, Guid PlayerGuid, IReadOnlyCollection<MapObserver>? Observers) 
    : IRequest<ServiceResult<IReadOnlyCollection<ObservedCampaignTile>>>;

internal partial class GetPlayerCampaignMapHandler(
    ILogger<GetPlayerCampaignMapHandler> logger,
    IMediator mediator,
    AscendantEmpireContext context,
     IServiceScopeFactory scopeFactory
    ) : IRequestHandler<GetPlayerCampaignMapRequest, ServiceResult<IReadOnlyCollection<ObservedCampaignTile>>>
{
    public async Task<ServiceResult<IReadOnlyCollection<ObservedCampaignTile>>> Handle(GetPlayerCampaignMapRequest request, CancellationToken cancelToken)
    {
        try
        {
            IReadOnlyCollection<MapObserver>? mutatedObservers = request.Observers;

            ServiceResult<IReadOnlyCollection<OpenGame>> gameInfoResult = await mediator.Send(new GetOpenGamesRequest(request.GameGuid, request.PlayerGuid), cancelToken);

            if (gameInfoResult.HasSucceeded == false)
            {
                const string error = "Error getting game info while trying to get a Player's view of the Campaign Map";
                logger.LogWarning(error);
                return ServiceResult<IReadOnlyCollection<ObservedCampaignTile>>.Fail(gameInfoResult.Errors.Prepend(error));
            }

            OpenGame? gameInfo = gameInfoResult.SuccessValue!.SingleOrDefault();

            if (gameInfo == null)
            {
                const string error = "Game not found while trying to get a Player's view of the Campaign Map";
                logger.LogWarning(error);
                return ServiceResult<IReadOnlyCollection<ObservedCampaignTile>>.NotFound(error);
            }

            Task<List<InternalMasterObservedTile>> fetchMasterView;
            Task<List<InternalPlayerObservedTile>> fetchPlayerView;
            OpenGamePlayer thisPlayer = gameInfo.Players.Single();
            IEnumerable<ObservedCampaignTile> observedTiles = [];

            // If no observers are provided, return the full map
            if (request.Observers == null)
            {
                // TODO: Update. If we're loading the whole map, then that means it's the start of the game which means we don't need to load the whole
                // master map. Can we instead use an initialisation method?
                fetchMasterView = GetMasterTilesInScopeAsync(gameInfo, cancelToken);

                fetchPlayerView = GetPlayerTilesInScopeAsync(gameInfo, thisPlayer.PlayerId, cancelToken);

                Task<ServiceResult<IReadOnlyCollection<CampaignMapBiome>>> fetchBiomes = mediator.Send(new GetCampaignMapBiomesRequest(), cancelToken);
                Task<ServiceResult<IReadOnlyCollection<CampaignMapGeography>>> fetchGeographies = mediator.Send(new GetCampaignMapGeographiesRequest(), cancelToken);

                await Task.WhenAll(fetchMasterView, fetchPlayerView, fetchBiomes, fetchGeographies);

                observedTiles = from masterTile in fetchPlayerView.Result
                                                              select new ObservedCampaignTile
                                                              {
                                                                  XCoord = masterTile.XCoord,
                                                                  YCoord = masterTile.YCoord,
                                                                  BiomeTypeId = masterTile.BiomeTypeId,
                                                                  CampaignMapGeographyTypeId = masterTile.GeographyTypeId,
                                                              };

                //var masterView = fetchMasterView.Result;


                //var biomes = fetchBiomes.Result.ToDictionary(b => b.BiomeTypeId, b => b);
                //var geographies = fetchGeographies.Result.ToDictionary(g => g.CampaignMapGeographyTypeId, g => g);

                //var playerView = fetchPlayerView.Result;
            }
          

                // Given gameInfo.CampaignMapOffsetId (the 0,0 position of the map), and the width and height of the map, calculate what the record ID will be
                //var enhancedCoordLookup = request.Observers.ToDictionary(o => o.TemporaryObserverId, o => o.Location.ToEnhancedCoord(gameInfo.Players.Single().CampaignMapOffsetId, gameInfo.MapWidth));




            return ServiceResult<IReadOnlyCollection<ObservedCampaignTile>>.Succeed([.. observedTiles]);
        }
        catch (Exception ex)
        {
            const string error = "An error occurred while retrieving the Player's Campaign Map.";
            logger.LogError(ex, error);
            return ServiceResult<IReadOnlyCollection<ObservedCampaignTile>>.Fail(error);
        }
    }

    private async Task<List<InternalMasterObservedTile>> GetMasterTilesInScopeAsync(OpenGame gameInfo, CancellationToken cancelToken)
    {
        using var scope = scopeFactory.CreateScope();
        using var newContext = scope.ServiceProvider.GetRequiredService<AscendantEmpireContext>();

        return await newContext.CampaignMaps
            .Where(cm => cm.GameId == gameInfo.GameId)
            .Select(cm => new InternalMasterObservedTile
            {
                CampaignMapId = cm.CampaignMapId,
                XCoord = cm.XCoord,
                YCoord = cm.YCoord,
                BiomeTypeId = cm.BiomeTypeId,
                GeographyTypeId = cm.CampaignMapGeographyTypeId,
                FirstSeenByPlayerId = cm.FirstSeenByPlayerId,
                LastVisitedByPlayerId = cm.LastVisitedByPlayerId,
                LastVisitedOnTurnId = cm.LastVisitedOnTurnId,
            })
            .ToListAsync(cancelToken);

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


    static int Wrap(int value, int size) => ((value % size) + size) % size;

    static int WrappedDelta(int from, int to, int size)
    {
        int diff = to - from;

        if (Math.Abs(diff) > size / 2)
        {
            diff = diff > 0 ? diff - size : diff + size;
        }

        return diff;
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
}
