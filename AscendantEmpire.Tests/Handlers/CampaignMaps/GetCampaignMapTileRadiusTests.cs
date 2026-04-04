using AscendantEmpire.Application.Handlers.Game;
using AscendantEmpire.Application.Handlers.Maps.CampaignMaps;
using AscendantEmpire.BackendContracts.CampaignMaps;
using AscendantEmpire.BackendContracts.CampaignMaps.GetPlayerCampaignMap;
using AscendantEmpire.BackendContracts.Games.GetOpenGames;
using AscendantEmpire.Domain.Enums;

using MediatR;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Tests.Handlers.CampaignMaps;

[TestFixture]
internal class GetCampaignMapTileRadiusTests
{
    private GetCampaignMapTileRadiusHandler _handler = null!;
    private IMediator _mediator = null!;

    private const int MapWidth = 8;
    private const int MapHeight = 10;
    private const int MapOffset = 1;

    [SetUp]
    public void Setup()
    {
        _mediator = Substitute.For<IMediator>();

        // Handler doesn't actually use scopeFactory, but it requires one in ctor.
        // Provide a minimal substitute.
        _handler = new GetCampaignMapTileRadiusHandler(
            logger: new NullLogger<GetCampaignMapTileRadiusHandler>(),
            mediator: _mediator
        );
    }

