using AscendantEmpire.Application.Handlers.Game;
using AscendantEmpire.BackendContracts.CampaignMaps;
using AscendantEmpire.BackendContracts.Games.GetOpenGames;

using MediatR;

using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Handlers.Maps.CampaignMaps;

/// <summary>
/// One or more requests to get the viewing radiuses which entities can see on the Campaign Map.
/// </summary>
public record GetCampaignMapTileRadiusRequest(
    Guid GameGuid,
    Guid PlayerGuid,
    IReadOnlyCollection<MapObserver> MapObservers
) : IRequest<ServiceResult<IDictionary<int, MapObserverOriginLocation>>>;

internal class GetCampaignMapTileRadiusHandler(
    ILogger<GetCampaignMapTileRadiusHandler> logger,
    IMediator mediator
) : IRequestHandler<GetCampaignMapTileRadiusRequest, ServiceResult<IDictionary<int, MapObserverOriginLocation>>>
{
    public async Task<ServiceResult<IDictionary<int, MapObserverOriginLocation>>> Handle(
        GetCampaignMapTileRadiusRequest request,
        CancellationToken cancelToken)
    {
        ServiceResult<IReadOnlyCollection<OpenGame>> gameInfoResult = await mediator.Send(new GetOpenGamesRequest(request.GameGuid, request.PlayerGuid), cancelToken);

        if (gameInfoResult.HasSucceeded == false)
        {
            const string error = "Error getting game info while trying to get a Player's view of the Campaign Map";
            logger.LogWarning(error);
            return ServiceResult<IDictionary<int, MapObserverOriginLocation>>.Fail(gameInfoResult.Errors.Prepend(error));
        }

        OpenGame? gameInfo = gameInfoResult.SuccessValue!.SingleOrDefault();

        // Pull out wrapping and dimensions
        int mapOffset = gameInfo.Players.Single().CampaignMapOffsetId;
        //int mapOffsetDelta = mapProps.MapOffsetDelta;
        int mapWidth = gameInfo.MapWidth;
        int mapHeight = gameInfo.MapHeight;
        bool hasHorizontalWrapping = gameInfo.HasHorizontalWrapping;
        bool hasVerticalWrapping = gameInfo.HasVerticalWrapping;

        var returnData = new Dictionary<int, MapObserverOriginLocation>();

        foreach (var viewer in request.MapObservers)
        {
            // The origin Coord
            var originCoord = new SightCoord
            {
                X = viewer.Location.X,
                Y = viewer.Location.Y,
                DbId = viewer.Location.GetDbId(mapOffset, mapWidth),
                Radius = 0
            };

            var viewerLocation = new MapObserverOriginLocation
            {
                TemporaryObserverId = viewer.TemporaryObserverId,
                OccupiedCoord = originCoord,
                SightRange = viewer.SightRange,
                SightChildren = []
            };

            int originX = originCoord.X;
            int originY = originCoord.Y;
            int viewRadius = viewer.SightRange;

            List<((int X, int Y) Child, (int X, int Y) Parent)> coordPairs = new();
            HashSet<(int X, int Y)> coordSet = new();
            List<SightCoord> allCoordinateList = new();

            for (int y = originY - viewRadius; y <= originY + viewRadius; y++)
            {
                for (int x = originX - viewRadius; x <= originX + viewRadius; x++)
                {
                    // Compute wrapped coordinates
                    int wrappedX = hasHorizontalWrapping ? Wrap(x, mapWidth) : x;
                    int wrappedY = hasVerticalWrapping ? Wrap(y, mapHeight) : y;

                    // Skip out-of-range if no wrap
                    if ((hasHorizontalWrapping == false && (wrappedX < 0 || wrappedX >= mapWidth)) 
                        || (hasVerticalWrapping == false && (wrappedY < 0 || wrappedY >= mapHeight)))
                    {
                        continue;
                    }

                    // Chebyshev distance from origin (without wrap)
                    int distance = Math.Max(Math.Abs(x - originX), Math.Abs(y - originY));
                    if (distance > viewRadius) continue;

                    // Create child tile
                    SightCoord childTile = new()
                    {
                        X = wrappedX,
                        Y = wrappedY,
                        DbId = MapMethods.GetDbId(mapOffset, mapWidth, wrappedX, wrappedY),
                        Radius = distance
                    };

                    if (coordSet.Add((childTile.X, childTile.Y)))
                    {
                        allCoordinateList.Add(childTile);
                    }

                    if (distance >= 1)
                    {
                        // Compute parent coordinate by stepping toward origin on wrapped grid:
                        int wrappedDeltaX = WrappedDelta(originX, wrappedX, mapWidth);
                        int wrappedDeltaY = WrappedDelta(originY, wrappedY, mapHeight);
                        int parentX = wrappedX - Math.Sign(wrappedDeltaX);
                        int parentY = wrappedY - Math.Sign(wrappedDeltaY);

                        if (hasHorizontalWrapping)
                        {
                            parentX = Wrap(parentX, mapWidth);
                        }
                        if (hasVerticalWrapping)
                        {
                            parentY = Wrap(parentY, mapHeight);
                        }

                        SightCoord parentCoord = new()
                        {
                            X = parentX,
                            Y = parentY,
                            DbId = MapMethods.GetDbId(mapOffset, mapWidth, parentX, parentY),
                            Radius = distance - 1
                        };

                        if (coordSet.Add((parentCoord.X, parentCoord.Y)))
                        {
                            allCoordinateList.Add(parentCoord);
                        }

                        coordPairs.Add(((childTile.X, childTile.Y), (parentCoord.X, parentCoord.Y)));
                    }
                }
            }

            // Create a dictionary for fast lookup by (X, Y)
            Dictionary<(int X, int Y), SightCoord> coordDict = allCoordinateList.ToDictionary(c => (c.X, c.Y));

            // Group child coordinate tuples by parent coordinate tuple
            ILookup<(int X, int Y), (int X, int Y)> parentToChildrenLookup = coordPairs.ToLookup(p => p.Parent, p => p.Child);

            var parentChildMap = parentToChildrenLookup
                .Select(g => new
                {
                    Parent = coordDict[g.Key],
                    Children = g.Select(childTuple => coordDict[childTuple]).ToList()
                });

            // Assign children to the Parent's Children property
            foreach (var item in parentChildMap)
            {
                if (coordDict.TryGetValue((item.Parent.X, item.Parent.Y), out var parentCoord))
                {
                    parentCoord.Children = item.Children;
                }
            }

            // Once all tiles processed, store the viewer’s result
            viewerLocation.SightChildren = [.. parentChildMap
                .Where(d => d.Parent.X == viewerLocation.OccupiedCoord.X && d.Parent.Y == viewerLocation.OccupiedCoord.Y)
                .SelectMany(x => x.Children)];

            returnData[viewer.TemporaryObserverId] = viewerLocation;
        }

        return ServiceResult<IDictionary<int, MapObserverOriginLocation>>.Succeed(returnData);
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
}
