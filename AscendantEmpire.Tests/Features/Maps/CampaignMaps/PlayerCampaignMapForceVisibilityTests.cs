using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Game.Handlers;
using AscendantEmpire.Application.Features.Maps.CampaignMaps.Handlers;
using AscendantEmpire.Application.Features.Players.Handlers;
using AscendantEmpire.BackendContracts.CampaignMaps.GetPlayerCampaignMap;
using AscendantEmpire.BackendContracts.Games.GetOpenGames;
using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Entities.Units;

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
