using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Features.Game.Handlers;
using AscendantEmpire.Application.Helpers;
using AscendantEmpire.BackendContracts.CampaignMaps;
using AscendantEmpire.BackendContracts.CampaignMaps.GetPlayerCampaignMap;
using AscendantEmpire.BackendContracts.Games.GetOpenGames;
using AscendantEmpire.Domain.Enums;
using AscendantEmpire.Domain.Enums.Maps;

using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.Maps.CampaignMaps.Handlers;

/// <summary>
/// One or more requests to get the viewing radiuses which entities can see on the Campaign Map.
/// </summary>
public record GetCampaignMapTileRadiusRequest(
    int GameId,
    int PlayerId,
    IReadOnlyCollection<MapObserver> MapObservers
) : IRequest<ServiceResult<IDictionary<(int, RecordType), MapObserverOriginLocation>>>;

internal class GetCampaignMapTileRadiusHandler(
    ILogger<GetCampaignMapTileRadiusHandler> logger,
    IMediator mediator
) : IRequestHandler<GetCampaignMapTileRadiusRequest, ServiceResult<IDictionary<(int, RecordType), MapObserverOriginLocation>>>
{
    public async Task<ServiceResult<IDictionary<(int, RecordType), MapObserverOriginLocation>>> Handle(
        GetCampaignMapTileRadiusRequest request,
        CancellationToken cancelToken)
    {
        ServiceResult<IReadOnlyCollection<OpenGame>> gameInfoResult = await mediator.Send(new GetOpenGamesRequest(request.GameId, request.PlayerId), cancelToken);

        if (gameInfoResult.HasSucceeded == false)
        {
            const string error = "Error getting game info while trying to get a Player's view of the Campaign Map";
            logger.LogWarning(error);
            return ServiceResult<IDictionary<(int, RecordType), MapObserverOriginLocation>>.Fail(gameInfoResult.Errors.Prepend(error));
        }

        OpenGame? gameInfo = gameInfoResult.SuccessValue!.SingleOrDefault();

        // Pull out wrapping and dimensions
        int mapOffset = gameInfo.Players.Single().CampaignMapOffsetId;
        //int mapOffsetDelta = mapProps.MapOffsetDelta;
        int mapWidth = gameInfo.MapWidth;
        int mapHeight = gameInfo.MapHeight;
        bool hasHorizontalWrapping = gameInfo.HasHorizontalWrapping;
        bool hasVerticalWrapping = gameInfo.HasVerticalWrapping;

        var returnData = new Dictionary<(int id, RecordType RecordType), MapObserverOriginLocation>();


        foreach (var viewer in request.MapObservers)
        {
            var sightMode = viewer.SightDirection;

            if (viewer.SightRange < 0) // No sight, quick return
            {
                continue;
            }

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
                Id = viewer.Id,
                RecordTypeId = viewer.RecordType,
                OccupiedCoord = originCoord,
                SightRange = viewer.SightRange,
                SightChildren = []
            };

            int originX = originCoord.X;
            int originY = originCoord.Y;
            int viewRadius = viewer.SightRange;

            List<((int X, int Y) Child, (int X, int Y) Parent)> coordPairs = [];
            HashSet<(int X, int Y)> coordSet = [];
            List<SightCoord> allCoordinateList = [];

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
                    if (distance > viewRadius)
                    {
                        continue;
                    }

                   /* // NEW: if not Normal, filter tiles by direction from origin (using wrapped deltas)
                    if (sightMode != DetectionDirection.Normal && distance != 0)
                    {
                        int dx = hasHorizontalWrapping
                            ? WrappedDelta(originX, wrappedX, mapWidth)
                            : (wrappedX - originX);

                        int dy = hasVerticalWrapping
                            ? WrappedDelta(originY, wrappedY, mapHeight)
                            : (wrappedY - originY);

                        var dirFlags = GetDirectionFlags(dx, dy);

                        // include tile if ANY of its direction components match the requested flags
                        if ((sightMode & dirFlags) == 0)
                            continue;
                    }*/

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

            returnData[(viewer.Id, viewer.RecordType)] = viewerLocation;
        }

        return ServiceResult<IDictionary<(int, RecordType), MapObserverOriginLocation>>.Succeed(returnData);
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

    static DetectionDirection GetDirectionFlags(int dx, int dy)
    {
        DetectionDirection flags = DetectionDirection.None;

        if (dy < 0)
        {
            flags |= DetectionDirection.North;
        }
        else if (dy > 0)
        {
            flags |= DetectionDirection.South;
        }

        if (dx > 0)
        {
            flags |= DetectionDirection.East;
        }
        else if (dx < 0)
        {
            flags |= DetectionDirection.West;
        }

        return flags;
    }
}
