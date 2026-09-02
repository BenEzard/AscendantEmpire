using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Game.Handlers;
using AscendantEmpire.Application.Features.Maps.CampaignMaps.Handlers;
using AscendantEmpire.Application.Features.Players.Handlers;
using AscendantEmpire.BackendContracts.CampaignMaps.GetPlayerCampaignMap;
using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Entities.Units;
using AscendantEmpire.Domain.Enums;

using MediatR;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AscendantEmpire.Tests.Features.Maps.CampaignMaps;

/// <summary>
/// Integration tests for the <c>IsExplored</c> flag on the campaign map a Player receives.
/// </summary>
/// <remarks>
/// <para>
/// Exploration is the permanent half of the fog of war, and it is a different question from whether a Tile is being watched
/// right now. The client draws a solid fog over ground the Player has never been to and a light haze over ground they explored
/// but have nothing looking at, so these two states have to stay distinguishable in the response or the client collapses them
/// back into one and the whole map greys out behind a Force as soon as it moves. See <c>docs/fog-of-war.md</c>.
/// </para>
/// <para>
/// The map is deliberately much larger than the Observers can see, so there is always ground left unexplored to assert about.
/// </para>
/// <para>
/// Runs at the SqlServer tier: game setup writes both maps with raw SQL, and the sight query uses OPENJSON, neither of which an
/// in-memory provider can execute.
/// </para>
/// </remarks>
[TestFixture]
internal class PlayerCampaignMapExplorationTests : GameSqlServerTestBase
{
    private const int _MAP_WIDTH = 10;
    private const int _MAP_HEIGHT = 8;

    /// <summary>The record type of the whole-map pseudo-observer, which carries remembered terrain rather than live sight.</summary>
    private const RecordType _WHOLE_MAP_OBSERVER = RecordType.None;

    private IMediator _mediator = null!;

    [SetUp]
    public void SetUpMediator()
    {
        // A real mediator, so the request goes through the same handler chain the API uses.
        // GetOpenGamesHandler takes a context directly rather than a factory, hence both registrations.
        ServiceProvider provider = new ServiceCollection()
            .AddSingleton(DbFactory)
            .AddScoped(_ => DbFactory.CreateDbContext())
            .AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<GetPlayersHandler>())
            .AddLogging()
            .BuildServiceProvider();

