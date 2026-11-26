using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.ForceOrders.Movement.Strategies;
using AscendantEmpire.Application.Features.Game.Handlers;
using AscendantEmpire.Application.Features.Game.Services;
using AscendantEmpire.Application.Features.Maps.CampaignMaps.Handlers;
using AscendantEmpire.Application.Features.Maps.CampaignMaps.Services;
using AscendantEmpire.Application.Features.Players.Handlers;
using AscendantEmpire.Application.Features.Technologies.Classes;
using AscendantEmpire.Application.Features.Technologies.Services;
using AscendantEmpire.Application.Features.Turns.Services;
using AscendantEmpire.BackendContracts.CampaignMaps.GetPlayerCampaignMap;
using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Entities.Maps.CampaignMaps;
using AscendantEmpire.Domain.Entities.Technologies;
using AscendantEmpire.Domain.Entities.Units;
using AscendantEmpire.Domain.Enums;
using AscendantEmpire.Domain.Enums.Maps;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

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
/// Runs at the SqlServer tier: game setup writes both maps with raw SQL, and the exploration stamp uses <c>ExecuteUpdateAsync</c>, neither of
/// which an in-memory provider can execute.
/// </para>
/// </remarks>
[TestFixture]
internal class PlayerCampaignMapExplorationTests : GameSqlServerTestBase
{
    private const int _MAP_WIDTH = 10;
    private const int _MAP_HEIGHT = 8;

    /// <summary>The record type of the whole-map pseudo-observer, which carries remembered terrain rather than live sight.</summary>
    private const RecordType _WHOLE_MAP_OBSERVER = RecordType.None;

    /// <summary>An interior Tile, far enough from every edge that the ring of eight around it is whole.</summary>
    private const int _BLOCKED_TILE_X = 4;
    private const int _BLOCKED_TILE_Y = 4;

    private IMediator _mediator = null!;

    [SetUp]
    public void SetUpMediator()
    {
        _mediator = BuildMediator(new ExperienceAllocator(NullLogger<ExperienceAllocator>.Instance));
    }

