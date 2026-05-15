using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Players.Handlers;
using AscendantEmpire.Application.Features.Turns.Handlers;
using AscendantEmpire.BackendContracts.Turns.CreateCampaignTurnQueue;
using AscendantEmpire.BackendContracts.Turns.GetCampaignRoundInfo;
using AscendantEmpire.Domain.Entities.Battles;
using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Entities.Turns;
using AscendantEmpire.Domain.Enums.Battle;
using AscendantEmpire.Domain.Enums.Games;
using AscendantEmpire.Domain.Enums.Turns;

using MediatR;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using PlayerContract = AscendantEmpire.BackendContracts.Players.GetPlayers.Player;

namespace AscendantEmpire.Tests.Handlers.Turns;

internal class CreateBattleTurnQueueHandlerTests
{
    private static readonly InMemoryDatabaseRoot DbRoot = new();
    private static readonly DbContextOptions<AscendantEmpireContext> DbOptions = new DbContextOptionsBuilder<AscendantEmpireContext>()
        .UseInMemoryDatabase(nameof(CreateBattleTurnQueueHandlerTests), DbRoot)
        .Options;

    private IDbContextFactory<AscendantEmpireContext> _dbFactory = null!;
    private IMediator _mediator = null!;
    private ILogger<CreateBattleTurnQueueHandler> _logger = null!;
    private CreateBattleTurnQueueHandler _handler = null!;

    [SetUp]
    public async Task Setup()
    {
        await using (var setupContext = new AscendantEmpireContext(DbOptions))
        {
            await setupContext.Database.EnsureDeletedAsync();
            await setupContext.Database.EnsureCreatedAsync();
        }

        _logger = NullLogger<CreateBattleTurnQueueHandler>.Instance;

        _dbFactory = Substitute.For<IDbContextFactory<AscendantEmpireContext>>();
        _dbFactory
            .CreateDbContextAsync(Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(new AscendantEmpireContext(DbOptions)));

        _mediator = Substitute.For<IMediator>();
        _mediator.Send(Arg.Any<GetPlayersRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(ServiceResultBase.ServiceResult<IReadOnlyCollection<PlayerContract>>.Fail(["not configured"])));

        _mediator.Send(Arg.Any<GetCampaignRoundRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(ServiceResultBase.ServiceResult<CampaignRound>.Fail(["not configured"])));

        _handler = new CreateBattleTurnQueueHandler(_logger, _dbFactory, _mediator);
    }

    [Test]
    public async Task ShouldFail_WhenValidationFailsAsync()
    {
        var request = new CreateBattleTurnQueueRequest(
            GameId: 0,
            CampaignMapTileId: 0,
            PlayerIdsInTurnOrder: [11, 11]);

        ServiceResultBase.ServiceResult<FirstPlayerInRound> result = await _handler.Handle(request, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(result.Errors, Is.Not.Null);
            Assert.That(result.Errors.Any(e => e.Contains("Validation failed", StringComparison.OrdinalIgnoreCase)), Is.True);
        }

        await _mediator.DidNotReceiveWithAnyArgs().Send(default(GetPlayersRequest)!, default);
        await _mediator.DidNotReceiveWithAnyArgs().Send(default(GetCampaignRoundRequest)!, default);
    }

