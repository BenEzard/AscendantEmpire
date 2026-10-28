using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.ForceOrders.ContextActions;
using AscendantEmpire.Application.Features.ForceOrders.Handlers;
using AscendantEmpire.Application.Features.Game.Handlers;
using AscendantEmpire.Application.Features.Turns.Services;
using AscendantEmpire.Application.Features.Units.Services;
using AscendantEmpire.BackendContracts.ForceOrders.SplitForce;
using AscendantEmpire.BackendContracts.Turns.GetCurrentCampaignTurn;
using AscendantEmpire.Domain.Entities.Units;
using AscendantEmpire.Domain.Enums.Forces;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Tests.Features.ForceOrders.Handlers;

/// <summary>
/// Tests for <see cref="SplitForceHandler"/>: what dividing an unled Force leaves standing, and what it refuses.
/// </summary>
/// <remarks>
/// <para>
/// On the containerised tier, for the reasons <c>testing.md</c> names: the split runs inside an explicit transaction and the force lifecycle writer allocates its
/// sequence numbers with raw SQL. The in-memory provider has neither.
/// </para>
/// <para>
/// The <b>rules</b> are substituted, not the writers. <see cref="IForceContextActionService"/> stands in for <c>SplitForceStrategy</c>, which has its own fixture and is
/// re-run here only to prove the handler asks and obeys; <see cref="ForceLifecycleWriter"/> is real, because the rows it writes are half of what a split means.
/// </para>
/// </remarks>
[TestFixture]
internal class SplitForceHandlerTests : GameSqlServerTestBase
{
    private const int _MAP_WIDTH = 4;
    private const int _MAP_HEIGHT = 4;

    /// <summary>The Settler organisation's authored quantum, as the seeded catalogue authors it.</summary>
    private const int _UNLED_BASE_SIZE = 25;

    /// <summary>Four quanta — the design doc's worked settler, and enough to divide several ways.</summary>
    private const int _SETTLER_SIZE = 100;

    /// <summary>Enough movement for the strategy's "holding a full turn" gate, which is what splitting costs.</summary>
    private const long _STARTING_MOVEMENT = 2_000;

    private int _gameId;
    private int _playerId;
    private int _otherPlayerId;
    private int _tileId;
    private int _settlerTemplateId;

    /// <summary>
    /// Every Force already in the game when the fixture finished setting up.
    /// </summary>
    /// <remarks>
    /// Game setup spawns each player's starting units, and some of them stand on the tile these tests use. Excluding this set is what lets a test say
    /// "three Forces are standing here" about the split's own output rather than about the whole tile.
    /// </remarks>
    private int[] _preExistingForceIds = [];

    private IForceContextActionService _contextActionService = null!;
    private ICampaignTurnService _campaignTurnService = null!;

    [SetUp]
    public async Task SetUpGameAsync()
    {
        // SeedGameAsync already seeds the starter catalogue — the organisation tags, components and templates with their compiled snapshots — because game setup spawns
        // each player's starting units. Seeding it again would duplicate the settler tag and break its unique index.
        await SeedGameAsync(mapWidth: _MAP_WIDTH, mapHeight: _MAP_HEIGHT, numberOfPlayers: 2);

        ServiceResult<int> setupResult = await _service.SetupGameAsync(
            new SetupGameRequest(GalleryMapId: _galleryIndexId, GameName: "Split Force Game", NumberOfPlayers: 2, BattleMapSize: 50, GenerationSeed: null),
            CancellationToken.None);

        Assert.That(setupResult.HasSucceeded, Is.True, () => string.Join("; ", setupResult.Errors));

        _gameId = setupResult.SuccessValue;

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        int[] playerIds = await context.Players.Where(p => p.GameId == _gameId).OrderBy(p => p.Id).Select(p => p.Id).ToArrayAsync();

        _playerId = playerIds[0];
        _otherPlayerId = playerIds[1];
        _tileId = await context.CampaignMaps.Where(t => t.GameId == _gameId).OrderBy(t => t.CampaignMapId).Select(t => t.CampaignMapId).FirstAsync();
        _settlerTemplateId = await context.UnitTemplates.Where(t => t.Code == "SETTLER").Select(t => t.Id).SingleAsync();
        _preExistingForceIds = await context.UnitInstances.Where(force => force.GameId == _gameId).Select(force => force.Id).ToArrayAsync();

        // The rules say yes unless a test says otherwise. The strategy itself is covered by SplitForceStrategyTests.
        _contextActionService = Substitute.For<IForceContextActionService>();

        _contextActionService
            .EvaluateActionAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<ForceActionType>(), Arg.Any<CancellationToken>())
            .Returns(ServiceResult<ForceActionAvailability>.Succeed(ForceActionAvailability.Available()));

