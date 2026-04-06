using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Turns.Handlers;
using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Enums.Games;

using MediatR;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

namespace AscendantEmpire.Tests.Handlers.Turns;

internal class EndTurnOnRosterTests
{
    private InMemoryDatabaseRoot _dbRoot = null!;
    private string _dbName = null!;
    private DbContextOptions<AscendantEmpireContext> _options = null!;

    private IDbContextFactory<AscendantEmpireContext> _dbFactory = null!;
    private IMediator _mediator = null!;
    private ILogger<EndTurnOnRosterHandler> _logger = null!;
    private EndTurnOnRosterHandler _handler = null!;

    [SetUp]
    public async Task Setup()
    {
        _dbRoot = new InMemoryDatabaseRoot();
        _dbName = Guid.NewGuid().ToString();

        _options = new DbContextOptionsBuilder<AscendantEmpireContext>()
            .UseInMemoryDatabase(_dbName, _dbRoot)
            .Options;

        await using (var setupContext = new AscendantEmpireContext(_options))
        {
            await setupContext.Database.EnsureDeletedAsync();
            await setupContext.Database.EnsureCreatedAsync();
        }

        _logger = NullLogger<EndTurnOnRosterHandler>.Instance;

        _dbFactory = Substitute.For<IDbContextFactory<AscendantEmpireContext>>();
        _dbFactory
            .CreateDbContextAsync(Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(new AscendantEmpireContext(_options)));

        _mediator = Substitute.For<IMediator>();

        // Safe defaults for awaited mediator calls (override per-test as needed)
        _mediator.Send(Arg.Any<StartTurnOnRosterRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(ServiceResultBase.Succeed()));

        _mediator.Send(Arg.Any<EndRoundRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(ServiceResultBase.Succeed()));

        _handler = new EndTurnOnRosterHandler(_logger, _mediator, _dbFactory);
    }

    [Test]
    public async Task ShouldFail_WhenValidationFailsAsync()
    {
        var request = new EndTurnOnRosterRequest(
            GameId: 0,
            RoundNumber: 0,
            CurrentTurnPlayerId: 0,
            BattleId: null);

        ServiceResultBase result = await _handler.Handle(request, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(result.Errors, Is.Not.Null);
            Assert.That(result.Errors.Any(e => e.Contains("Validation failed", StringComparison.OrdinalIgnoreCase)), Is.True);
        }

        await _mediator.DidNotReceiveWithAnyArgs().Send(default(StartTurnOnRosterRequest)!, default);
        await _mediator.DidNotReceiveWithAnyArgs().Send(default(EndRoundRequest)!, default);
    }

    [Test]
    public async Task ShouldFail_WhenNoRosterRowsForRoundAsync()
    {
        int gameId = 1;
        int roundNumber = 1;

        int currentPlayerId = 11;

        await SeedGameAndPlayersAsync(gameId,
        [
            currentPlayerId,
        ]);

        // No TurnRosters for that round

        var request = new EndTurnOnRosterRequest(
            GameId: gameId,
            RoundNumber: roundNumber,
            CurrentTurnPlayerId: currentPlayerId,
            BattleId: null);

        ServiceResultBase result = await _handler.Handle(request, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(result.Errors.Any(e => e.Contains("No current roster found", StringComparison.OrdinalIgnoreCase)), Is.True);
        }

        await _mediator.DidNotReceiveWithAnyArgs().Send(default(StartTurnOnRosterRequest)!, default);
        await _mediator.DidNotReceiveWithAnyArgs().Send(default(EndRoundRequest)!, default);
    }

    [Test]
    public async Task ShouldFail_WhenNoTakingTurnRosterEntryForPlayerAsync()
    {
        int gameId = 1;
        int roundNumber = 1;

        int currentPlayerId = 11;

        int otherPlayerId = 12;

        await SeedGameAndPlayersAsync(gameId,
        [
            currentPlayerId,
            otherPlayerId,
        ]);

        // Current player is Pending, other player is TakingTurn
        _ = await SeedRosterAsync(gameId, roundNumber, currentPlayerId, turnOrder: 1, status: TurnStatus.Pending, battleId: null);
        _ = await SeedRosterAsync(gameId, roundNumber, otherPlayerId, turnOrder: 2, status: TurnStatus.TakingTurn, battleId: null);

        var request = new EndTurnOnRosterRequest(
            GameId: gameId,
            RoundNumber: roundNumber,
            CurrentTurnPlayerId: currentPlayerId,
            BattleId: null);

        ServiceResultBase result = await _handler.Handle(request, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(result.Errors.Any(e => e.Contains("No current roster entry found", StringComparison.OrdinalIgnoreCase)), Is.True);
        }

        await _mediator.DidNotReceiveWithAnyArgs().Send(default(StartTurnOnRosterRequest)!, default);
        await _mediator.DidNotReceiveWithAnyArgs().Send(default(EndRoundRequest)!, default);
    }