    [Test]
    [Category("ProductionBugSuspected")]
    [Ignore("ProductionBugSuspected")]
    public async Task ShouldReturnNotFound_WhenGameMissingAsync()
    {
        var request = new CreateBattleTurnQueueRequest(1, 100, [11, 12]);

        ServiceResultBase.ServiceResult<FirstPlayerInRound> result = await _handler.Handle(request, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.NotFound));
            Assert.That(result.Errors.Any(e => e.Contains("Game was not found", StringComparison.OrdinalIgnoreCase)), Is.True);
        }
    }

    [Test]
    [Category("ProductionBugSuspected")]
    [Ignore("ProductionBugSuspected")]
    public async Task ShouldFail_WhenRequestedPlayersAreNotAllActiveAsync()
    {
        int gameId = 1;
        await SeedGameAsync(gameId, GamePlayStatus.InProgress, numberOfPlayers: 2);

        _mediator.Send(Arg.Any<GetPlayersRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(ServiceResultBase.ServiceResult<IReadOnlyCollection<PlayerContract>>.Succeed([
                new PlayerContract
                {
                    Id = 11,
                    GameId = gameId,
                    RulerTitle = "Title 11",
                    RulerName = "Ruler 11",
                    FactionName = "Faction 11",
                    Status = PlayerStatus.Active,
                    CampaignOffsetId = 111
                }
            ])));

        ServiceResultBase.ServiceResult<FirstPlayerInRound> result = await _handler.Handle(
            new CreateBattleTurnQueueRequest(gameId, 100, [11, 12]),
            CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(result.Errors.Any(e => e.Contains("not active", StringComparison.OrdinalIgnoreCase)), Is.True);
        }
    }

    [Test]
    [Category("ProductionBugSuspected")]
    [Ignore("ProductionBugSuspected")]
    public async Task ShouldFail_WhenActiveBattleAlreadyExistsForTileAsync()
    {
        int gameId = 1;
        int campaignMapTileId = 100;

        await SeedGameAsync(gameId, GamePlayStatus.InProgress, numberOfPlayers: 2);
        await SeedOpenBattleAsync(gameId, campaignMapTileId);

        _mediator.Send(Arg.Any<GetPlayersRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(ServiceResultBase.ServiceResult<IReadOnlyCollection<PlayerContract>>.Succeed([
                CreatePlayerContract(gameId, 11),
                CreatePlayerContract(gameId, 12)
            ])));

        ServiceResultBase.ServiceResult<FirstPlayerInRound> result = await _handler.Handle(
            new CreateBattleTurnQueueRequest(gameId, campaignMapTileId, [11, 12]),
            CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(result.Errors.Any(e => e.Contains("active Battle already exists", StringComparison.OrdinalIgnoreCase)), Is.True);
        }

        await _mediator.DidNotReceiveWithAnyArgs().Send(default(GetCampaignRoundRequest)!, default);
    }

    [Test]
    [Category("ProductionBugSuspected")]
    [Ignore("ProductionBugSuspected")]
    public async Task ShouldFail_WhenCampaignRoundLookupFailsAsync()
    {
        int gameId = 1;
        await SeedGameAsync(gameId, GamePlayStatus.InProgress, numberOfPlayers: 2);

        _mediator.Send(Arg.Any<GetPlayersRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(ServiceResultBase.ServiceResult<IReadOnlyCollection<PlayerContract>>.Succeed([
                CreatePlayerContract(gameId, 11),
                CreatePlayerContract(gameId, 12)
            ])));

        _mediator.Send(Arg.Any<GetCampaignRoundRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(ServiceResultBase.ServiceResult<CampaignRound>.Fail(["round lookup failed"])));

        ServiceResultBase.ServiceResult<FirstPlayerInRound> result = await _handler.Handle(
            new CreateBattleTurnQueueRequest(gameId, 100, [11, 12]),
            CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(result.Errors.Any(e => e.Contains("Campaign Round", StringComparison.OrdinalIgnoreCase)), Is.True);
        }
    }

    [Test]
    [Category("ProductionBugSuspected")]
    [Ignore("ProductionBugSuspected")]
    public async Task ShouldCreateBattleAndTurnQueueAsync()
    {
        int gameId = 1;
        int campaignMapTileId = 100;
        int currentRoundNumber = 7;
        int[] playerIdsInOrder = [12, 11, 14];

        await SeedGameAsync(gameId, GamePlayStatus.InProgress, numberOfPlayers: playerIdsInOrder.Length);

        _mediator.Send(Arg.Is<GetPlayersRequest>(r =>
                r.GameId == gameId
                && r.PlayerIds != null
                && r.PlayerIds.OrderBy(x => x).SequenceEqual(playerIdsInOrder.OrderBy(x => x))
                && r.PlayerStatuses != null
                && r.PlayerStatuses.Count() == 1
                && r.PlayerStatuses.Contains(PlayerStatus.Active)),
            Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(ServiceResultBase.ServiceResult<IReadOnlyCollection<PlayerContract>>.Succeed([
                CreatePlayerContract(gameId, 11, "Faction 11"),
                CreatePlayerContract(gameId, 12, "Faction 12"),
                CreatePlayerContract(gameId, 14, "Faction 14")
            ])));

        _mediator.Send(Arg.Is<GetCampaignRoundRequest>(r => r.GameId == gameId), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(ServiceResultBase.ServiceResult<CampaignRound>.Succeed(new CampaignRound
            {
                GameId = gameId,
                TurnQueueId = 99,
                CurrentRoundNumber = currentRoundNumber,
                CurrentPlayerId = 11,
                CurrentPlayerTurnOrder = 1,
                IsCompleted = false
            })));

        ServiceResultBase.ServiceResult<FirstPlayerInRound> result = await _handler.Handle(
            new CreateBattleTurnQueueRequest(gameId, campaignMapTileId, playerIdsInOrder),
            CancellationToken.None);

        Assert.That(result.HasSucceeded, Is.True);
        Assert.That(result.SuccessValue, Is.Not.Null);

        await using var assertContext = new AscendantEmpireContext(DbOptions);

        Battle battle = await assertContext.Battles
            .Include(b => b.BattleParticipants)
            .SingleAsync(b => b.GameId == gameId && b.CampaignMapTileId == campaignMapTileId);

        TurnQueue queue = await assertContext.TurnQueues
            .Include(q => q.TurnQueueParticipants)
            .SingleAsync(q => q.GameId == gameId && q.BattleId == battle.Id && q.QueueType == TurnQueueType.Battle);

        TurnQueueParticipant firstParticipant = queue.TurnQueueParticipants
            .OrderBy(p => p.TurnOrder)
            .First();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(battle.GlobalBattleName, Is.EqualTo("Battle 7 between Faction 11 and Faction 12 and Faction 14"));
            Assert.That(battle.CommencedAtRoundNumber, Is.EqualTo(currentRoundNumber));
            Assert.That(battle.ConcludedAtRoundNumber, Is.Null);
            Assert.That(battle.VictorId, Is.Null);
            Assert.That(battle.BattleParticipants, Has.Count.EqualTo(playerIdsInOrder.Length));
            Assert.That(battle.BattleParticipants.Select(p => p.PlayerId), Is.EquivalentTo(playerIdsInOrder));
            Assert.That(battle.BattleParticipants.All(p => p.JoinedAtRoundNumber == currentRoundNumber), Is.True);
            Assert.That(battle.BattleParticipants.All(p => p.IsStillInBattle), Is.True);
            Assert.That(battle.BattleParticipants.All(p => p.LeftAtRoundNumber is null), Is.True);
            Assert.That(battle.BattleParticipants.All(p => p.ExitReason == BattleExitReason.NotApplicable), Is.True);

            Assert.That(queue.BattleId, Is.EqualTo(battle.Id));
            Assert.That(queue.CurrentRoundNumber, Is.EqualTo(currentRoundNumber));
            Assert.That(queue.IsCompleted, Is.False);
            Assert.That(queue.TurnQueueParticipants, Has.Count.EqualTo(playerIdsInOrder.Length));
            Assert.That(queue.TurnQueueParticipants.Select(p => p.PlayerId), Is.EqualTo(playerIdsInOrder));
            Assert.That(queue.TurnQueueParticipants.Select(p => p.TurnOrder), Is.EquivalentTo(Enumerable.Range(1, playerIdsInOrder.Length)));
            Assert.That(queue.TurnQueueParticipants.All(p => p.Status == TurnQueueParticipantStatus.Pending), Is.True);
            Assert.That(queue.TurnQueueParticipants.All(p => p.IsActiveInLane), Is.True);

            Assert.That(result.SuccessValue!.TurnQueueId, Is.EqualTo(queue.Id));
            Assert.That(result.SuccessValue.PlayerId, Is.EqualTo(firstParticipant.PlayerId));
            Assert.That(result.SuccessValue.RoundNumber, Is.EqualTo(currentRoundNumber));
        }
    }

    private async Task SeedGameAsync(int gameId, GamePlayStatus playStatus, int numberOfPlayers)
    {
        await using var context = new AscendantEmpireContext(DbOptions);

        context.GameIndex.Add(new GameIndex
        {
            Id = gameId,
            GameName = $"Game {gameId}",
            NumberOfPlayers = numberOfPlayers,
            CreatedAt = DateTimeOffset.UtcNow,
            CreationStatus = GameCreationStatus.SetupComplete,
            PlayStatus = playStatus
        });

        await context.SaveChangesAsync();
    }

    private async Task SeedOpenBattleAsync(int gameId, int campaignMapTileId)
    {
        await using var context = new AscendantEmpireContext(DbOptions);

        context.Battles.Add(new Battle
        {
            GameId = gameId,
            CampaignMapTileId = campaignMapTileId,
            GlobalBattleName = "Existing battle",
            CommencedAtRoundNumber = 1,
            OriginalTileStatus = Domain.Enums.Maps.TileOwnershipStatus.Unclaimed,
            OriginalTileOwnerId = null,
            ConcludedAtRoundNumber = null,
            VictorId = null,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            RowVersion = []
        });

        await context.SaveChangesAsync();
    }

    private static PlayerContract CreatePlayerContract(int gameId, int playerId, string? factionName = null)
    {
        return new PlayerContract
        {
            Id = playerId,
            GameId = gameId,
            RulerTitle = $"Title {playerId}",
            RulerName = $"Ruler {playerId}",
            FactionName = factionName ?? $"Faction {playerId}",
            Status = PlayerStatus.Active,
            CampaignOffsetId = playerId + 100
        };
    }
}