        _mediator = provider.GetRequiredService<IMediator>();
    }

    /// <summary>Sets up a game on a map big enough that the starting Observers cannot possibly see all of it.</summary>
    private async Task<int> SetupGameAsync()
    {
        await SeedGameAsync(mapWidth: _MAP_WIDTH, mapHeight: _MAP_HEIGHT, numberOfPlayers: 2);

        ServiceResultBase.ServiceResult<int> result = await _service.SetupGameAsync(
            new SetupGameRequest(
                GalleryMapId: _galleryIndexId,
                GameName: "Exploration Game",
                NumberOfPlayers: 2,
                BattleMapSize: 50,
                GenerationSeed: null),
            CancellationToken.None);

        Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));

        return result.SuccessValue;
    }

    private async Task<Player> GetFirstPlayerAsync(int gameId)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        return await context.Players.Where(p => p.GameId == gameId).OrderBy(p => p.Id).FirstAsync();
    }

    private async Task<IReadOnlyCollection<ObserverSight>> ReadWholeMapAsync(int gameId, int playerId)
    {
        ServiceResultBase.ServiceResult<IReadOnlyCollection<ObserverSight>> result =
            await _mediator.Send(new GetPlayerCampaignMapRequest(gameId, playerId, LoadWholeMap: true));

        Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));

        return result.SuccessValue!;
    }

    /// <summary>The coordinates of every Tile a real Observer can see, which is what the client lights as currently watched.</summary>
    private static HashSet<(int X, int Y)> WatchedCoords(IReadOnlyCollection<ObserverSight> observers)
    {
        return
        [
            .. observers
                .Where(observer => observer.RecordType != _WHOLE_MAP_OBSERVER)
                .SelectMany(observer => observer.SeenTiles)
                .Select(tile => (tile.XCoord, tile.YCoord))
        ];
    }

    /// <summary>The Tiles the whole-map pseudo-observer carries: everything the Player is not watching this moment.</summary>
    private static List<ObservedCampaignTile> RememberedTiles(IReadOnlyCollection<ObserverSight> observers)
    {
        return
        [
            .. observers
                .Where(observer => observer.RecordType == _WHOLE_MAP_OBSERVER)
                .SelectMany(observer => observer.SeenTiles)
        ];
    }

    [Test, Description("Every Tile a real Observer reports comes back explored, because seeing a Tile is what explores it.")]
    public async Task WatchedTilesAreReportedAsExploredAsync()
    {
        int gameId = await SetupGameAsync();
        Player player = await GetFirstPlayerAsync(gameId);

        IReadOnlyCollection<ObserverSight> observers = await ReadWholeMapAsync(gameId, player.Id);

        List<ObservedCampaignTile> watchedTiles =
        [
            .. observers
                .Where(observer => observer.RecordType != _WHOLE_MAP_OBSERVER)
                .SelectMany(observer => observer.SeenTiles)
        ];

        using (Assert.EnterMultipleScope())
        {
            Assert.That(watchedTiles, Is.Not.Empty, "The Player's starting Forces must be able to see at least their own Tile.");
            Assert.That(watchedTiles.All(tile => tile.IsExplored), Is.True, "A Tile an Observer can see has been explored by definition.");
        }
    }

    [Test, Description("Ground the Player has never been to comes back through the whole-map read marked unexplored.")]
    public async Task GroundTheHasPlayerNeverSeenIsReportedAsUnexploredAsync()
    {
        int gameId = await SetupGameAsync();
        Player player = await GetFirstPlayerAsync(gameId);

        IReadOnlyCollection<ObserverSight> observers = await ReadWholeMapAsync(gameId, player.Id);

        HashSet<(int X, int Y)> watched = WatchedCoords(observers);
        List<ObservedCampaignTile> unexplored = [.. RememberedTiles(observers).Where(tile => tile.IsExplored == false)];

        using (Assert.EnterMultipleScope())
        {
            Assert.That(unexplored, Is.Not.Empty, "The map is far larger than the starting Observers can see, so most of it is unexplored.");
            Assert.That(
                unexplored.Any(tile => watched.Contains((tile.XCoord, tile.YCoord))),
                Is.False,
                "No Tile can be reported unexplored while an Observer is looking straight at it.");
        }
    }

    [Test, Description("A Tile stays explored once seen, so ground a Force walks away from comes back as remembered terrain rather than unknown.")]
    public async Task ExploredGroundStaysExploredAfterTheForceWatchingItMovesAwayAsync()
    {
        int gameId = await SetupGameAsync();
        Player player = await GetFirstPlayerAsync(gameId);

        // The first read is what explores the ground around the Player's starting position.
        HashSet<(int X, int Y)> watchedAtStart = WatchedCoords(await ReadWholeMapAsync(gameId, player.Id));

        await MovePlayerForcesToFarCornerAsync(gameId, player.Id);

        IReadOnlyCollection<ObserverSight> afterMove = await ReadWholeMapAsync(gameId, player.Id);

        HashSet<(int X, int Y)> watchedAfterMove = WatchedCoords(afterMove);

        // The interesting Tiles: seen before the move, and not seen now. These are exactly the ones the client draws as
        // explored-but-unwatched, and they only work if the flag survived the Force walking away from them.
        HashSet<(int X, int Y)> leftBehind = [.. watchedAtStart.Except(watchedAfterMove)];

        List<ObservedCampaignTile> leftBehindTiles =
        [
            .. RememberedTiles(afterMove).Where(tile => leftBehind.Contains((tile.XCoord, tile.YCoord)))
        ];

        using (Assert.EnterMultipleScope())
        {
            Assert.That(leftBehind, Is.Not.Empty, "The move must take the Forces far enough that some previously seen ground falls out of sight.");
            Assert.That(leftBehindTiles.Select(tile => (tile.XCoord, tile.YCoord)), Is.EquivalentTo(leftBehind),
                "Ground that has fallen out of sight is still the Player's to remember, so the whole-map read has to hand it back.");
            Assert.That(leftBehindTiles.All(tile => tile.IsExplored), Is.True,
                "Exploration is permanent: losing sight of a Tile must not make it unknown again.");
        }
    }

    /// <summary>
    /// Marches every one of the Player's Forces to the opposite corner of the map, so the ground they were watching falls out of
    /// sight.
    /// </summary>
    /// <remarks>
    /// A direct write rather than a step order: this fixture is about what the map read reports, not about how a Force gets
    /// anywhere, and a step would drag movement costs and terrain rules into a test that has no opinion on either.
    /// </remarks>
    private async Task MovePlayerForcesToFarCornerAsync(int gameId, int playerId)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        GameIndex game = await context.GameIndex.SingleAsync(g => g.Id == gameId);

        // Both maps are laid out row-major from their own offset, so a coordinate is the offset plus its position in the block.
        int farCornerTileId = game.CampaignMapOffsetId + (_MAP_WIDTH - 1) + ((_MAP_HEIGHT - 1) * _MAP_WIDTH);

        List<UnitInstance> forces = await context.UnitInstances.Where(u => u.GameId == gameId && u.PlayerId == playerId).ToListAsync();

        Assert.That(forces, Is.Not.Empty, "Game setup spawns starting Units for every Player.");

        foreach (UnitInstance force in forces)
        {
            force.CampaignMapTileId = farCornerTileId;
        }

        await context.SaveChangesAsync();
    }
}