        _campaignTurnService = Substitute.For<ICampaignTurnService>();
    }

    private void ItIsThisPlayersTurn(int playerTurnId, int roundNumber)
    {
        _campaignTurnService
            .GetCurrentCampaignTurnAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(ServiceResult<CurrentCampaignTurn>.Succeed(new CurrentCampaignTurn
            {
                PlayerId = _playerId,
                PlayerTurnId = playerTurnId,
                CurrentRoundNumber = roundNumber,
                FactionName = "Us"
            }));
    }

    private void ItIsTheOtherPlayersTurn(int playerTurnId, int roundNumber)
    {
        _campaignTurnService
            .GetCurrentCampaignTurnAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(ServiceResult<CurrentCampaignTurn>.Succeed(new CurrentCampaignTurn
            {
                PlayerId = _otherPlayerId,
                PlayerTurnId = playerTurnId,
                CurrentRoundNumber = roundNumber,
                FactionName = "Them"
            }));
    }

    private async Task<ServiceResult<SplitForceResult>> HandleAsync(int unitInstanceId, IReadOnlyCollection<int> groupSizes)
    {
        var playerTurnService = new PlayerTurnService(NullLogger<PlayerTurnService>.Instance, DbFactory);

        var handler = new SplitForceHandler(
            NullLogger<SplitForceHandler>.Instance,
            DbFactory,
            _campaignTurnService,
            _contextActionService,
            new ForceLifecycleWriter(NullLogger<ForceLifecycleWriter>.Instance, playerTurnService));

        return await handler.Handle(new SplitForceRequest(_gameId, _playerId, unitInstanceId, groupSizes), CancellationToken.None);
    }

    /// <summary>A settler standing on the fixture's tile holding a full turn's movement, saved and ready to split.</summary>
    private async Task<int> SeedSettlerAsync(int? playerId = null, int healthyCount = _SETTLER_SIZE, int injuredCount = 0)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        var settler = new UnitInstance
        {
            GameId = _gameId,
            PlayerId = playerId ?? _playerId,
            CampaignMapTileId = _tileId,
            UnitTemplateId = _settlerTemplateId,
            InitialStrength = healthyCount + injuredCount,
            HealthyCount = healthyCount,
            InjuredCount = injuredCount,
            CampaignMovementRemaining = _STARTING_MOVEMENT
        };

        context.UnitInstances.Add(settler);
        await context.SaveChangesAsync();

        return settler.Id;
    }

    /// <summary>
    /// The Forces this test put on the fixture's tile — the seeded settler and anything the split created — oldest first, so a split's output reads in the
    /// order it was produced.
    /// </summary>
    /// <remarks>The game's own starting units share this tile, so they are excluded by id; see <see cref="_preExistingForceIds"/>.</remarks>
    private async Task<List<UnitInstance>> SplitForcesOnTileAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        return await context.UnitInstances
            .AsNoTracking()
            .Where(force => force.CampaignMapTileId == _tileId && force.PlayerId == _playerId)
            .Where(force => !_preExistingForceIds.Contains(force.Id))
            .OrderBy(force => force.Id)
            .ToListAsync();
    }

    [Test, Description("The Force the player was holding survives carrying the first group, rather than being destroyed and recreated, so its identity and history are not thrown away by a reorganisation.")]
    public async Task ShouldLeaveTheOriginalForceHoldingTheFirstGroupAsync()
    {
        int playerTurnId = await OpenFirstPlayerTurnAsync(_gameId);
        ItIsThisPlayersTurn(playerTurnId, roundNumber: 1);

        int settlerId = await SeedSettlerAsync();

        ServiceResult<SplitForceResult> result = await HandleAsync(settlerId, [_UNLED_BASE_SIZE, 75]);

        Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        UnitInstance? original = await context.UnitInstances.AsNoTracking().FirstOrDefaultAsync(force => force.Id == settlerId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(original, Is.Not.Null, "Splitting reorganises a Force; it does not consume it.");
            Assert.That(original!.HealthyCount + original.InjuredCount, Is.EqualTo(_UNLED_BASE_SIZE));
            Assert.That(result.SuccessValue!.Groups.Single(group => group.IsOriginalForce).UnitInstanceId, Is.EqualTo(settlerId));
        }
    }

    [Test, Description("A split leaves every Force reporting the group it actually holds as its initial strength, the survivor included, so no Force claims a size the division took away from it.")]
    public async Task ShouldSetEveryForcesInitialStrengthToTheGroupItHoldsAsync()
    {
        int playerTurnId = await OpenFirstPlayerTurnAsync(_gameId);
        ItIsThisPlayersTurn(playerTurnId, roundNumber: 1);

        int settlerId = await SeedSettlerAsync();

        ServiceResult<SplitForceResult> result = await HandleAsync(settlerId, [_UNLED_BASE_SIZE, 75]);

        Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));

        List<UnitInstance> forcesOnTile = await SplitForcesOnTileAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(forcesOnTile.Single(force => force.Id == settlerId).InitialStrength, Is.EqualTo(_UNLED_BASE_SIZE),
                "The survivor was raised 100 strong and comes out of the split holding 25.");
            Assert.That(forcesOnTile.Select(force => force.InitialStrength), Is.EquivalentTo(new[] { _UNLED_BASE_SIZE, 75 }));
        }
    }

    [Test, Description("A split produces one Force per group, so the groups beyond the first have to be created — this is what makes the division real rather than a relabelling.")]
    public async Task ShouldCreateOneNewForceForEveryGroupBeyondTheFirstAsync()
    {
        int playerTurnId = await OpenFirstPlayerTurnAsync(_gameId);
        ItIsThisPlayersTurn(playerTurnId, roundNumber: 1);

        int settlerId = await SeedSettlerAsync();

        ServiceResult<SplitForceResult> result = await HandleAsync(settlerId, [_UNLED_BASE_SIZE, _UNLED_BASE_SIZE, 50]);

        Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));

        List<UnitInstance> forcesOnTile = await SplitForcesOnTileAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(forcesOnTile, Has.Exactly(3).Items, "Three groups asked for, three Forces standing.");
            Assert.That(forcesOnTile.Select(force => force.HealthyCount + force.InjuredCount), Is.EquivalentTo(new[] { 25, 25, 50 }));
            Assert.That(result.SuccessValue!.Groups.Select(group => group.Size), Is.EqualTo(new[] { 25, 25, 50 }),
                "The groups come back in the order the sizes were asked for.");
        }
    }

    [Test, Description("The wounded travel with their group in proportion to its size, and the split must neither invent a casualty nor lose one, because either would silently change the Force's strength.")]
    public async Task ShouldApportionTheInjuredAcrossTheGroupsAsync()
    {
        int playerTurnId = await OpenFirstPlayerTurnAsync(_gameId);
        ItIsThisPlayersTurn(playerTurnId, roundNumber: 1);

        int settlerId = await SeedSettlerAsync(healthyCount: 90, injuredCount: 10);

        ServiceResult<SplitForceResult> result = await HandleAsync(settlerId, [_UNLED_BASE_SIZE, 75]);

        Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));

        List<UnitInstance> forcesOnTile = await SplitForcesOnTileAsync();

        using (Assert.EnterMultipleScope())
        {
            // Exact shares of 10 across 25 and 75 are 2.5 and 7.5; the tied leftover goes to the earlier group.
            Assert.That(result.SuccessValue!.Groups.Select(group => group.InjuredCount), Is.EqualTo(new[] { 3, 7 }));
            Assert.That(forcesOnTile.Sum(force => force.InjuredCount), Is.EqualTo(10), "No casualty is invented or lost by the split.");
            Assert.That(forcesOnTile.Sum(force => force.HealthyCount), Is.EqualTo(90));
        }
    }

    [Test, Description("Splitting costs the whole turn, so no group may march on afterwards — otherwise a player could divide a Force and still move every piece of it.")]
    public async Task ShouldSpendTheWholeTurnOnEveryGroupAsync()
    {
        int playerTurnId = await OpenFirstPlayerTurnAsync(_gameId);
        ItIsThisPlayersTurn(playerTurnId, roundNumber: 1);

        int settlerId = await SeedSettlerAsync();

        ServiceResult<SplitForceResult> result = await HandleAsync(settlerId, [_UNLED_BASE_SIZE, 75]);

        Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));

        List<UnitInstance> forcesOnTile = await SplitForcesOnTileAsync();

        Assert.That(forcesOnTile.Select(force => force.CampaignMovementRemaining), Is.All.Zero);
    }

    [Test, Description("The completed order is what the event log will read, so a split records itself with the Force it divided as Primary and every Force it created as a Result.")]
    public async Task ShouldWriteTheSplitAsACompletedOrderNamingEveryNewForceAsync()
    {
        int playerTurnId = await OpenFirstPlayerTurnAsync(_gameId);
        ItIsThisPlayersTurn(playerTurnId, roundNumber: 1);

        int settlerId = await SeedSettlerAsync();

        ServiceResult<SplitForceResult> result = await HandleAsync(settlerId, [_UNLED_BASE_SIZE, _UNLED_BASE_SIZE, 50]);

        Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        ForceOrder order = await context.ForceOrders
            .AsNoTracking()
            .Include(o => o.Participants)
            .SingleAsync(o => o.GameId == _gameId && o.OrderType == ForceOrderType.SplitForce);

        int[] newForceIds = [.. result.SuccessValue!.Groups.Where(group => !group.IsOriginalForce).Select(group => group.UnitInstanceId)];

        using (Assert.EnterMultipleScope())
        {
            Assert.That(order.OrderStatus, Is.EqualTo(ForceOrderStatus.Completed), "A split resolves at once, so it is recorded finished rather than pending.");
            Assert.That(order.UnitInstanceId, Is.EqualTo(settlerId));
            Assert.That(order.CommandId, Is.Null);

            Assert.That(order.Participants.Where(p => p.Role == ForceOrderParticipantRole.Primary).Select(p => p.UnitInstanceId),
                Is.EqualTo(new[] { settlerId }));
            Assert.That(order.Participants.Where(p => p.Role == ForceOrderParticipantRole.Result).Select(p => p.UnitInstanceId),
                Is.EquivalentTo(newForceIds));
        }
    }

    [Test, Description("No record, no change: a Force that appears on the map without a Created row is intelligence that never arrives, which is close to undebuggable from a bug report.")]
    public async Task ShouldRecordACreatedRowForEveryNewForceAsync()
    {
        int playerTurnId = await OpenFirstPlayerTurnAsync(_gameId);
        ItIsThisPlayersTurn(playerTurnId, roundNumber: 1);

        int settlerId = await SeedSettlerAsync();

        ServiceResult<SplitForceResult> result = await HandleAsync(settlerId, [_UNLED_BASE_SIZE, 75]);

        Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));

        int newForceId = result.SuccessValue!.Groups.Single(group => !group.IsOriginalForce).UnitInstanceId;

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        bool hasCreatedRow = await context.MovementHistory
            .AnyAsync(row => row.UnitInstanceId == newForceId && row.EventType == ForceHistoryEventType.Created);

        Assert.That(hasCreatedRow, Is.True);
    }

    [Test, Description("Sizes that are not a legal unled division are refused outright, because a client that got the arithmetic wrong must not be able to reshape a Force into something the design forbids.")]
    public async Task ShouldRefuseTheSplitWhenTheSizesAreNotALegalDivisionAsync()
    {
        int playerTurnId = await OpenFirstPlayerTurnAsync(_gameId);
        ItIsThisPlayersTurn(playerTurnId, roundNumber: 1);

        int settlerId = await SeedSettlerAsync();

        // 30 and 70 each leave something over against a quantum of 25, so the remainder has been divided between them.
        ServiceResult<SplitForceResult> result = await HandleAsync(settlerId, [30, 70]);

        List<UnitInstance> forcesOnTile = await SplitForcesOnTileAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(result.Errors, Has.One.Contains("one group may carry the remainder"));
            Assert.That(forcesOnTile, Has.Exactly(1).Items, "A refused split leaves the Force exactly as it was.");
            Assert.That(forcesOnTile[0].HealthyCount, Is.EqualTo(_SETTLER_SIZE));
        }
    }

    [Test, Description("The handler re-runs the toolbar's own rules against freshly loaded state and returns their wording untouched, so a refusal reads the same whether the button was greyed out or the order was posted anyway.")]
    public async Task ShouldRefuseTheSplitInTheStrategysOwnWordsWhenTheRulesSayNoAsync()
    {
        int playerTurnId = await OpenFirstPlayerTurnAsync(_gameId);
        ItIsThisPlayersTurn(playerTurnId, roundNumber: 1);

        _contextActionService
            .EvaluateActionAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<ForceActionType>(), Arg.Any<CancellationToken>())
            .Returns(ServiceResult<ForceActionAvailability>.Succeed(ForceActionAvailability.Blocked(["Settler has already moved this turn. Splitting requires a full turn."])));

        int settlerId = await SeedSettlerAsync();

        ServiceResult<SplitForceResult> result = await HandleAsync(settlerId, [_UNLED_BASE_SIZE, 75]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(result.Errors, Has.One.Contains("already moved this turn"));
            Assert.That(await SplitForcesOnTileAsync(), Has.Exactly(1).Items);
        }
    }

    [Test, Description("A Force that is not in this game cannot be split, and Not Found is the honest answer rather than a validation failure about its sizes.")]
    public async Task ShouldReturnNotFoundWhenTheForceIsNotInThisGameAsync()
    {
        int playerTurnId = await OpenFirstPlayerTurnAsync(_gameId);
        ItIsThisPlayersTurn(playerTurnId, roundNumber: 1);

        ServiceResult<SplitForceResult> result = await HandleAsync(unitInstanceId: 999_999, [_UNLED_BASE_SIZE, 75]);

        Assert.That(result.StatusCode, Is.EqualTo(Application.Common.ServiceResultStatus.NotFound));
    }

    [Test, Description("Splitting someone else's Force would reorganise an army the caller does not own, so ownership is checked on the write path rather than trusted from the toolbar.")]
    public async Task ShouldReturnForbiddenWhenTheForceBelongsToAnotherPlayerAsync()
    {
        int playerTurnId = await OpenFirstPlayerTurnAsync(_gameId);
        ItIsThisPlayersTurn(playerTurnId, roundNumber: 1);

        int otherPlayersSettlerId = await SeedSettlerAsync(playerId: _otherPlayerId);

        ServiceResult<SplitForceResult> result = await HandleAsync(otherPlayersSettlerId, [_UNLED_BASE_SIZE, 75]);

        Assert.That(result.StatusCode, Is.EqualTo(Application.Common.ServiceResultStatus.Forbidden));
    }

    [Test, Description("The toolbar never checks whose turn it is, so the write path has to — otherwise a player could reorganise their army during someone else's turn.")]
    public async Task ShouldReturnForbiddenWhenItIsAnotherPlayersTurnAsync()
    {
        int playerTurnId = await OpenFirstPlayerTurnAsync(_gameId);
        ItIsTheOtherPlayersTurn(playerTurnId, roundNumber: 1);

        int settlerId = await SeedSettlerAsync();

        ServiceResult<SplitForceResult> result = await HandleAsync(settlerId, [_UNLED_BASE_SIZE, 75]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.StatusCode, Is.EqualTo(Application.Common.ServiceResultStatus.Forbidden));
            Assert.That(await SplitForcesOnTileAsync(), Has.Exactly(1).Items);
        }
    }
}