    // ----------------------------
    // 1) In centre, no wrapping
    // ----------------------------
    [Test, Description("In Centre, No wrapping")]
    [TestCase(3, 5, 0, Description = "A - Radius 0")]
    [TestCase(3, 5, 1, Description = "B - Radius 1")]
    [TestCase(3, 5, 2, Description = "C - Radius 2")]
    [TestCase(3, 5, 3, Description = "D - Radius 3")]
    public async Task Test1_RadiusInCentre_NoWrap(int originX, int originY, int radius)
    {
        ArrangeOpenGame(horizontalWrap: false, verticalWrap: false);

        var occupied = new Coord { X = originX, Y = originY };

        var request = new GetCampaignMapTileRadiusRequest(
            GameId: 1,
            PlayerId: 1,
            MapObservers:
            [
                new MapObserver
                {
                    Id = 1,
                    RecordType = RecordType.None,
                    Location = occupied,
                    SightRange = radius
                }
            ]);

        var result = await _handler.Handle(request, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.True, string.Join(" | ", result.Errors ?? []));
            Assert.That(result.Errors, Is.Empty);

            Assert.That(result.SuccessValue, Is.Not.Null);
        }

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.SuccessValue!, Has.Count.EqualTo(1));
            Assert.That(result.SuccessValue.ContainsKey((1, RecordType.None)), Is.True);
        }

        var view = result.SuccessValue[(1, RecordType.None)];
        using (Assert.EnterMultipleScope())
        {
            Assert.That(view.Id, Is.EqualTo(1));
            Assert.That(view.SightRange, Is.EqualTo(radius));
            Assert.That(view.OccupiedCoord.X, Is.EqualTo(originX));
            Assert.That(view.OccupiedCoord.Y, Is.EqualTo(originY));
        }

        var actual = ExtractAllCoordsExceptOccupied(view);

        // Expected: square within bounds, no wrapping, excluding occupied
        var expected = new List<Coord>();

        for (int y = originY - radius; y <= originY + radius; y++)
        {
            for (int x = originX - radius; x <= originX + radius; x++)
            {
                if (x < 0 || x >= MapWidth || y < 0 || y >= MapHeight)
                    continue;

                if (x == originX && y == originY)
                    continue;

                expected.Add(new Coord { X = x, Y = y });
            }
        }

        Assert.That(actual, Is.EquivalentTo(expected));
    }

    // ----------------------------
    // 2) Vertical wrap on top
    // ----------------------------
    [Test, Description("Vertical Wrap - origin at top of map")]
    [TestCase(1, Description = "A - At top of map, Radius 1")]
    [TestCase(2, Description = "B - At top of map, Radius 2")]
    public async Task Test2_VerticalWrap_Top(int radius)
    {
        ArrangeOpenGame(horizontalWrap: false, verticalWrap: true);

        int originX = 3;
        int originY = 0;
        var occupied = new Coord { X = originX, Y = originY };

        var request = new GetCampaignMapTileRadiusRequest(
            GameId: 1,
            PlayerId: 1,
            MapObservers:
            [
                new MapObserver
                {
                    Id = 1,
                    RecordType = RecordType.None,
                    Location = occupied,
                    SightRange = radius
                }
            ]);

        var result = await _handler.Handle(request, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.True, string.Join(" | ", result.Errors ?? []));
            Assert.That(result.Errors, Is.Empty);
        }

        var view = result.SuccessValue![(1, RecordType.None)];
        var actual = ExtractAllCoordsExceptOccupied(view);

        // Expected: square, X in-bounds, Y wrapped, excluding occupied
        var expected = new List<Coord>();
        for (int y = originY - radius; y <= originY + radius; y++)
        {
            for (int x = originX - radius; x <= originX + radius; x++)
            {
                if (x < 0 || x >= MapWidth)
                    continue;

                int wrappedY = Wrap(y, MapHeight);

                if (x == originX && wrappedY == originY)
                    continue;

                expected.Add(new Coord { X = x, Y = wrappedY });
            }
        }

        Assert.That(actual, Is.EquivalentTo(expected));
    }

    // ----------------------------
    // 3) Vertical wrap on bottom
    // ----------------------------
    [Test, Description("Vertical Wrap - origin at bottom of map")]
    [TestCase(2, Description = "A - At bottom of map, Radius 2")]
    public async Task Test3_VerticalWrap_Bottom(int radius)
    {
        ArrangeOpenGame(horizontalWrap: false, verticalWrap: true);

        int originX = 3;
        int originY = MapHeight - 1; // 9
        var occupied = new Coord { X = originX, Y = originY };

        var request = new GetCampaignMapTileRadiusRequest(
            GameId: 1,
            PlayerId: 1,
            MapObservers:
            [
                new MapObserver
                {
                    Id = 1,
                    RecordType = RecordType.None,
                    Location = occupied,
                    SightRange = radius
                }
            ]);

        var result = await _handler.Handle(request, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.True, string.Join(" | ", result.Errors ?? []));
            Assert.That(result.Errors, Is.Empty);
        }

        var view = result.SuccessValue![(1, RecordType.None)];
        var actual = ExtractAllCoordsExceptOccupied(view);

        var expected = new List<Coord>();
        for (int y = originY - radius; y <= originY + radius; y++)
        {
            for (int x = originX - radius; x <= originX + radius; x++)
            {
                if (x < 0 || x >= MapWidth)
                    continue;

                int wrappedY = Wrap(y, MapHeight);

                if (x == originX && wrappedY == originY)
                    continue;

                expected.Add(new Coord { X = x, Y = wrappedY });
            }
        }

        Assert.That(actual, Is.EquivalentTo(expected));
    }

    // ----------------------------
    // 4) Horizontal wrap on left
    // ----------------------------
    [Test, Description("Horizontal Wrap - origin at left of map")]
    [TestCase(2, Description = "A - At left of map, Radius 2")]
    public async Task Test4_HorizontalWrap_Left(int radius)
    {
        ArrangeOpenGame(horizontalWrap: true, verticalWrap: false);

        int originX = 0;
        int originY = 4;
        var occupied = new Coord { X = originX, Y = originY };

        var request = new GetCampaignMapTileRadiusRequest(
            GameId: 1,
            PlayerId: 1,
            MapObservers:
            [
                new MapObserver
                {
                    Id = 1,
                    RecordType = RecordType.None,
                    Location = occupied,
                    SightRange = radius
                }
            ]);

        var result = await _handler.Handle(request, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.True, string.Join(" | ", result.Errors ?? []));
            Assert.That(result.Errors, Is.Empty);
        }

        var view = result.SuccessValue![(1, RecordType.None)];
        var actual = ExtractAllCoordsExceptOccupied(view);

        var expected = new List<Coord>();
        for (int y = originY - radius; y <= originY + radius; y++)
        {
            for (int x = originX - radius; x <= originX + radius; x++)
            {
                if (y < 0 || y >= MapHeight)
                    continue;

                int wrappedX = Wrap(x, MapWidth);

                if (wrappedX == originX && y == originY)
                    continue;

                expected.Add(new Coord { X = wrappedX, Y = y });
            }
        }

        Assert.That(actual, Is.EquivalentTo(expected));
    }

    // ----------------------------
    // 5) Horizontal wrap on right
    // ----------------------------
    [Test, Description("Horizontal Wrap - origin at right of map")]
    [TestCase(2, Description = "A - At right of map, Radius 2")]
    public async Task Test5_HorizontalWrap_Right(int radius)
    {
        ArrangeOpenGame(horizontalWrap: true, verticalWrap: false);

        int originX = MapWidth - 1; // 7
        int originY = 4;
        var occupied = new Coord { X = originX, Y = originY };

        var request = new GetCampaignMapTileRadiusRequest(
            GameId: 1,
            PlayerId: 1,
            MapObservers:
            [
                new MapObserver
                {
                    Id = 1,
                    RecordType = RecordType.None,
                    Location = occupied,
                    SightRange = radius
                }
            ]);

        var result = await _handler.Handle(request, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.True, string.Join(" | ", result.Errors ?? []));
            Assert.That(result.Errors, Is.Empty);
        }

        var view = result.SuccessValue![(1, RecordType.None)];
        var actual = ExtractAllCoordsExceptOccupied(view);

        var expected = new List<Coord>();
        for (int y = originY - radius; y <= originY + radius; y++)
        {
            for (int x = originX - radius; x <= originX + radius; x++)
            {
                if (y < 0 || y >= MapHeight)
                    continue;

                int wrappedX = Wrap(x, MapWidth);

                if (wrappedX == originX && y == originY)
                    continue;

                expected.Add(new Coord { X = wrappedX, Y = y });
            }
        }

        Assert.That(actual, Is.EquivalentTo(expected));
    }

    // ----------------------------
    // 6) Both wraps in top-left
    // ----------------------------
    [Test, Description("Horizontal and Vertical Wrap - top-left corner")]
    [TestCase(1, Description = "A - In top left corner, Radius 1")]
    public async Task Test6_BothWraps_TopLeft(int radius)
    {
        ArrangeOpenGame(horizontalWrap: true, verticalWrap: true);

        int originX = 0;
        int originY = 0;
        var occupied = new Coord { X = originX, Y = originY };

        var request = new GetCampaignMapTileRadiusRequest(
            GameId: 1,
            PlayerId: 1,
            MapObservers:
            [
                new MapObserver
                {
                    Id = 1,
                    RecordType = RecordType.None,
                    Location = occupied,
                    SightRange = radius
                }
            ]);

        var result = await _handler.Handle(request, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.True, string.Join(" | ", result.Errors ?? []));
            Assert.That(result.Errors, Is.Empty);
        }

        var view = result.SuccessValue![(1, RecordType.None)];
        var actual = ExtractAllCoordsExceptOccupied(view);

        var expected = new List<Coord>();
        for (int y = originY - radius; y <= originY + radius; y++)
        {
            for (int x = originX - radius; x <= originX + radius; x++)
            {
                int wrappedX = Wrap(x, MapWidth);
                int wrappedY = Wrap(y, MapHeight);

                if (wrappedX == originX && wrappedY == originY)
                    continue;

                expected.Add(new Coord { X = wrappedX, Y = wrappedY });
            }
        }

        Assert.That(actual, Is.EquivalentTo(expected));
    }

    // ----------------------------
    // 7) Top-left corner, no wrap
    // ----------------------------
    [Test, Description("At top-left, no wrap")]
    [TestCase(0, 0, 1, Description = "Radius 1")]
    public async Task Test7_TopLeft_NoWrap(int originX, int originY, int radius)
    {
        ArrangeOpenGame(horizontalWrap: false, verticalWrap: false);

        var occupied = new Coord { X = originX, Y = originY };

        var request = new GetCampaignMapTileRadiusRequest(
            GameId: 1,
            PlayerId: 1,
            MapObservers:
            [
                new MapObserver
                {
                    Id = 1,
                    RecordType = RecordType.None,
                    Location = occupied,
                    SightRange = radius
                }
            ]);

        var result = await _handler.Handle(request, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.True, string.Join(" | ", result.Errors ?? []));
            Assert.That(result.Errors, Is.Empty);
        }

        var view = result.SuccessValue![(1, RecordType.None)];
        var actual = ExtractAllCoordsExceptOccupied(view);

        // For origin (0,0) radius=1 no wrap => possible coords are (0,0)(1,0)(0,1)(1,1) excluding occupied -> (1,0)(0,1)(1,1)
        var expected = new List<Coord>
        {
            new() { X = 1, Y = 0 },
            new() { X = 0, Y = 1 },
            new() { X = 1, Y = 1 }
        };

        Assert.That(actual, Is.EquivalentTo(expected));
    }

    // ----------------------------
    // Helpers
    // ----------------------------
    private void ArrangeOpenGame(bool horizontalWrap, bool verticalWrap)
    {
        var openGame = new OpenGame
        {
            GameId = 1,
            GameName = "Test Game",
            NumberOfPlayers = 1,
            MapWidth = MapWidth,
            MapHeight = MapHeight,
            HasHorizontalWrapping = horizontalWrap,
            HasVerticalWrapping = verticalWrap,
            Players =
            [
                new OpenGamePlayer
                {
                    PlayerId = 1,
                    RulerTitle = "King",
                    RulerName = "Test",
                    FactionName = "Faction",
                    RulerLabel = "King Test of Faction",
                    CampaignMapOffsetId = MapOffset
                }
            ]
        };

        _mediator.Send(Arg.Any<GetOpenGamesRequest>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(ServiceResult<IReadOnlyCollection<OpenGame>>.Succeed([openGame])));
    }

    private static int Wrap(int value, int size) => ((value % size) + size) % size;

    private static List<Coord> ExtractAllCoordsExceptOccupied(MapObserverOriginLocation view)
    {
        // The handler returns origin's *children* (distance >= 1) in SightChildren.
        // Flatten the whole tree to coords and compare against expected, excluding occupied.
        Coord occupied = view.OccupiedCoord.ToCoord;

        var all = new List<Coord>();

        foreach (var child in view.SightChildren ?? Enumerable.Empty<SightCoord>())
        {
            all.AddRange(FlattenSightCoordsToCoords(child));
        }

        return [.. all.Where(c => !(c.X == occupied.X && c.Y == occupied.Y))];
    }

    private static IEnumerable<Coord> FlattenSightCoordsToCoords(SightCoord root)
    {
        yield return root.ToCoord;

        if (root.Children == null)
        {
            yield break;
        }

        foreach (var child in root.Children)
        {
            foreach (var descendant in FlattenSightCoordsToCoords(child))
            {
                yield return descendant;
            }
        }
    }
}
