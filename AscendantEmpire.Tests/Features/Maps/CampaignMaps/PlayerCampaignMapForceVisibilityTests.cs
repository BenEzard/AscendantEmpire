using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Game.Handlers;
using AscendantEmpire.Application.Features.Maps.CampaignMaps.Handlers;
using AscendantEmpire.Application.Features.Players.Handlers;
using AscendantEmpire.Application.Helpers.ScaledNumerics;
using AscendantEmpire.BackendContracts.CampaignMaps.GetPlayerCampaignMap;
using AscendantEmpire.BackendContracts.Games.GetOpenGames;
using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Entities.Units;
using AscendantEmpire.Domain.Enums;

using MediatR;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AscendantEmpire.Tests.Features.Maps.CampaignMaps;

/// <summary>
/// Integration tests for the campaign map a player actually receives, focused on whether their units
/// come back with it.
/// </summary>
/// <remarks>
/// <para>
/// This is the regression test for units being invisible on the map. Forces are stored against the
/// world map, the sight pipeline works in the player's own map, and the handler converts between the
/// two. That conversion used to add the constant 1 where it needed the game's world-map offset, so it
/// asked for tile ids belonging to no game at all and every tile came back with an empty Force list.
/// It looked correct only for the very first game created, whose map does happen to start at id 1.
/// </para>
/// <para>
/// Every test here therefore sets up a throwaway game first, so the game under test is never the one
/// sitting at offset 1.
/// </para>
/// <para>
/// Runs at the SqlServer tier: game setup writes both maps with raw SQL, and the sight query uses
/// OPENJSON, neither of which an in-memory provider can execute.
/// </para>
/// </remarks>
[TestFixture]
internal class PlayerCampaignMapForceVisibilityTests : GameSqlServerTestBase
{
    private const int _MAP_WIDTH = 4;
    private const int _MAP_HEIGHT = 3;

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

    private SetupGameRequest BuildRequest(string gameName)
    {
        return new SetupGameRequest(
            GalleryMapId: _galleryIndexId,
            GameName: gameName,
            NumberOfPlayers: 2,
            BattleMapSize: 50,
            GenerationSeed: null);
    }