    [Test]
    public async Task ShouldCompleteCurrentTurn_AndStartNextPlayer_WhenNextPendingExistsAsync()
    {
        int gameId = 1;
        int roundNumber = 1;

        int currentPlayerId = 11;

        int nextPlayerId = 12;

        int eliminatedPlayerId = 13;

        await SeedGameAndPlayersAsync(gameId,
        [
            currentPlayerId,
            nextPlayerId,
            eliminatedPlayerId,
        ]);

        // Current TakingTurn at TurnOrder=2
        int currentRosterId = await SeedRosterAsync(gameId, roundNumber, currentPlayerId, turnOrder: 2, status: TurnStatus.TakingTurn, battleId: null);

        // Eliminated entries that should be skipped
        _ = await SeedRosterAsync(gameId, roundNumber, eliminatedPlayerId, turnOrder: 3, status: TurnStatus.Eliminated, battleId: null);

        // Next Pending at TurnOrder=5 (should be selected)
        _ = await SeedRosterAsync(gameId, roundNumber, nextPlayerId, turnOrder: 5, status: TurnStatus.Pending, battleId: null);

        var request = new EndTurnOnRosterRequest(
            GameId: gameId,
            RoundNumber: roundNumber,
            CurrentTurnPlayerId: currentPlayerId,
            BattleId: null);

        ServiceResultBase result = await _handler.Handle(request, CancellationToken.None);

        Assert.That(result.HasSucceeded, Is.True);

        await using (var assertContext = new AscendantEmpireContext(_options))
        {
            TurnRoster updated = await assertContext.TurnRosters
                .AsNoTracking()
                .SingleAsync(r => r.Id == currentRosterId);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(updated.Status, Is.EqualTo(TurnStatus.TurnCompleted));
                Assert.That(updated.TurnEndTime, Is.Not.Null);
            }
        }

        await _mediator.Received(1).Send(
            Arg.Is<StartTurnOnRosterRequest>(r =>
                r.GameId == gameId
                && r.PlayerId == nextPlayerId
                && r.RoundNumber == roundNumber),
            Arg.Any<CancellationToken>());