    /// <summary>
    /// A mediator wired to the same handler chain the API uses, accruing experience through the allocator given.
    /// </summary>
    /// <remarks>The allocator is a parameter so one test can hand in a failing one and watch the sighting stamps go back with it.</remarks>
    private IMediator BuildMediator(IExperienceAllocator experienceAllocator)
    {
        // A real mediator, so the request goes through the same handler chain the API uses.
        // The campaign-map handlers take a context directly rather than a factory, hence both registrations.
        // GetGameContextHandler delegates its query to IGameContextService, which AddMediator does not register, so that goes in by hand.
        // The movement domain strategies likewise: the map read asks them what a blinded Observer could not have walked onto. They arrive as an
        // IEnumerable, so leaving them out gives the handler an empty set rather than a resolution failure — it would silently reveal nothing.
        // The real turn service, because the map read stamps what the Player saw with a Turn it resolves through it; a substitute would hand back a Turn id no row has.
        // The real field-of-view service and its reader, because working out what the Observers can see is exactly what these tests are about.
        ServiceProvider provider = new ServiceCollection()
            .AddSingleton(DbFactory)
            .AddScoped(_ => DbFactory.CreateDbContext())
            .AddScoped<IGameContextService, GameContextService>()
            .AddSingleton<ICampaignTurnService>(new CampaignTurnService(NullLogger<CampaignTurnService>.Instance, DbFactory))
            .AddScoped<ISightDataReader, SightDataReader>()
            .AddScoped<IFieldOfViewService, FieldOfViewService>()
            // The map read describes the rivers on every Tile it hands back, so the handler cannot be constructed without this.
            .AddScoped<ICampaignRiverReader, CampaignRiverReader>()
            .AddScoped<ICampaignOwnershipReader, CampaignOwnershipReader>()
            .AddScoped<IMovementDomainStrategy, LandMovementStrategy>()
            .AddScoped<IMovementDomainStrategy, WaterMovementStrategy>()
            .AddScoped<IMovementDomainStrategy, BelowWaterMovementStrategy>()
            .AddScoped<IMovementDomainStrategy, AirMovementStrategy>()
            .AddScoped<IMovementDomainStrategy, NearEarthOrbitMovementStrategy>()
            // The real allocator, because what the read accrues for newly uncovered ground is one of the things these tests assert.
            .AddScoped(_ => experienceAllocator)
            .AddMediator(typeof(GetPlayersHandler).Assembly)
            .AddLogging()
            .BuildServiceProvider();

        return provider.GetRequiredService<IMediator>();
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

        // Setup leaves the Game waiting for its slots to be claimed. These tests are about what happens *during* a Game, and the queries
        // they exercise only look at Games in progress, so start it rather than claiming seats that are beside the point here.
        await StartGameAsync(result.SuccessValue);

        // The map read stamps what the Player saw with a Turn and refuses a Game that has none, so open the first Player's Turn as play would have.
        await OpenFirstPlayerTurnAsync(result.SuccessValue);

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

    [Test, Description("The reported bug: a Force blinded in a coastal Jungle uncovers the water beside it, which it could never have stepped onto to discover.")]
    public async Task AForceBlindedInAJungleUncoversOnlyTheWaterItCannotEnterAsync()
    {
        int gameId = await SetupGameAsync();
        Player player = await GetFirstPlayerAsync(gameId);

        // An interior Tile, so all eight neighbours exist and the seven that stay dark are a real assertion rather than an edge of the map.
        await MovePlayerForcesToTileAsync(gameId, player.Id, _BLOCKED_TILE_X, _BLOCKED_TILE_Y);
        await PaintWorldMapTileAsync(gameId, _BLOCKED_TILE_X, _BLOCKED_TILE_Y, BiomeType.Jungle);
        await PaintWorldMapTileAsync(gameId, _BLOCKED_TILE_X + 1, _BLOCKED_TILE_Y, BiomeType.Ocean);

        IReadOnlyCollection<ObserverSight> observers = await ReadWholeMapAsync(gameId, player.Id);

        // Every Force is standing on the blocked Tile, so each is judged on its own. The Settler has one tile of Sight and the Scout three, and the
        // Jungle must leave both with exactly the same two Tiles — the one they occupy, and the sea they are barred from.
        List<ObserverSight> forceObservers = [.. observers.Where(observer => observer.RecordType != _WHOLE_MAP_OBSERVER)];

        Assert.That(forceObservers, Is.Not.Empty, "The Player's Forces are all standing on the blocked Tile and must still report as Observers.");

        (int X, int Y)[] expected =
        [
            (_BLOCKED_TILE_X, _BLOCKED_TILE_Y),
            (_BLOCKED_TILE_X + 1, _BLOCKED_TILE_Y)
        ];

        using (Assert.EnterMultipleScope())
        {
            foreach (ObserverSight observer in forceObservers)
            {
                Assert.That(
                    observer.SeenTiles.Select(tile => (tile.XCoord, tile.YCoord)),
                    Is.EquivalentTo(expected),
                    $"Observer {observer.RecordType} {observer.Id} must uncover the water and nothing else — the seven land neighbours stay dark, because it could walk out of the Jungle onto any of them and look back.");

                Assert.That(observer.SeenTiles.All(tile => tile.IsExplored), Is.True, "Watching a Tile is what explores it.");
            }
        }
    }

    [Test, Description("The mechanic being protected: a Force blinded deep in a Jungle, with only land around it, still uncovers nothing at all.")]
    public async Task AForceBlindedInlandUncoversNothingAsync()
    {
        int gameId = await SetupGameAsync();
        Player player = await GetFirstPlayerAsync(gameId);

        // The stock test map is Grassland throughout, so painting only the occupied Tile leaves every neighbour dry land the Force could step onto.
        await MovePlayerForcesToTileAsync(gameId, player.Id, _BLOCKED_TILE_X, _BLOCKED_TILE_Y);
        await PaintWorldMapTileAsync(gameId, _BLOCKED_TILE_X, _BLOCKED_TILE_Y, BiomeType.Jungle);

        IReadOnlyCollection<ObserverSight> observers = await ReadWholeMapAsync(gameId, player.Id);

        List<ObserverSight> forceObservers = [.. observers.Where(observer => observer.RecordType != _WHOLE_MAP_OBSERVER)];

        Assert.That(forceObservers, Is.Not.Empty, "The Player's Forces are all standing on the blocked Tile and must still report as Observers.");

        using (Assert.EnterMultipleScope())
        {
            foreach (ObserverSight observer in forceObservers)
            {
                Assert.That(
                    observer.SeenTiles.Select(tile => (tile.XCoord, tile.YCoord)),
                    Is.EquivalentTo(new[] { (_BLOCKED_TILE_X, _BLOCKED_TILE_Y) }),
                    $"Observer {observer.RecordType} {observer.Id} must see only the Tile it occupies, or the Jungle stops being worth hiding in.");
            }
        }
    }

    [Test, Description("An explored Tile reports all four of its river edges, including the North one the Tile above it owns and has never been seen.")]
    public async Task AnExploredTileReportsTheEdgesItsNeighboursOwnAsync()
    {
        int gameId = await SetupGameAsync();
        Player player = await GetFirstPlayerAsync(gameId);

        await MovePlayerForcesToTileAsync(gameId, player.Id, _BLOCKED_TILE_X, _BLOCKED_TILE_Y);

        // The Tile above owns the edge between them, and nothing is ever looking at it from the north side; the occupied Tile owns its own East edge.
        await PaintWorldMapRiverAsync(gameId, _BLOCKED_TILE_X, _BLOCKED_TILE_Y - 1, southRiver: RiverWidth.Large);
        await PaintWorldMapRiverAsync(gameId, _BLOCKED_TILE_X, _BLOCKED_TILE_Y, eastRiver: RiverWidth.Medium);

        IReadOnlyCollection<ObserverSight> observers = await ReadWholeMapAsync(gameId, player.Id);

        TileRivers? rivers = FindTile(observers, _BLOCKED_TILE_X, _BLOCKED_TILE_Y).Rivers;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(rivers, Is.Not.Null, "An explored Tile always describes its edges, even when none of them carries a river.");
            Assert.That(rivers!.North, Is.EqualTo(RiverWidth.Large), "A Tile's North edge is the SouthRiver of the Tile above it, whether or not that Tile is explored.");
            Assert.That(rivers.East, Is.EqualTo(RiverWidth.Medium), "A Tile's East edge is its own column.");
            Assert.That(rivers.South, Is.EqualTo(RiverWidth.None));
            Assert.That(rivers.West, Is.EqualTo(RiverWidth.None));
        }
    }