    private async Task<int> SetupGameAsync(string gameName)
    {
        ServiceResultBase.ServiceResult<int> result = await _service.SetupGameAsync(BuildRequest(gameName), CancellationToken.None);

        Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));

        // Setup leaves the Game waiting for its slots to be claimed. These tests are about what happens *during* a Game, and the queries
        // they exercise only look at Games in progress, so start it rather than claiming seats that are beside the point here.
        await StartGameAsync(result.SuccessValue);

        return result.SuccessValue;
    }

    /// <summary>
    /// Seeds two games and returns the second one, which is the interesting case: its map is numbered
    /// from an offset well past 1.
    /// </summary>
    private async Task<int> SetupSecondGameAsync()
    {
        await SeedGameAsync(mapWidth: _MAP_WIDTH, mapHeight: _MAP_HEIGHT, numberOfPlayers: 2);

        await SetupGameAsync("Throwaway First Game");

        return await SetupGameAsync("Game Under Test");
    }

    [Test, Description("A player's own starting units come back on the tile they are standing on, in a game whose map does not start at tile id 1.")]
    public async Task PlayerSeesTheirOwnStartingForcesAsync()
    {
        int gameId = await SetupSecondGameAsync();

        await using AscendantEmpireContext setup = await DbFactory.CreateDbContextAsync();

        GameIndex game = await setup.GameIndex.SingleAsync(g => g.Id == gameId);
        Player player = await setup.Players.Where(p => p.GameId == gameId).OrderBy(p => p.Id).FirstAsync();

        List<UnitInstance> expectedForces = await setup.UnitInstances
            .Where(t => t.GameId == gameId && t.PlayerId == player.Id)
            .ToListAsync();

        CampaignMap startTile = await setup.CampaignMaps.SingleAsync(cm => cm.CampaignMapId == expectedForces[0].CampaignMapTileId);

        ServiceResultBase.ServiceResult<IReadOnlyCollection<ObserverSight>> result =
            await _mediator.Send(new GetPlayerCampaignMapRequest(gameId, player.Id, LoadWholeMap: true));

        Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));

        List<ObservedForce> forcesOnStartTile =
        [
            .. result.SuccessValue!
                .SelectMany(observer => observer.SeenTiles)
                .Where(tile => tile.XCoord == startTile.XCoord && tile.YCoord == startTile.YCoord)
                .SelectMany(tile => tile.Forces)
        ];

        using (Assert.EnterMultipleScope())
        {
            Assert.That(game.CampaignMapOffsetId, Is.GreaterThan(1), "The game under test must not be the one whose map starts at id 1.");
            Assert.That(expectedForces, Is.Not.Empty, "Game setup spawns starting units.");

            Assert.That(forcesOnStartTile.Select(t => t.Id).Distinct(), Is.EquivalentTo(expectedForces.Select(t => t.Id)),
                "Every starting unit is reported on the tile it occupies.");

            Assert.That(forcesOnStartTile, Has.All.Property(nameof(ObservedForce.OwnerPlayerId)).EqualTo(player.Id));
        }
    }

    [Test, Description("A Force whose template compiles to no Sight at all reveals only the tile it occupies, rather than falling back to another stat.")]
    public async Task ForceWithNoCompiledSightSeesOnlyItsOwnTileAsync()
    {
        int gameId = await SetupSecondGameAsync();

        // Sight is nullable with no default and no backfill, so an un-authored catalogue compiles to a
        // TotalSight of zero. That is the state of every existing Unit until someone sets a Sight value in the
        // Entity Setup Tool, so it is worth pinning what the map does with it.
        await using (AscendantEmpireContext blind = await DbFactory.CreateDbContextAsync())
        {
            await blind.CompiledUnitTemplates.ExecuteUpdateAsync(setters => setters.SetProperty(c => c.TotalSight, 0));
        }

        await using AscendantEmpireContext setup = await DbFactory.CreateDbContextAsync();
        Player player = await setup.Players.Where(p => p.GameId == gameId).OrderBy(p => p.Id).FirstAsync();

        // LoadWholeMap would add a terrain-only pseudo-observer covering the entire map, which would drown out
        // the very thing under test.
        ServiceResultBase.ServiceResult<IReadOnlyCollection<ObserverSight>> result =
            await _mediator.Send(new GetPlayerCampaignMapRequest(gameId, player.Id, LoadWholeMap: false));

        Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));

        List<ObserverSight> unitObservers = [.. result.SuccessValue!.Where(observer => observer.RecordType == RecordType.Unit)];

        using (Assert.EnterMultipleScope())
        {
            Assert.That(unitObservers, Is.Not.Empty, "The player's starting units should still be observers, just blind ones.");

            // The seeded map is uniformly Flat grassland, so no elevation bonus can lift a zero range off the floor.
            Assert.That(unitObservers, Has.All.Property(nameof(ObserverSight.SeenTiles)).Count.EqualTo(1),
                "A Unit with no Sight still sees the tile it stands on, and nothing beyond it.");

            Assert.That(
                unitObservers.All(observer => observer.SeenTiles.Single().XCoord == observer.Location.X
                    && observer.SeenTiles.Single().YCoord == observer.Location.Y),
                Is.True,
                "The single seen tile is the occupied one.");
        }
    }

    [Test, Description("Force strengths survive the trip to the client, so the units render with the counts they were spawned with.")]
    public async Task ReportedForcesCarryTheirStrengthAsync()
    {
        int gameId = await SetupSecondGameAsync();

        await using AscendantEmpireContext setup = await DbFactory.CreateDbContextAsync();

        Player player = await setup.Players.Where(p => p.GameId == gameId).OrderBy(p => p.Id).FirstAsync();

        Dictionary<int, UnitInstance> forcesById = await setup.UnitInstances
            .Where(t => t.GameId == gameId && t.PlayerId == player.Id)
            .ToDictionaryAsync(t => t.Id);

        ServiceResultBase.ServiceResult<IReadOnlyCollection<ObserverSight>> result =
            await _mediator.Send(new GetPlayerCampaignMapRequest(gameId, player.Id, LoadWholeMap: true));

        Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));

        List<ObservedForce> reportedForces =
        [
            .. result.SuccessValue!
                .SelectMany(observer => observer.SeenTiles)
                .SelectMany(tile => tile.Forces)
                .Where(force => forcesById.ContainsKey(force.Id))
        ];

        using (Assert.EnterMultipleScope())
        {
            Assert.That(reportedForces, Is.Not.Empty);

            foreach (ObservedForce reported in reportedForces.DistinctBy(t => t.Id))
            {
                UnitInstance expected = forcesById[reported.Id];

                Assert.That(reported.HealthyTroops, Is.EqualTo(expected.HealthyCount));
                Assert.That(reported.InjuredTroops, Is.EqualTo(expected.InjuredCount));
                Assert.That(reported.InitialStrength, Is.EqualTo(expected.InitialStrength));
                Assert.That(reported.UnitTemplateId, Is.EqualTo(expected.UnitTemplateId));
            }
        }
    }

    [Test, Description("A Force's remaining movement is reported to the player who owns it and masked to null for a rival's Force standing in sight.")]
    public async Task ReportedForcesCarryOwnMovementAndMaskARivalsAsync()
    {
        int gameId = await SetupSecondGameAsync();

        await using AscendantEmpireContext setup = await DbFactory.CreateDbContextAsync();

        List<Player> players = await setup.Players.Where(p => p.GameId == gameId).OrderBy(p => p.Id).ToListAsync();
        Player viewer = players[0];
        Player rival = players[1];

        List<UnitInstance> viewerForces = await setup.UnitInstances
            .Where(u => u.GameId == gameId && u.PlayerId == viewer.Id)
            .OrderBy(u => u.Id)
            .ToListAsync();

        UnitInstance rivalForce = await setup.UnitInstances
            .Where(u => u.GameId == gameId && u.PlayerId == rival.Id)
            .OrderBy(u => u.Id)
            .FirstAsync();

        // Setup spawns every Force holding its full campaign allowance (F-9), so left alone they would all report
        // true. Both states are set by hand here, otherwise the projection would look correct while simply
        // echoing whatever the spawn defaults happen to be.
        foreach (UnitInstance spentForce in viewerForces)
        {
            spentForce.MovementPointsRemaining = 0;
        }

        UnitInstance movingForce = viewerForces[0];
        movingForce.MovementPointsRemaining = ScaledAmount.Scale;

        // Park the rival on the viewer's own tile so it is certainly observed. Left where it spawned, the masking
        // assertion would pass vacuously on any map where the rival happened to start out of sight.
        rivalForce.CampaignMapTileId = movingForce.CampaignMapTileId;
        rivalForce.MovementPointsRemaining = ScaledAmount.Scale;

        await setup.SaveChangesAsync();

        ServiceResultBase.ServiceResult<IReadOnlyCollection<ObserverSight>> result =
            await _mediator.Send(new GetPlayerCampaignMapRequest(gameId, viewer.Id, LoadWholeMap: true));

        Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));

        List<ObservedForce> reportedForces =
        [
            .. result.SuccessValue!
                .SelectMany(observer => observer.SeenTiles)
                .SelectMany(tile => tile.Forces)
                .DistinctBy(force => force.Id)
        ];

        ObservedForce? reportedMovingForce = reportedForces.SingleOrDefault(force => force.Id == movingForce.Id);
        ObservedForce? reportedRivalForce = reportedForces.SingleOrDefault(force => force.Id == rivalForce.Id);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(reportedMovingForce, Is.Not.Null, "The viewer sees their own Force.");
            Assert.That(reportedMovingForce!.HasMovementRemaining, Is.True, "The viewer is told their own Force can still move.");

            foreach (ObservedForce reported in reportedForces.Where(f => f.OwnerPlayerId == viewer.Id && f.Id != movingForce.Id))
            {
                Assert.That(reported.HasMovementRemaining, Is.False, $"Force {reported.Id} has spent its movement and is reported as such.");
            }

            Assert.That(reportedRivalForce, Is.Not.Null, "The rival Force is standing on the viewer's own tile, so it is in sight.");
            Assert.That(reportedRivalForce!.HasMovementRemaining, Is.Null,
                "A rival's remaining movement is masked — seeing a Force is not the same as knowing whether it can still act.");
        }
    }

    [Test, Description("Every reported unit, the viewer's own and any rival's in sight, is placed on the tile it actually occupies and credited to its real owner.")]
    public async Task ReportedForcesArePlacedOnTheirOwnTileAndOwnerAsync()
    {
        int gameId = await SetupSecondGameAsync();

        await using AscendantEmpireContext setup = await DbFactory.CreateDbContextAsync();

        Player viewer = await setup.Players.Where(p => p.GameId == gameId).OrderBy(p => p.Id).FirstAsync();

        // The whole Force roster, keyed by id, with the coordinates of the tile each one stands on.
        var forcePlacements = await setup.UnitInstances
            .Where(t => t.GameId == gameId)
            .Select(t => new
            {
                t.Id,
                t.PlayerId,
                t.CampaignMap.XCoord,
                t.CampaignMap.YCoord
            })
            .ToDictionaryAsync(t => t.Id);

        ServiceResultBase.ServiceResult<IReadOnlyCollection<ObserverSight>> result =
            await _mediator.Send(new GetPlayerCampaignMapRequest(gameId, viewer.Id, LoadWholeMap: true));

        Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));

        // Flatten to (Force, the tile it was reported on). The two players start adjacent on this map,
        // so a rival's units may legitimately fall inside the viewer's sight; what matters is that
        // whatever comes back is on the right tile and credited to the right commander.
        var reported = result.SuccessValue!
            .SelectMany(observer => observer.SeenTiles)
            .SelectMany(tile => tile.Forces.Select(force => new { Force = force, tile.XCoord, tile.YCoord }))
            .ToList();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(reported, Is.Not.Empty, "The viewer sees at least their own starting units.");

            foreach (var entry in reported)
            {
                Assert.That(forcePlacements.ContainsKey(entry.Force.Id), Is.True,
                    $"Force {entry.Force.Id} is a real unit in this game.");

                var placement = forcePlacements[entry.Force.Id];

                Assert.That(entry.XCoord, Is.EqualTo(placement.XCoord), $"Force {entry.Force.Id} is reported on its own X coordinate.");
                Assert.That(entry.YCoord, Is.EqualTo(placement.YCoord), $"Force {entry.Force.Id} is reported on its own Y coordinate.");
                Assert.That(entry.Force.OwnerPlayerId, Is.EqualTo(placement.PlayerId), $"Force {entry.Force.Id} is credited to its owner.");
            }
        }
    }

    [Test, Description("Open game details carry the game's own map offset and dimensions, rather than the shared gallery row's.")]
    public async Task OpenGameCarriesTheGamesOwnMapOffsetAndDimensionsAsync()
    {
        int gameId = await SetupSecondGameAsync();

        await using AscendantEmpireContext setup = await DbFactory.CreateDbContextAsync();

        GameIndex game = await setup.GameIndex.SingleAsync(g => g.Id == gameId);

        ServiceResultBase.ServiceResult<IReadOnlyCollection<OpenGame>> result =
            await _mediator.Send(new GetOpenGamesRequest(gameId, null));

        Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));

        OpenGame openGame = result.SuccessValue!.Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(openGame.CampaignMapOffsetId, Is.EqualTo(game.CampaignMapOffsetId));
            Assert.That(openGame.MapWidth, Is.EqualTo(_MAP_WIDTH));
            Assert.That(openGame.MapHeight, Is.EqualTo(_MAP_HEIGHT));
        }
    }
}