        await _mediator.DidNotReceive().Send(
            Arg.Any<EndRoundRequest>(),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ShouldCompleteCurrentTurn_AndEndRound_WhenNoNextPendingAsync()
    {
        int gameId = 1;
        int roundNumber = 1;

        int currentPlayerId = 11;

        int earlierPendingPlayerId = 12;

        await SeedGameAndPlayersAsync(gameId,
        [
            currentPlayerId,
            earlierPendingPlayerId,
        ]);

        // Pending exists only BEFORE current player's TurnOrder => no wraparound expected
        _ = await SeedRosterAsync(gameId, roundNumber, earlierPendingPlayerId, turnOrder: 1, status: TurnStatus.Pending, battleId: null);

        // Current player TakingTurn at TurnOrder=5 => no Pending with TurnOrder > 5
        int currentRosterId = await SeedRosterAsync(gameId, roundNumber, currentPlayerId, turnOrder: 5, status: TurnStatus.TakingTurn, battleId: null);

        var request = new EndTurnOnRosterRequest(
            GameId: gameId,
            RoundNumber: roundNumber,
            CurrentTurnPlayerId: currentPlayerId,
            BattleId: null);

        ServiceResultBase result = await _handler.Handle(request, CancellationToken.None);

        Assert.That(result.HasSucceeded, Is.True);

        await using (var assertContext = new AscendantEmpireContext(_options))
        {
            TurnRoster updated = await assertContext.TurnRosters
                .AsNoTracking()
                .SingleAsync(r => r.Id == currentRosterId);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(updated.Status, Is.EqualTo(TurnStatus.TurnCompleted));
                Assert.That(updated.TurnEndTime, Is.Not.Null);
            }
        }

        await _mediator.Received(1).Send(
            Arg.Is<EndRoundRequest>(r => r.GameId == gameId),
            Arg.Any<CancellationToken>());

        await _mediator.DidNotReceive().Send(
            Arg.Any<StartTurnOnRosterRequest>(),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ShouldFail_WhenEndRoundFailsAsync_ButStillCompletesTurnAsync()
    {
        int gameId = 1;
        int roundNumber = 1;

        int currentPlayerId = 11;

        await SeedGameAndPlayersAsync(gameId,
        [
            currentPlayerId,
        ]);

        int currentRosterId = await SeedRosterAsync(gameId, roundNumber, currentPlayerId, turnOrder: 1, status: TurnStatus.TakingTurn, battleId: null);

        _mediator.Send(Arg.Any<EndRoundRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(ServiceResultBase.Fail(["could not end round"])));

        var request = new EndTurnOnRosterRequest(
            GameId: gameId,
            RoundNumber: roundNumber,
            CurrentTurnPlayerId: currentPlayerId,
            BattleId: null);

        ServiceResultBase result = await _handler.Handle(request, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(result.Errors.Any(e => e.Contains("Failed to End the Round", StringComparison.OrdinalIgnoreCase)), Is.True);
        }

        await using (var assertContext = new AscendantEmpireContext(_options))
        {
            TurnRoster updated = await assertContext.TurnRosters
                .AsNoTracking()
                .SingleAsync(r => r.Id == currentRosterId);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(updated.Status, Is.EqualTo(TurnStatus.TurnCompleted));
                Assert.That(updated.TurnEndTime, Is.Not.Null);
            }
        }

        await _mediator.Received(1).Send(
            Arg.Is<EndRoundRequest>(r => r.GameId == gameId),
            Arg.Any<CancellationToken>());

        await _mediator.DidNotReceive().Send(
            Arg.Any<StartTurnOnRosterRequest>(),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ShouldEndBattleTurn_WithoutAffectingCampaignOrOtherBattles_WhenMultipleBattlesExistAsync()
    {
        int gameId = 1;
        int roundNumber = 1;

        int p1Id = 11; // Campaign active player (TakingTurn)
        int p2Id = 12; // Battle 100 current (TakingTurn)
        int p3Id = 13; // Battle 100 next pending

        const int battleToEnd = 100;
        const int otherBattle = 101;

        await SeedGameAndPlayersAsync(gameId,
        [
            p1Id,
            p2Id,
            p3Id,
        ]);

        // --- Campaign roster exists and is active (but we are NOT ending campaign here) ---
        int campaignTakingTurnId = await SeedRosterAsync(gameId, roundNumber, p1Id, turnOrder: 1, status: TurnStatus.TakingTurn, battleId: null);
        _ = await SeedRosterAsync(gameId, roundNumber, p2Id, turnOrder: 2, status: TurnStatus.Pending, battleId: null);
        _ = await SeedRosterAsync(gameId, roundNumber, p3Id, turnOrder: 3, status: TurnStatus.Pending, battleId: null);

        // --- Battle 100: we are ending p2's turn and expect p3 to start next ---
        int battle100TakingTurnId = await SeedRosterAsync(gameId, roundNumber, p2Id, turnOrder: 1, status: TurnStatus.TakingTurn, battleId: battleToEnd);
        _ = await SeedRosterAsync(gameId, roundNumber, p3Id, turnOrder: 2, status: TurnStatus.Pending, battleId: battleToEnd);

        // --- Another battle exists in parallel; should remain untouched ---
        int battle101TakingTurnId = await SeedRosterAsync(gameId, roundNumber, p3Id, turnOrder: 1, status: TurnStatus.TakingTurn, battleId: otherBattle);
        _ = await SeedRosterAsync(gameId, roundNumber, p2Id, turnOrder: 2, status: TurnStatus.Pending, battleId: otherBattle);

        // Default mediator stubs are already safe; keep StartTurn succeeding.
        // We want to ensure EndRound is NOT called in this path.

        var request = new EndTurnOnRosterRequest(
            GameId: gameId,
            RoundNumber: roundNumber,
            CurrentTurnPlayerId: p2Id,
            BattleId: battleToEnd);

        // Act
        ServiceResultBase result = await _handler.Handle(request, CancellationToken.None);

        // Assert overall success
        Assert.That(result.HasSucceeded, Is.True);

        // Assert only Battle 100 current entry was completed
        await using (var assertContext = new AscendantEmpireContext(_options))
        {
            TurnRoster battle100Updated = await assertContext.TurnRosters
                .AsNoTracking()
                .SingleAsync(r => r.Id == battle100TakingTurnId);

            TurnRoster campaignStillTakingTurn = await assertContext.TurnRosters
                .AsNoTracking()
                .SingleAsync(r => r.Id == campaignTakingTurnId);

            TurnRoster otherBattleStillTakingTurn = await assertContext.TurnRosters
                .AsNoTracking()
                .SingleAsync(r => r.Id == battle101TakingTurnId);

            using (Assert.EnterMultipleScope())
            {
                // Battle 100 ended
                Assert.That(battle100Updated.Status, Is.EqualTo(TurnStatus.TurnCompleted));
                Assert.That(battle100Updated.TurnEndTime, Is.Not.Null);

                // Campaign unaffected
                Assert.That(campaignStillTakingTurn.Status, Is.EqualTo(TurnStatus.TakingTurn));
                Assert.That(campaignStillTakingTurn.TurnEndTime, Is.Null);

                // Other battle unaffected
                Assert.That(otherBattleStillTakingTurn.Status, Is.EqualTo(TurnStatus.TakingTurn));
                Assert.That(otherBattleStillTakingTurn.TurnEndTime, Is.Null);
            }
        }

        // Should start next player ONLY for battle 100
        await _mediator.Received(1).Send(
            Arg.Is<StartTurnOnRosterRequest>(r =>
                r.GameId == gameId
                && r.PlayerId == p3Id
                && r.RoundNumber == roundNumber),
            Arg.Any<CancellationToken>());

        // Should NOT end the round (battle still has next pending)
        await _mediator.DidNotReceive().Send(
            Arg.Any<EndRoundRequest>(),
            Arg.Any<CancellationToken>());
    }


    [Test]
    public async Task ShouldRespectBattleId_FilteringAsync()
    {
        int gameId = 1;
        int roundNumber = 1;

        int currentPlayerId = 11;

        int nextBattlePlayerId = 12;

        await SeedGameAndPlayersAsync(gameId,
        [
            currentPlayerId,
            nextBattlePlayerId,
        ]);

        // Seed BOTH campaign and battle rosters; handler should only act on the requested BattleId
        _ = await SeedRosterAsync(gameId, roundNumber, currentPlayerId, turnOrder: 1, status: TurnStatus.TakingTurn, battleId: null);
        _ = await SeedRosterAsync(gameId, roundNumber, nextBattlePlayerId, turnOrder: 2, status: TurnStatus.Pending, battleId: 99);

        // Act on battleId=99 should fail (no taking turn for current player in that battle)
        var request = new EndTurnOnRosterRequest(
            GameId: gameId,
            RoundNumber: roundNumber,
            CurrentTurnPlayerId: currentPlayerId,
            BattleId: 99);

        ServiceResultBase result = await _handler.Handle(request, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(result.Errors.Any(e => e.Contains("No current roster entry found", StringComparison.OrdinalIgnoreCase)), Is.True);
        }
    }

    private async Task SeedGameAndPlayersAsync(
        int gameId,
        IReadOnlyCollection<int> players)
    {
        await using var context = new AscendantEmpireContext(_options);

        context.GameIndex.Add(new GameIndex
        {
            Id = gameId,
            GameName = "Test Game",
            NumberOfPlayers = players.Count,
            CreatedAt = DateTimeOffset.UtcNow,
            CreationStatus = GameCreationStatus.SetupComplete,
            PlayStatus = GamePlayStatus.InProgress
        });

        foreach (var id in players)
        {
            context.Players.Add(new Player
            {
                Id = id,
                GameId = gameId,
                Status = PlayerStatus.Active,
                RulerName = "Ruler",
                RulerTitle = "Title",
                FactionName = "Faction",
            });
        }

        await context.SaveChangesAsync();
    }

    private async Task<int> SeedRosterAsync(
        int gameId,
        int roundNumber,
        int playerId,
        int turnOrder,
        TurnStatus status,
        int? battleId)
    {
        await using var context = new AscendantEmpireContext(_options);

        Player player = await context.Players.SingleAsync(p => p.Id == playerId);

        var roster = new TurnRoster
        {
            GameId = gameId,
            PlayerId = playerId,
            Player = player,
            TurnOrder = turnOrder,
            BattleId = battleId,
            PlayerStillInBattle = null,
            PlayerBattleName = null,
            Status = status,
            TurnStartTime = null,
            TurnEndTime = null,
            ScheduledTurnEndTime = null,
            RoundNumber = roundNumber,
            CreatedAt = DateTimeOffset.UtcNow
        };

        context.TurnRosters.Add(roster);
        await context.SaveChangesAsync();

        return roster.Id;
    }
}