    [Test, Description("Ground the Player has never been to reports no rivers at all, even where the world map has one on its edge.")]
    public async Task UnexploredGroundReportsNoRiversAsync()
    {
        int gameId = await SetupGameAsync();
        Player player = await GetFirstPlayerAsync(gameId);

        // The Forces start nowhere near the far corner, so this river is on ground the Player has not seen.
        await PaintWorldMapRiverAsync(gameId, _MAP_WIDTH - 2, _MAP_HEIGHT - 1, eastRiver: RiverWidth.Large);

        IReadOnlyCollection<ObserverSight> observers = await ReadWholeMapAsync(gameId, player.Id);

        List<ObservedCampaignTile> unexplored = [.. RememberedTiles(observers).Where(tile => tile.IsExplored == false)];

        using (Assert.EnterMultipleScope())
        {
            Assert.That(unexplored, Is.Not.Empty, "The map is far larger than the starting Observers can see.");
            Assert.That(unexplored.All(tile => tile.Rivers == null), Is.True,
                "Rivers are terrain, so an unexplored Tile must disclose none - null, not four edges of None, which would claim the ground has no river.");
            Assert.That(FindTile(observers, _MAP_WIDTH - 2, _MAP_HEIGHT - 1).IsExplored, Is.False,
                "The painted river must be on ground the Player has not seen, or this test is asserting nothing.");
        }
    }

