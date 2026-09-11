using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Game.Handlers;
using AscendantEmpire.Application.Features.Game.Services;
using AscendantEmpire.Application.Features.Players.Handlers;
using AscendantEmpire.Application.Features.Turns.Services;
using AscendantEmpire.Application.Features.Units.Handlers;
using AscendantEmpire.Application.Features.Units.Services;
using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Entities.UnitDesigns;
using AscendantEmpire.Domain.Entities.Units;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace AscendantEmpire.Tests.Features.Units;

/// <summary>
/// Tests that a newly spawned Unit is fielded holding its template's full movement allowance on both maps.
/// </summary>
/// <remarks>
/// <para>
/// <c>CampaignMovementRemaining</c> used to be left at its column default of 0, which made a Unit immobile
/// from the moment it was created: <c>MoveForceHandler</c> refuses a Force with nothing left to spend, and
/// the start-of-Turn replenishment will not reach it until its owner's next Turn. Both allowances are copied
/// from the template's <see cref="CompiledUnitTemplate"/> snapshot at spawn time.
/// </para>
/// <para>
/// Runs at the SqlServer tier for the same reasons as <see cref="UnitPlacementByCoordinateTests"/>: game
/// setup writes both maps with raw SQL, and the spawn handler materialises a <c>CompiledUnitTemplate</c>,
/// whose owned damage profiles the in-memory provider cannot read back.
/// </para>
/// </remarks>
[TestFixture]
internal class SpawnUnitMovementPointsTests : GameSqlServerTestBase
{
    private const int _MAP_WIDTH = 4;
    private const int _MAP_HEIGHT = 4;

    /// <summary>The seeded settler tag is unled with a base size of 25, so a spawn must be a multiple of it.</summary>
    private const int _SETTLER_SPAWN_SIZE = 50;

    private IMediator _mediator = null!;

    [SetUp]
    public void SetUpMediator()
    {
        // The real turn service, not a substitute: the spawn handler reads the current Round through it to stamp the Unit, so stubbing it would
        // leave that stamp pinned to a default rather than to the Round the Game is actually on.
        ICampaignTurnService campaignTurnService = new CampaignTurnService(NullLogger<CampaignTurnService>.Instance, DbFactory);

        // The spawn handler places its Unit through the lifecycle writer. These Games are started without opening a Turn, so the writer records nothing and never allocates a sequence.
        // GetGameContextHandler delegates its query to IGameContextService, which AddMediator does not register, so that goes in by hand.
        ServiceProvider provider = new ServiceCollection()
            .AddSingleton(DbFactory)
            .AddScoped(_ => DbFactory.CreateDbContext())
            .AddScoped<IGameContextService, GameContextService>()
            .AddSingleton(campaignTurnService)
            .AddScoped<IPlayerTurnService, PlayerTurnService>()
            .AddScoped<IForceLifecycleWriter, ForceLifecycleWriter>()
            .AddMediator(typeof(GetPlayersHandler).Assembly)
            .AddLogging()
            .BuildServiceProvider();

        _mediator = provider.GetRequiredService<IMediator>();
    }

    private async Task<int> SetupGameAsync()
    {
        await SeedGameAsync(mapWidth: _MAP_WIDTH, mapHeight: _MAP_HEIGHT, numberOfPlayers: 2);

        ServiceResultBase.ServiceResult<int> result = await _service.SetupGameAsync(
            new SetupGameRequest(GalleryMapId: _galleryIndexId, GameName: "Movement Points Game", NumberOfPlayers: 2, BattleMapSize: 50, GenerationSeed: null),
            CancellationToken.None);

        Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));

        // Setup leaves the Game waiting for its slots to be claimed. These tests are about what happens *during* a Game, and the queries
        // they exercise only look at Games in progress, so start it rather than claiming seats that are beside the point here.
        await StartGameAsync(result.SuccessValue);

        return result.SuccessValue;
    }

    [Test, Description("A spawned Unit starts with the campaign and battle movement of its template's compiled snapshot and is stamped for the current Round, rather than sitting at the column defaults of zero.")]
    public async Task SpawnGivesTheUnitItsTemplatesFullMovementAsync()
    {
        int gameId = await SetupGameAsync();

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        Player player = await context.Players.Where(p => p.GameId == gameId).OrderBy(p => p.Id).FirstAsync();
        int settlerTemplateId = (await context.UnitTemplates.SingleAsync(t => t.Code == "SETTLER")).Id;

        // The allowances the spawned Unit is expected to carry. Read from the database rather than hard-coded so
        // the test pins the handler's behaviour, not the seed's numbers.
        var templateMovement = await context.CompiledUnitTemplates
            .Where(c => c.UnitTemplateId == settlerTemplateId)
            .Select(c => new { c.CampaignMovementPoints, c.BattlefieldMovementPoints })
            .SingleAsync();

        // The Game was started above, so its campaign Queue is open on a Round the spawned Unit should be stamped with.
        int currentRoundNumber = await context.TurnQueues
            .Where(q => q.GameId == gameId && q.IsCompleted == false)
            .Select(q => q.CurrentRoundNumber)
            .SingleAsync();

        int[] unitIdsBefore = await context.UnitInstances.Where(u => u.GameId == gameId).Select(u => u.Id).ToArrayAsync();

        ServiceResultBase result = await _mediator.Send(new SpawnUnitRequest(
            GameId: gameId,
            PlayerId: player.Id,
            XCoord: 2,
            YCoord: 1,
            UnitTemplateId: settlerTemplateId,
            InitialStrength: _SETTLER_SPAWN_SIZE,
            HealthyCount: _SETTLER_SPAWN_SIZE,
            InjuredCount: 0));

        Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));

        await using AscendantEmpireContext verify = await DbFactory.CreateDbContextAsync();

        UnitInstance spawned = await verify.UnitInstances
            .Where(u => u.GameId == gameId && unitIdsBefore.Contains(u.Id) == false)
            .SingleAsync();

        using (Assert.EnterMultipleScope())
        {
            // Without these the assertions below would pass on a template compiled to zero movement, which is the
            // exact bug being guarded against.
            Assert.That(templateMovement.CampaignMovementPoints, Is.GreaterThan(0), "The seeded template must have campaign movement for this test to mean anything.");
            Assert.That(templateMovement.BattlefieldMovementPoints, Is.GreaterThan(0), "The seeded template must have battlefield movement for this test to mean anything.");
            Assert.That(currentRoundNumber, Is.GreaterThan(0), "The Game must be on an open Round for the stamp assertion to mean anything.");

            Assert.That(spawned.CampaignMovementRemaining, Is.EqualTo(templateMovement.CampaignMovementPoints),
                "The Unit is fielded holding a full turn's campaign movement, copied from its compiled template.");

            Assert.That(spawned.BattleMovementRemaining, Is.EqualTo(templateMovement.BattlefieldMovementPoints),
                "The Unit is fielded holding a full battle allowance too — a separate pool, and the only place it is ever written.");

            Assert.That(spawned.CampaignMovementReplenishedOnRound, Is.EqualTo(currentRoundNumber),
                "Stamped as already granted for this Round, so the next start-of-Turn run cannot hand it a second allowance.");
        }
    }
}
