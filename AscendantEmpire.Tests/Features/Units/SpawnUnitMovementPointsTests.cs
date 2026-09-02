using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Game.Handlers;
using AscendantEmpire.Application.Features.Players.Handlers;
using AscendantEmpire.Application.Features.Turns.Services;
using AscendantEmpire.Application.Features.Units.Handlers;
using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Entities.UnitDesigns;
using AscendantEmpire.Domain.Entities.Units;

using MediatR;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using NSubstitute;

namespace AscendantEmpire.Tests.Features.Units;

/// <summary>
/// Tests that a newly spawned Unit is fielded holding its template's full campaign movement allowance.
/// </summary>
/// <remarks>
/// <para>
/// <c>MovementPointsRemaining</c> used to be left at its column default of 0, which made a Unit immobile
/// from the moment it was created: <c>MoveForceHandler</c> refuses a Force with nothing left to spend, and
/// nothing replenishes the field at turn rollover yet. The allowance is now copied from the template's
/// <see cref="CompiledUnitTemplate"/> snapshot at spawn time.
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
        // Only the spawn path is exercised, but MediatR scans the whole assembly and DI still has to be able
        // to construct whatever it resolves, so the turn service other handlers depend on is stubbed out.
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

    private async Task<int> SetupGameAsync()
    {
        await SeedGameAsync(mapWidth: _MAP_WIDTH, mapHeight: _MAP_HEIGHT, numberOfPlayers: 2);

        ServiceResultBase.ServiceResult<int> result = await _service.SetupGameAsync(
            new SetupGameRequest(GalleryMapId: _galleryIndexId, GameName: "Movement Points Game", NumberOfPlayers: 2, BattleMapSize: 50, GenerationSeed: null),
            CancellationToken.None);

        Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));

        return result.SuccessValue;
    }

    [Test, Description("A spawned Unit starts with the campaign movement points of its template's compiled snapshot, rather than the column default of zero.")]
    public async Task SpawnGivesTheUnitItsTemplatesFullCampaignMovementAsync()
    {
        int gameId = await SetupGameAsync();

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        Player player = await context.Players.Where(p => p.GameId == gameId).OrderBy(p => p.Id).FirstAsync();
        int settlerTemplateId = (await context.UnitTemplates.SingleAsync(t => t.Code == "SETTLER")).Id;

        // The allowance the spawned Unit is expected to carry. Read from the database rather than hard-coded so
        // the test pins the handler's behaviour, not the seed's numbers.
        long templateMovementPoints = await context.CompiledUnitTemplates
            .Where(c => c.UnitTemplateId == settlerTemplateId)
            .Select(c => c.CampaignMovementPoints)
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
            // Without this the assertion below would pass on a template compiled to zero movement, which is the
            // exact bug being guarded against.
            Assert.That(templateMovementPoints, Is.GreaterThan(0), "The seeded template must have movement points for this test to mean anything.");

            Assert.That(spawned.MovementPointsRemaining, Is.EqualTo(templateMovementPoints),
                "The Unit is fielded holding a full turn's movement, copied from its compiled template.");
        }
    }
}