    [Test, Description("A river survives the Force watching it walking away, because rivers are terrain and terrain is remembered.")]
    public async Task ARiverStaysReportedOnGroundThatHasFallenOutOfSightAsync()
    {
        int gameId = await SetupGameAsync();
        Player player = await GetFirstPlayerAsync(gameId);

        await MovePlayerForcesToTileAsync(gameId, player.Id, _BLOCKED_TILE_X, _BLOCKED_TILE_Y);
        await PaintWorldMapRiverAsync(gameId, _BLOCKED_TILE_X, _BLOCKED_TILE_Y, eastRiver: RiverWidth.Large);

        // The first read is what explores the Tile; the move then takes every Observer away from it.
        await ReadWholeMapAsync(gameId, player.Id);
        await MovePlayerForcesToFarCornerAsync(gameId, player.Id);

        IReadOnlyCollection<ObserverSight> afterMove = await ReadWholeMapAsync(gameId, player.Id);

        ObservedCampaignTile leftBehind = FindTile(afterMove, _BLOCKED_TILE_X, _BLOCKED_TILE_Y);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(WatchedCoords(afterMove), Does.Not.Contain((_BLOCKED_TILE_X, _BLOCKED_TILE_Y)), "The move must take every Observer off the Tile.");
            Assert.That(leftBehind.IsExplored, Is.True);
            Assert.That(leftBehind.Rivers?.East, Is.EqualTo(RiverWidth.Large), "Like the Biome and Geography, a river is shown for explored ground whether or not it is watched.");
        }
    }

    /// <summary>
    /// The Tile at the given coordinates, taken from whichever Observer reported it.
    /// </summary>
    /// <remarks>
    /// A whole-map read covers every Tile exactly once between its Observers and its pseudo-observer, so there is never a choice to make here.
    /// </remarks>
    private static ObservedCampaignTile FindTile(IReadOnlyCollection<ObserverSight> observers, int xCoord, int yCoord)
    {
        List<ObservedCampaignTile> matches =
        [
            .. observers.SelectMany(observer => observer.SeenTiles).Where(tile => tile.XCoord == xCoord && tile.YCoord == yCoord)
        ];

        Assert.That(matches, Is.Not.Empty, $"A whole-map read must report ({xCoord}, {yCoord}).");

        return matches[0];
    }

    /// <summary>
    /// Puts a river on one Tile's South or East edge on the world map.
    /// </summary>
    /// <remarks>
    /// The world map, like <see cref="PaintWorldMapTileAsync"/>: a Player's own map has no river columns, because the read derives a Tile's four edges
    /// from the world at the moment it responds. Game setup copies the gallery map's rivers, and the stock test map has none, so a river has to be
    /// painted on for a test to have one.
    /// </remarks>
    private async Task PaintWorldMapRiverAsync(int gameId, int xCoord, int yCoord, RiverWidth? southRiver = null, RiverWidth? eastRiver = null)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        CampaignMap tile = await context.CampaignMaps.SingleAsync(t => t.GameId == gameId && t.XCoord == xCoord && t.YCoord == yCoord);

        if (southRiver is not null)
        {
            tile.SouthRiver = southRiver.Value;
        }

        if (eastRiver is not null)
        {
            tile.EastRiver = eastRiver.Value;
        }

        await context.SaveChangesAsync();
    }

    /// <summary>
    /// Marches every one of the Player's Forces to the opposite corner of the map, so the ground they were watching falls out of
    /// sight.
    /// </summary>
    private Task MovePlayerForcesToFarCornerAsync(int gameId, int playerId)
    {
        return MovePlayerForcesToTileAsync(gameId, playerId, _MAP_WIDTH - 1, _MAP_HEIGHT - 1);
    }

    /// <summary>
    /// Puts every one of the Player's Forces on the given Tile.
    /// </summary>
    /// <remarks>
    /// A direct write rather than a step order: this fixture is about what the map read reports, not about how a Force gets
    /// anywhere, and a step would drag movement costs and terrain rules into a test that has no opinion on either.
    /// </remarks>
    private async Task MovePlayerForcesToTileAsync(int gameId, int playerId, int xCoord, int yCoord)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        GameIndex game = await context.GameIndex.SingleAsync(g => g.Id == gameId);

        // Both maps are laid out row-major from their own offset, so a coordinate is the offset plus its position in the block.
        int tileId = game.CampaignMapOffsetId + xCoord + (yCoord * _MAP_WIDTH);

        List<UnitInstance> forces = await context.UnitInstances.Where(u => u.GameId == gameId && u.PlayerId == playerId).ToListAsync();

        Assert.That(forces, Is.Not.Empty, "Game setup spawns starting Units for every Player.");

        foreach (UnitInstance force in forces)
        {
            // Through the change tracker, because the property is init-only - the lifecycle writer is the only production code allowed to move a Force.
            context.Entry(force).Property(f => f.CampaignMapTileId).CurrentValue = tileId;
        }

        await context.SaveChangesAsync();
    }

    /// <summary>
    /// Repaints one Tile of the world map, seeding the Biome first if this is the first test to ask for it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>GameSqlServerTestBase</c> seeds only Unknown and Grassland — neither of which blocks sight and neither of which is water — and paints the
    /// whole gallery map Grassland, so a stock test map cannot express either half of this rule.
    /// </para>
    /// <para>
    /// The world map is the one to paint. The sight pipeline reads <c>DoesBlockSight</c> and the entry rules from there rather than from the Player's
    /// own copy, precisely so a Player who has never seen a Tile is still judged on what is really on it.
    /// </para>
    /// </remarks>
    private async Task PaintWorldMapTileAsync(int gameId, int xCoord, int yCoord, BiomeType biomeType)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        if (await context.CampaignBiomes.AnyAsync(biome => biome.BiomeTypeId == biomeType) == false)
        {
            context.CampaignBiomes.Add(BuildBiome(biomeType));
            await context.SaveChangesAsync();
        }

        CampaignMap tile = await context.CampaignMaps.SingleAsync(t => t.GameId == gameId && t.XCoord == xCoord && t.YCoord == yCoord);
        tile.BiomeTypeId = biomeType;

        await context.SaveChangesAsync();
    }

    /// <summary>
    /// The seed row for a Biome these tests need and the shared base does not provide.
    /// </summary>
    /// <remarks>
    /// Only the two flags the rule turns on are meaningful here — <c>DoesBlockSight</c> and <c>IsWater</c>. Everything else is filled in plausibly so
    /// the row is valid.
    /// </remarks>
    private static CampaignBiome BuildBiome(BiomeType biomeType)
    {
        return biomeType switch
        {
            BiomeType.Jungle => new CampaignBiome
            {
                BiomeTypeId = BiomeType.Jungle,
                Name = "Jungle",
                MapEditorHtmlColor = "#006400",
                MovementCost = 2.0m,
                DoesBlockSight = true,
                IsWater = false,
                IsTraversable = true,
                RegenerationPercent = 0.1m,
                IsPaintableOnCampaignMap = true,
                CanBeElevated = true
            },
            BiomeType.Ocean => new CampaignBiome
            {
                BiomeTypeId = BiomeType.Ocean,
                Name = "Ocean",
                MapEditorHtmlColor = "#1e90ff",
                MovementCost = 1.0m,
                DoesBlockSight = false,
                IsWater = true,
                IsTraversable = true,
                RegenerationPercent = 0.1m,
                IsPaintableOnCampaignMap = true,
                CanBeElevated = false
            },
            _ => throw new ArgumentOutOfRangeException(nameof(biomeType), biomeType, "This fixture only seeds the Biomes its own rules need.")
        };
    }

    #region Exploration experience

    [Test, Description("A Tile nobody has seen before is worth 3 Exploration points, which is what separates discovering ground from merely arriving on it.")]
    public async Task ShouldAccrueThreePointsPerFirstSightingAsync()
    {
        await SeedExplorationExperienceTypeAsync();

        int gameId = await SetupGameAsync();
        Player player = await GetFirstPlayerAsync(gameId);

        await ReadWholeMapAsync(gameId, player.Id);

        int exploredTiles = await CountStampedTilesAsync(player.CampaignOffsetId);
        int accruedPoints = await AccruedExplorationPointsAsync(player.Id);

        Assert.Multiple(() =>
        {
            Assert.That(exploredTiles, Is.GreaterThan(0), "the starting Observers must reveal something, or this test proves nothing");
            Assert.That(accruedPoints, Is.EqualTo(exploredTiles * 3));
        });
    }

    [Test, Description("Ground somebody else found first is worth 1 point, because arriving where another empire has already been teaches less than discovery.")]
    public async Task ShouldAccrueOnePointPerTileSomebodyElseSawFirstAsync()
    {
        await SeedExplorationExperienceTypeAsync();

        int gameId = await SetupGameAsync();
        Player player = await GetFirstPlayerAsync(gameId);

        await ReadWholeMapAsync(gameId, player.Id);

        int exploredTiles = await CountStampedTilesAsync(player.CampaignOffsetId);

        // Forget what this Player saw while leaving the world's record of who saw it first intact, so the second read finds ground that is new to them and
        // old to the world - the only state in which the one-point rule applies.
        await ForgetPlayerSightingsAsync(player.CampaignOffsetId);
        await ClearAccrualsAsync(player.Id);

        await ReadWholeMapAsync(gameId, player.Id);

        Assert.That(await AccruedExplorationPointsAsync(player.Id), Is.EqualTo(exploredTiles));
    }

    [Test, Description("A second read in the same Turn accrues nothing more, because the sighting stamp the first read wrote is what makes the grant idempotent.")]
    public async Task ShouldAccrueNothingOnASecondReadWhenTheStampIsAlreadySetAsync()
    {
        await SeedExplorationExperienceTypeAsync();

        int gameId = await SetupGameAsync();
        Player player = await GetFirstPlayerAsync(gameId);

        await ReadWholeMapAsync(gameId, player.Id);

        int afterFirstRead = await AccruedExplorationPointsAsync(player.Id);

        await ReadWholeMapAsync(gameId, player.Id);

        Assert.That(await AccruedExplorationPointsAsync(player.Id), Is.EqualTo(afterFirstRead));
    }

    [Test, Description("Two reads that overlap pay for the ground once between them, because the grant counts the Tiles its own stamp changed rather than what it saw before stamping.")]
    public async Task ShouldPayForNewGroundOnceWhenTwoReadsOverlapAsync()
    {
        await SeedExplorationExperienceTypeAsync();

        int gameId = await SetupGameAsync();
        Player player = await GetFirstPlayerAsync(gameId);

        Task<IReadOnlyCollection<ObserverSight>> firstRead = ReadWholeMapAsync(gameId, player.Id);
        Task<IReadOnlyCollection<ObserverSight>> secondRead = ReadWholeMapAsync(gameId, player.Id);

        await Task.WhenAll(firstRead, secondRead);

        int exploredTiles = await CountStampedTilesAsync(player.CampaignOffsetId);

        Assert.That(
            await AccruedExplorationPointsAsync(player.Id),
            Is.EqualTo(exploredTiles * 3),
            "Whichever read loses the race revealed nothing, so it must earn nothing.");
    }

    [Test, Description("A read that reveals nothing writes no accrual row at all, rather than a row of zero that would claim the Player learned something in a Turn they did not.")]
    public async Task ShouldWriteNoAccrualRowWhenTheReadRevealsNothingNewAsync()
    {
        await SeedExplorationExperienceTypeAsync();

        int gameId = await SetupGameAsync();
        Player player = await GetFirstPlayerAsync(gameId);

        await ReadWholeMapAsync(gameId, player.Id);
        await ClearAccrualsAsync(player.Id);

        await ReadWholeMapAsync(gameId, player.Id);

        Assert.That(await AccrualRowCountAsync(player.Id), Is.Zero);
    }

    [Test, Description("An accrual that throws takes the sighting stamps with it. The stamp is the only record that ground was ever new, so one that committed without its points would lose them for good.")]
    public async Task ShouldRollTheSightingStampsBackWhenTheAccrualThrowsAsync()
    {
        await SeedExplorationExperienceTypeAsync();

        int gameId = await SetupGameAsync();
        Player player = await GetFirstPlayerAsync(gameId);

        _mediator = BuildMediator(new ThrowingExperienceAllocator());

        try
        {
            await _mediator.Send(new GetPlayerCampaignMapRequest(gameId, player.Id, LoadWholeMap: true));
        }
        catch (InvalidOperationException)
        {
            // The refusal itself is not what is under test - what the database looks like afterwards is.
        }

        Assert.That(
            await CountStampedTilesAsync(player.CampaignOffsetId),
            Is.Zero,
            "No ground may be marked explored by a read that could not pay for exploring it.");
    }

    [Test, Description("A database whose experience catalogue was never seeded still serves the map, because refusing the read would cost the Player far more than the points.")]
    public async Task ShouldStillServeTheMapWhenTheExperienceCatalogueIsEmptyAsync()
    {
        int gameId = await SetupGameAsync();
        Player player = await GetFirstPlayerAsync(gameId);

        IReadOnlyCollection<ObserverSight> observers = await ReadWholeMapAsync(gameId, player.Id);

        int stampedTiles = await CountStampedTilesAsync(player.CampaignOffsetId);

        Assert.Multiple(() =>
        {
            Assert.That(observers, Is.Not.Empty);
            Assert.That(stampedTiles, Is.GreaterThan(0), "the stamp must still land even though nothing could be accrued");
        });
    }

    #endregion

    #region Exploration experience helpers

    /// <summary>
    /// Adds the one <c>ExperienceType</c> row the exploration rules resolve against.
    /// </summary>
    /// <remarks>
    /// The migration seeds no experience types - they come from the database seed tool - so the restored template has none, and a test that wants the accrual
    /// to happen has to provide the row itself.
    /// </remarks>
    private async Task SeedExplorationExperienceTypeAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        if (await context.ExperienceTypes.AnyAsync(type => type.Code == ExperienceTypeCodes.EXPLORATION))
        {
            return;
        }

        context.ExperienceTypes.Add(new ExperienceType
        {
            Code = ExperienceTypeCodes.EXPLORATION,
            DisplayName = "Exploration",
            IconPath = string.Empty,
            SortOrder = 10
        });

        await context.SaveChangesAsync();
    }

    private async Task<int> AccruedExplorationPointsAsync(int playerId)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        return await context.PendingExperienceAccruals
            .Where(accrual => accrual.PlayerId == playerId && accrual.ExperienceType.Code == ExperienceTypeCodes.EXPLORATION)
            .Select(accrual => accrual.Points)
            .FirstOrDefaultAsync();
    }

    /// <summary>How many of this Player's own map Tiles carry a first-seen stamp, which is the count the grant is worked out from.</summary>
    private async Task<int> CountStampedTilesAsync(int playerMapOffsetId)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        return await context.PlayerCampaignMaps
            .CountAsync(tile => tile.PlayerCampaignMapId >= playerMapOffsetId
                             && tile.PlayerCampaignMapId < playerMapOffsetId + (_MAP_WIDTH * _MAP_HEIGHT)
                             && tile.FirstSeenOnTurnId != null);
    }

    private async Task ForgetPlayerSightingsAsync(int playerMapOffsetId)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        await context.PlayerCampaignMaps
            .Where(tile => tile.PlayerCampaignMapId >= playerMapOffsetId
                        && tile.PlayerCampaignMapId < playerMapOffsetId + (_MAP_WIDTH * _MAP_HEIGHT))
            .ExecuteUpdateAsync(setters => setters.SetProperty(tile => tile.FirstSeenOnTurnId, (int?)null));
    }

    private async Task ClearAccrualsAsync(int playerId)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        await context.PendingExperienceAccruals.Where(accrual => accrual.PlayerId == playerId).ExecuteDeleteAsync();
    }

    /// <summary>How many accrual rows the Player holds, which tells a row of zero apart from no row at all.</summary>
    private async Task<int> AccrualRowCountAsync(int playerId)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        return await context.PendingExperienceAccruals.CountAsync(accrual => accrual.PlayerId == playerId);
    }

    /// <summary>An allocator that refuses by throwing, standing in for a write the database rejects.</summary>
    private sealed class ThrowingExperienceAllocator : IExperienceAllocator
    {
        public Task<ServiceResultBase> AccrueAsync(
            AscendantEmpireContext context,
            int gameId,
            int playerId,
            IReadOnlyDictionary<string, int> pointsByCode,
            CancellationToken cancelToken)
        {
            throw new InvalidOperationException("The accrual could not be written.");
        }
    }

    #endregion
}
