using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Game.Handlers;
using AscendantEmpire.Application.Features.Players.Handlers;
using AscendantEmpire.Application.Features.ForceOrders.Handlers;
using AscendantEmpire.Application.Features.Turns.Services;
using AscendantEmpire.Application.Features.Units.Handlers;
using AscendantEmpire.BackendContracts.ForceOrders.MoveForces;
using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Entities.Units;

using MediatR;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using NSubstitute;

namespace AscendantEmpire.Tests.Features.Units;

/// <summary>
/// Tests that the handlers which place a troop by coordinate resolve that coordinate to the right
/// campaign map tile.
/// </summary>
/// <remarks>
/// <para>
/// Both handlers used to calculate the tile id from the coordinates, and each got it wrong in its own
/// way: the spawn endpoint assumed every map starts at tile id 1, and the move handler used the
/// player's own map offset, which addresses the per-player map rather than the world map a troop is
/// stored against. Both now resolve the coordinate to a real <see cref="CampaignMap"/> row.
/// </para>
/// <para>
/// Every test sets a throwaway game up first, so the game under test is numbered from an offset well
/// past 1 and a handler that fell back to calculating the id could not pass by coincidence.
/// </para>
/// <para>
/// Runs at the SqlServer tier. The in-memory provider was tried first and cannot serve these paths:
/// game setup writes both maps with raw SQL, and loading a <c>CompiledUnitTemplate</c> throws on its
/// owned damage profiles ("the given key Attack#DamageProfile.Burn was not present").
/// </para>
/// </remarks>
[TestFixture]
internal class TroopPlacementByCoordinateTests : GameSqlServerTestBase
{
    private const int _MAP_WIDTH = 5;
    private const int _MAP_HEIGHT = 4;

    /// <summary>The seeded settler tag is unled with a base size of 25, so a spawn must be a multiple of it.</summary>
    private const int _SETTLER_SPAWN_SIZE = 50;

    private IMediator _mediator = null!;

    [SetUp]
    public void SetUpMediator()
    {
        // MoveForceHandler takes the turn service as a dependency. These tests are rejected on the
        // destination coordinate before it is consulted, but DI still has to construct the handler.
        ICampaignTurnService campaignTurnService = Substitute.For<ICampaignTurnService>();

        ServiceProvider provider = new ServiceCollection()
            .AddSingleton(DbFactory)
            .AddScoped(_ => DbFactory.CreateDbContext())
            .AddSingleton(campaignTurnService)
            .AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<GetPlayersHandler>())
            .AddLogging()
            .BuildServiceProvider();

        _mediator = provider.GetRequiredService<IMediator>();
    }

    private async Task<int> SetupGameAsync(string gameName)
    {
        ServiceResultBase.ServiceResult<int> result = await _service.SetupGameAsync(
            new SetupGameRequest(GalleryMapId: _galleryIndexId, GameName: gameName, NumberOfPlayers: 2, BattleMapSize: 50, GenerationSeed: null),
            CancellationToken.None);

        Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));

        return result.SuccessValue;
    }

    /// <summary>
    /// Seeds two games and returns the id of the second, whose map is numbered from an offset past 1.
    /// </summary>
    private async Task<int> SetupSecondGameAsync()
    {
        await SeedGameAsync(mapWidth: _MAP_WIDTH, mapHeight: _MAP_HEIGHT, numberOfPlayers: 2);

        await SetupGameAsync("Throwaway First Game");

        return await SetupGameAsync("Game Under Test");
    }

    private static async Task<Player> FirstPlayerAsync(AscendantEmpireContext context, int gameId)
    {
        return await context.Players.Where(p => p.GameId == gameId).OrderBy(p => p.Id).FirstAsync();
    }

    [Test, Description("A spawned troop is placed on the game's own map row for the requested coordinate, not on an id derived by assuming the map starts at 1.")]
    public async Task SpawnPlacesTroopOnTheTileForTheRequestedCoordinateAsync()
    {
        const int xCoord = 3;
        const int yCoord = 2;

        int gameId = await SetupSecondGameAsync();

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        GameIndex game = await context.GameIndex.SingleAsync(g => g.Id == gameId);
        Player player = await FirstPlayerAsync(context, gameId);
        int settlerTemplateId = (await context.UnitTemplates.SingleAsync(t => t.Code == "SETTLER")).Id;

        int[] troopIdsBefore = await context.UnitInstances.Where(t => t.GameId == gameId).Select(t => t.Id).ToArrayAsync();

        ServiceResultBase result = await _mediator.Send(new SpawnTroopRequest(
            GameId: gameId,
            PlayerId: player.Id,
            XCoord: xCoord,
            YCoord: yCoord,
            UnitTemplateId: settlerTemplateId,
            InitialStrength: _SETTLER_SPAWN_SIZE,
            HealthyCount: _SETTLER_SPAWN_SIZE,
            InjuredCount: 0));

        Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));

        await using AscendantEmpireContext verify = await DbFactory.CreateDbContextAsync();

        UnitInstance spawned = await verify.UnitInstances
            .Where(t => t.GameId == gameId && troopIdsBefore.Contains(t.Id) == false)
            .SingleAsync();

        CampaignMap tile = await verify.CampaignMaps.SingleAsync(cm => cm.CampaignMapId == spawned.CampaignMapTileId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(game.CampaignMapOffsetId, Is.GreaterThan(1), "The game under test must not be the one whose map starts at id 1.");

            Assert.That(tile.XCoord, Is.EqualTo(xCoord), "The tile the troop landed on carries the requested coordinate.");
            Assert.That(tile.YCoord, Is.EqualTo(yCoord));
            Assert.That(tile.GameId, Is.EqualTo(gameId), "The troop is on a tile belonging to its own game.");

            // Same thing stated as the addressing rule the rest of the map code relies on.
            Assert.That(spawned.CampaignMapTileId, Is.EqualTo(game.CampaignMapOffsetId + xCoord + (yCoord * game.MapWidth)));
        }
    }

    [Test, Description("Spawning onto a coordinate past the edge of the map is refused, rather than silently landing on another game's tile.")]
    public async Task SpawnRejectsACoordinateOffTheMapAsync()
    {
        int gameId = await SetupSecondGameAsync();

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        Player player = await FirstPlayerAsync(context, gameId);
        int settlerTemplateId = (await context.UnitTemplates.SingleAsync(t => t.Code == "SETTLER")).Id;
        int troopCountBefore = await context.UnitInstances.CountAsync(t => t.GameId == gameId);

        // Nothing bounds the coordinates against the map size: the validator only requires them to be
        // non-negative, and the old arithmetic turned this into a valid id outside this game's block.
        ServiceResultBase result = await _mediator.Send(new SpawnTroopRequest(
            GameId: gameId,
            PlayerId: player.Id,
            XCoord: _MAP_WIDTH + 4,
            YCoord: _MAP_HEIGHT + 4,
            UnitTemplateId: settlerTemplateId,
            InitialStrength: _SETTLER_SPAWN_SIZE,
            HealthyCount: _SETTLER_SPAWN_SIZE,
            InjuredCount: 0));

        await using AscendantEmpireContext verify = await DbFactory.CreateDbContextAsync();

        int troopCountAfter = await verify.UnitInstances.CountAsync(t => t.GameId == gameId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(troopCountAfter, Is.EqualTo(troopCountBefore), "No troop is created for an off-map coordinate.");
        }
    }

    [Test, Description("A move to a coordinate past the edge of the map is refused.")]
    public async Task MoveRejectsACoordinateOffTheMapAsync()
    {
        int gameId = await SetupSecondGameAsync();

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        Player player = await FirstPlayerAsync(context, gameId);
        UnitInstance troop = await context.UnitInstances.Where(t => t.GameId == gameId && t.PlayerId == player.Id).FirstAsync();

        ServiceResultBase.ServiceResult<MoveForceResult> result = await _mediator.Send(new MoveForceRequest(
            GameId: gameId,
            PlayerId: player.Id,
            UnitInstanceId: troop.Id,
            XCoord: _MAP_WIDTH + 4,
            YCoord: _MAP_HEIGHT + 4));

        Assert.That(result.HasSucceeded, Is.False, "An off-map destination is not a legal move target.");
    }
}
