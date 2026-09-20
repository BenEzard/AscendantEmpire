using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Game.Services;
using AscendantEmpire.Application.Features.Turns.Handlers;
using AscendantEmpire.Application.Features.Turns.Services;
using AscendantEmpire.Application.Features.Turns.Services.EndCampaignTurnPossibleActions;
using AscendantEmpire.Application.Features.Turns.Services.TurnPipeline;
using AscendantEmpire.Application.Features.Turns.Services.TurnPipeline.Campaign.EndRound;
using AscendantEmpire.Application.Features.Turns.Services.TurnPipeline.Campaign.EndTurn;
using AscendantEmpire.Application.Features.Turns.Services.TurnPipeline.Campaign.StartRound;
using AscendantEmpire.Application.Features.Turns.Services.TurnPipeline.Campaign.StartTurn;
using AscendantEmpire.BackendContracts.Games.GetGameContext;
using AscendantEmpire.BackendContracts.Turns.GetCurrentCampaignTurn;
using AscendantEmpire.Domain.Entities.Turns;
using AscendantEmpire.Domain.Enums.Turns;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Tests.Features.Turns.Handlers;

/// <summary>
/// Tests for <see cref="EndCampaignTurnHandler"/>.
/// </summary>
/// <remarks>
/// The force-end path marks participants and queues completed with <c>ExecuteUpdateAsync</c>, which the in-memory provider cannot run, so these tests need
/// a real SQL Server. The request validator's own rules are covered without a database in <see cref="EndCampaignTurnRequestValidatorTests"/>.
/// </remarks>
[TestFixture]
public class EndCampaignTurnHandlerTests : GameSqlServerTestBase
{
    // ── mocked services ──────────────────────────────────────────────────────
    private IGameContextService _gameContextService = null!;
    private ICampaignTurnService _campaignTurnService = null!;
    private IEndCampaignTurnPossibleActionService _possibleActionService = null!;
    private ITurnServicePipeline<IEndCampaignTurnProcess> _endCampaignTurnPipeline = null!;
    private ITurnServicePipeline<IEndCampaignRoundProcess> _endCampaignRoundPipeline = null!;
    private ITurnServicePipeline<IStartCampaignRoundProcess> _startCampaignRoundPipeline = null!;
    private ITurnServicePipeline<IStartCampaignTurnProcess> _startCampaignTurnPipeline = null!;

    // Real, not mocked: the round-boundary tests assert on the participant and queue rows this service writes.
    private ICampaignRoundService _campaignRoundService = null!;

    // Also real: it only combines the two mocks above, so mocking it as well would mean arranging the same facts twice.
    private ICampaignTurnAccessService _campaignTurnAccessService = null!;
    private IPlayerTurnService _playerTurnService = null!;

    private EndCampaignTurnHandler _handler = null!;

    // ── seeded IDs populated by SetUp ────────────────────────────────────────
    private int _gameId;
    private int _player1Id;
    private int _player2Id;
    private int _queueId;
    private int _participant1Id; // TurnOrder 1
    private int _participant2Id; // TurnOrder 2

    // The open Player Turn rows behind those participants. Player 2's stays null until ArrangePlayer2TakingTurnAsync hands the Turn to them, which is also
    // what the real "next" lookup returns for a Turn that has not opened yet.
    private int _player1TurnId;
    private int? _player2TurnId;

    [SetUp]
    public async Task SetUp()
    {
        // Seed a 2×2 map and 2 players so we have real FK-valid rows.
        await SeedGameAsync(2, 2, 2);
        _gameId = await SeedGameWithPlayersAsync(2);

        // Resolve the two seeded player IDs (ordered by Id ascending).
        await using AscendantEmpireContext seedCtx = await DbFactory.CreateDbContextAsync();
        List<int> playerIds = [.. seedCtx.Players
            .Where(p => p.GameId == _gameId)
            .OrderBy(p => p.Id)
            .Select(p => p.Id)
            .Take(2)];
        _player1Id = playerIds[0];
        _player2Id = playerIds[1];

        // Seed a Campaign TurnQueue with participant 1 currently taking their turn.
        (_queueId, _participant1Id, _participant2Id) =
            await SeedTurnQueueAsync(_gameId, _player1Id, _player2Id);

        // ── mock services ────────────────────────────────────────────────────
        _gameContextService              = Substitute.For<IGameContextService>();
        _campaignTurnService          = Substitute.For<ICampaignTurnService>();
        _possibleActionService        = Substitute.For<IEndCampaignTurnPossibleActionService>();
        _endCampaignTurnPipeline      = Substitute.For<ITurnServicePipeline<IEndCampaignTurnProcess>>();
        _endCampaignRoundPipeline     = Substitute.For<ITurnServicePipeline<IEndCampaignRoundProcess>>();
        _startCampaignRoundPipeline   = Substitute.For<ITurnServicePipeline<IStartCampaignRoundProcess>>();
        _startCampaignTurnPipeline    = Substitute.For<ITurnServicePipeline<IStartCampaignTurnProcess>>();

        // Pipelines succeed by default — individual tests can override.
        _endCampaignTurnPipeline.ProcessPipelineAsync(Arg.Any<TurnPipelineContextData>()).Returns(ServiceResultBase.Succeed());
        _endCampaignRoundPipeline.ProcessPipelineAsync(Arg.Any<TurnPipelineContextData>()).Returns(ServiceResultBase.Succeed());
        _startCampaignRoundPipeline.ProcessPipelineAsync(Arg.Any<TurnPipelineContextData>()).Returns(ServiceResultBase.Succeed());
        _startCampaignTurnPipeline.ProcessPipelineAsync(Arg.Any<TurnPipelineContextData>()).Returns(ServiceResultBase.Succeed());

        // No possible actions by default.
        _possibleActionService
            .GetPossibleActionsAsync(Arg.Any<PossibleActionContext>(), Arg.Any<CancellationToken>())
            .Returns([]);

        // Real, like the round service below: the handler refuses to end a Turn with no open Player Turn row, so the rows it closes and the round service opens
        // have to genuinely exist.
        _playerTurnService = new PlayerTurnService(new NullLogger<PlayerTurnService>(), DbFactory);

        // Player 1 is seeded as TakingTurn, so they get the open Player Turn that normal play would have opened before their Turn began.
        await using (AscendantEmpireContext playerTurnContext = await DbFactory.CreateDbContextAsync())
        {
            ServiceResult<int> openPlayer1TurnResult = await _playerTurnService.OpenPlayerTurnAsync(playerTurnContext, turnQueueId: _queueId, playerId: _player1Id, roundNumber: 1);
            Assert.That(openPlayer1TurnResult.HasSucceeded, Is.True, () => string.Join("; ", openPlayer1TurnResult.Errors));

            _player1TurnId = openPlayer1TurnResult.SuccessValue;
        }

        // The round service does the real writing, so it is built for real over the mocked pipelines and turn lookups.
        _campaignRoundService = new CampaignRoundService(
            new NullLogger<CampaignRoundService>(),
            _campaignTurnService,
            _playerTurnService,
            _startCampaignRoundPipeline,
            _startCampaignTurnPipeline);

        // Real, over the two mocks it reads through, so the membership and open-Turn stubs each test already arranges keep driving the handler unchanged.
        _campaignTurnAccessService = new CampaignTurnAccessService(
            new NullLogger<CampaignTurnAccessService>(),
            _gameContextService,
            _campaignTurnService);

        _handler = new EndCampaignTurnHandler(
            new NullLogger<EndCampaignTurnHandler>(),
            DbFactory,
            _possibleActionService,
            _endCampaignTurnPipeline,
            _endCampaignRoundPipeline,
            _campaignTurnService,
            _campaignRoundService,
            _playerTurnService,
            _campaignTurnAccessService);
    }

    // ── seed helper ──────────────────────────────────────────────────────────

    /// <summary>
    /// Inserts a Campaign TurnQueue for the given game with two participants.
    /// Player 1 is in <see cref="TurnQueueParticipantStatus.TakingTurn"/> (TurnOrder 1),
    /// Player 2 is <see cref="TurnQueueParticipantStatus.Pending"/> (TurnOrder 2).
    /// Returns (queueId, participant1Id, participant2Id).
    /// </summary>
    private async Task<(int queueId, int p1Id, int p2Id)> SeedTurnQueueAsync(
        int gameId, int player1Id, int player2Id,
        TurnQueueParticipantStatus p1Status = TurnQueueParticipantStatus.TakingTurn,
        TurnQueueParticipantStatus p2Status = TurnQueueParticipantStatus.Pending)
    {
        await using AscendantEmpireContext ctx = await DbFactory.CreateDbContextAsync();

        TurnQueue queue = new TurnQueue
        {
            GameId = gameId,
            QueueType = TurnQueueType.Campaign,
            BattleId = null,
            CurrentRoundNumber = 1,
            CurrentTurnStartAt = DateTimeOffset.UtcNow.AddMinutes(-30),
            CurrentScheduledTurnEndAt = DateTimeOffset.UtcNow.AddHours(47),
            IsCompleted = false,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        ctx.TurnQueues.Add(queue);
        await ctx.SaveChangesAsync();

        TurnQueueParticipant p1 = new TurnQueueParticipant
        {
            TurnQueueId = queue.Id,
            PlayerId = player1Id,
            TurnOrder = 1,
            Status = p1Status,
            IsActiveInLane = true
        };
        TurnQueueParticipant p2 = new TurnQueueParticipant
        {
            TurnQueueId = queue.Id,
            PlayerId = player2Id,
            TurnOrder = 2,
            Status = p2Status,
            IsActiveInLane = true
        };
        ctx.TurnQueueParticipants.AddRange(p1, p2);
        await ctx.SaveChangesAsync();

        return (queue.Id, p1.Id, p2.Id);
    }

    // ── shared arrange helpers ────────────────────────────────────────────────

    /// <summary>
    /// Returns a CurrentCampaignTurn representing player 1's turn (TurnOrder 1, not last in round).
    /// </summary>
    /// <remarks>
    /// Carries player 1's open Player Turn, as the real "current" lookup does. Mocks of the "first" lookup clear it, because that describes a Turn that has not
    /// opened yet.
    /// </remarks>
    private CurrentCampaignTurn Player1Turn() => new()
    {
        GameId = _gameId,
        TurnQueueId = _queueId,
        TurnQueueParticipantId = _participant1Id,
        PlayerId = _player1Id,
        TurnOrder = 1,
        CurrentRoundNumber = 1,
        PlayerTurnId = _player1TurnId
    };

    /// <summary>
    /// Returns a CurrentCampaignTurn representing player 2's turn (TurnOrder 2, last in round).
    /// </summary>
    /// <remarks>
    /// Its Player Turn is null until <see cref="ArrangePlayer2TakingTurnAsync"/> opens one, so the same helper serves both as the "next" lookup and, once
    /// arranged, as the "current" one.
    /// </remarks>
    private CurrentCampaignTurn Player2Turn() => new()
    {
        GameId = _gameId,
        TurnQueueId = _queueId,
        TurnQueueParticipantId = _participant2Id,
        PlayerId = _player2Id,
        TurnOrder = 2,
        CurrentRoundNumber = 1,
        PlayerTurnId = _player2TurnId
    };

    /// <summary>Configures the two services that ValidateGameAndPlayerAsync calls so they succeed for the given player.</summary>
    private void ArrangeValidPlayer(CurrentCampaignTurn currentTurn)
    {
        _gameContextService
            .GetGameContexts(_gameId, currentTurn.PlayerId, Arg.Any<CancellationToken>())
            .Returns(ServiceResult<IReadOnlyCollection<GameContext>>.Succeed(
                [new GameContext { GameId = _gameId, GameName = "Test", NumberOfPlayers = 2, MapWidth = 2, MapHeight = 2, Players = [] }]));

        _campaignTurnService
            .GetCurrentCampaignTurnAsync(_gameId, Arg.Any<CancellationToken>())
            .Returns(ServiceResult<CurrentCampaignTurn>.Succeed(currentTurn));
    }

    // =========================================================================
    // Handle — ValidateGameAndPlayerAsync
    // =========================================================================

    [Test, Description("A player with no claim on the game cannot end its turn, and is refused before any queue state is touched.")]
    public async Task ShouldFailWhenGameContextServiceReturnsNoGame()
    {
        _gameContextService
            .GetGameContexts(Arg.Any<int?>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns(ServiceResult<IReadOnlyCollection<GameContext>>.Succeed([]));

        EndCampaignTurnRequest request = new EndCampaignTurnRequest(_gameId, _player1Id, EndTurnMode.ForceEnd);

        ServiceResult<EndCampaignTurnOutcome> result = await _handler.Handle(request, CancellationToken.None);

        Assert.That(result.HasSucceeded, Is.False, "Should fail when the game is not found for the player.");
    }

    [Test, Description("Being in the game is not enough: a player may only end their own turn, not whoever's turn is currently active.")]
    public async Task ShouldFailWhenItIsNotThePlayersTurn()
    {
        // The game exists but the current turn belongs to player 2, not player 1.
        _gameContextService
            .GetGameContexts(_gameId, _player1Id, Arg.Any<CancellationToken>())
            .Returns(ServiceResult<IReadOnlyCollection<GameContext>>.Succeed(
                [new GameContext { GameId = _gameId, GameName = "Test", NumberOfPlayers = 2, MapWidth = 2, MapHeight = 2, Players = [] }]));

        _campaignTurnService
            .GetCurrentCampaignTurnAsync(_gameId, Arg.Any<CancellationToken>())
            .Returns(ServiceResult<CurrentCampaignTurn>.Succeed(Player2Turn())); // player 2's turn

        EndCampaignTurnRequest request = new EndCampaignTurnRequest(_gameId, _player1Id, EndTurnMode.ForceEnd);

        ServiceResult<EndCampaignTurnOutcome> result = await _handler.Handle(request, CancellationToken.None);

        Assert.That(result.HasSucceeded, Is.False, "Should fail when it is not the requesting player's turn.");
    }

    // =========================================================================
    // Handle — CheckForActions
    // =========================================================================

    [Test, Description("CheckForActions is a query, not a command: with moves still available it reports them and ends nothing.")]
    public async Task ShouldReturnPossibleActionsWithoutEndingTurnWhenActionsExist()
    {
        CurrentCampaignTurn currentTurn = Player1Turn();
        ArrangeValidPlayer(currentTurn);

        PossibleAction action = new() { Description = "Move unit", Id = 1, RecordTypeId = 1 };
        _possibleActionService
            .GetPossibleActionsAsync(Arg.Any<PossibleActionContext>(), Arg.Any<CancellationToken>())
            .Returns([action]);

        EndCampaignTurnRequest request = new EndCampaignTurnRequest(_gameId, _player1Id, EndTurnMode.CheckForActions);

        ServiceResult<EndCampaignTurnOutcome> result = await _handler.Handle(request, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.True);
            Assert.That(result.SuccessValue!.TurnEnded, Is.False, "Turn should not be ended.");
            Assert.That(result.SuccessValue.RoundEnded, Is.False, "Round should not be ended.");
            Assert.That(result.SuccessValue.PossibleActions, Has.Count.EqualTo(1), "Should return the possible actions.");
        }
    }

    [Test, Description("With nothing left outstanding the check becomes the end of the turn, rather than making the Player ask a second time.")]
    public async Task ShouldEndTurnWhenCheckForActionsAndNoPossibleActionsExist()
    {
        CurrentCampaignTurn currentTurn = Player1Turn();
        ArrangeValidPlayer(currentTurn);

        _possibleActionService
            .GetPossibleActionsAsync(Arg.Any<PossibleActionContext>(), Arg.Any<CancellationToken>())
            .Returns([]);

        // Player 1 is not last in the round, so play passes to player 2.
        _campaignTurnService
            .GetNextCampaignTurnAsync(_gameId, 1, Arg.Any<CancellationToken>())
            .Returns(ServiceResult<CurrentCampaignTurn>.Succeed(Player2Turn()));

        EndCampaignTurnRequest request = new EndCampaignTurnRequest(_gameId, _player1Id, EndTurnMode.CheckForActions);

        ServiceResult<EndCampaignTurnOutcome> result = await _handler.Handle(request, CancellationToken.None);

        await using AscendantEmpireContext verifyCtx = await DbFactory.CreateDbContextAsync();
        TurnQueueParticipant participant = verifyCtx.TurnQueueParticipants.First(p => p.Id == _participant1Id);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.True);
            Assert.That(result.SuccessValue!.TurnEnded, Is.True, "The turn should end when there is nothing left to prompt about.");
            Assert.That(result.SuccessValue.PossibleActions, Is.Empty);
            Assert.That(participant.Status, Is.EqualTo(TurnQueueParticipantStatus.TurnCompleted_Manual),
                "The end-turn writes should have been made, not skipped.");
        }
    }

    [Test, Description("The developer skip-everyone mode is refused outright, so it cannot be mistaken for an ordinary end of turn.")]
    public async Task ShouldFailWhenEndTurnModeIsDeveloperAllAboutMe()
    {
        CurrentCampaignTurn currentTurn = Player1Turn();
        ArrangeValidPlayer(currentTurn);

        EndCampaignTurnRequest request = new EndCampaignTurnRequest(_gameId, _player1Id, EndTurnMode.Developer_AllAboutMe);

        ServiceResult<EndCampaignTurnOutcome> result = await _handler.Handle(request, CancellationToken.None);

        await using AscendantEmpireContext verifyCtx = await DbFactory.CreateDbContextAsync();
        TurnQueueParticipant participant = verifyCtx.TurnQueueParticipants.First(p => p.Id == _participant1Id);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False, "The mode is not implemented, so the request should be refused rather than half-honoured.");
            Assert.That(participant.Status, Is.EqualTo(TurnQueueParticipantStatus.TakingTurn), "No turn state should have been touched.");
        }
    }

    // =========================================================================
    // Handle — ForceEnd, not last turn in round (player 1 of 2)
    // =========================================================================

    [Test, Description("Force-ending mid-round closes the turn but not the round, because another player is still to move.")]
    public async Task ShouldSucceedWithTurnEndedAndRoundNotEndedWhenForceEndNotLastTurn()
    {
        CurrentCampaignTurn currentTurn = Player1Turn();
        ArrangeValidPlayer(currentTurn);

        // Next turn is player 2's.
        _campaignTurnService
            .GetNextCampaignTurnAsync(_gameId, 1, Arg.Any<CancellationToken>())
            .Returns(ServiceResult<CurrentCampaignTurn>.Succeed(Player2Turn()));

        EndCampaignTurnRequest request = new EndCampaignTurnRequest(_gameId, _player1Id, EndTurnMode.ForceEnd);

        ServiceResult<EndCampaignTurnOutcome> result = await _handler.Handle(request, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.True);
            Assert.That(result.SuccessValue!.TurnEnded, Is.True);
            Assert.That(result.SuccessValue.RoundEnded, Is.False);
            Assert.That(result.SuccessValue.PossibleActions, Is.Empty);
        }
    }

    [Test, Description("The persisted half of a mid-round hand-over: both participants and the queue's turn clock are updated.")]
    public async Task ShouldMarkParticipantTurnCompletedInDbWhenForceEndNotLastTurn()
    {
        CurrentCampaignTurn currentTurn = Player1Turn();
        ArrangeValidPlayer(currentTurn);

        _campaignTurnService
            .GetNextCampaignTurnAsync(_gameId, 1, Arg.Any<CancellationToken>())
            .Returns(ServiceResult<CurrentCampaignTurn>.Succeed(Player2Turn()));

        EndCampaignTurnRequest request = new EndCampaignTurnRequest(_gameId, _player1Id, EndTurnMode.ForceEnd);
        await _handler.Handle(request, CancellationToken.None);

        await using AscendantEmpireContext verifyCtx = await DbFactory.CreateDbContextAsync();
        TurnQueueParticipant p1 = verifyCtx.TurnQueueParticipants.First(p => p.Id == _participant1Id);
        TurnQueueParticipant p2 = verifyCtx.TurnQueueParticipants.First(p => p.Id == _participant2Id);
        TurnQueue queue = verifyCtx.TurnQueues.First(q => q.Id == _queueId);

        using (Assert.EnterMultipleScope())
        {
            // Ended player's participant
            Assert.That(p1.Status, Is.EqualTo(TurnQueueParticipantStatus.TurnCompleted_Manual), "Player 1 participant should be marked TurnCompleted_Manual.");
            Assert.That(p1.LastCompletedRoundNumber, Is.EqualTo(1), "LastCompletedRoundNumber should be set to the current round.");
            Assert.That(p1.LastTurnEndedAt, Is.Not.Null, "LastTurnEndedAt should be stamped.");

            // Next player's participant
            Assert.That(p2.Status, Is.EqualTo(TurnQueueParticipantStatus.TakingTurn), "Player 2 participant should now be TakingTurn.");
            Assert.That(p2.LastTurnStartedAt, Is.Not.Null,
                "Player 2 LastTurnStartedAt should be stamped.");

            // Queue
            Assert.That(queue.CurrentTurnStartAt, Is.Not.Null, "Queue CurrentTurnStartAt should be set for the new turn.");
            Assert.That(queue.CurrentScheduledTurnEndAt, Is.Not.Null, "Queue CurrentScheduledTurnEndAt should be set for the new turn.");
            Assert.That(queue.CurrentActualTurnEndAt, Is.Null, "Queue CurrentActualTurnEndAt should be null (turn not yet finished).");
        }
    }

    [Test, Description("If who-plays-next cannot be resolved the turn is left alone, rather than leaving the queue with nobody active.")]
    public async Task ShouldFailWhenGetNextCampaignTurnFailsDuringForceEnd()
    {
        CurrentCampaignTurn currentTurn = Player1Turn();
        ArrangeValidPlayer(currentTurn);

        _campaignTurnService
            .GetNextCampaignTurnAsync(_gameId, 1, Arg.Any<CancellationToken>())
            .Returns(ServiceResult<CurrentCampaignTurn>.Fail("next turn lookup failed"));

        EndCampaignTurnRequest request = new EndCampaignTurnRequest(_gameId, _player1Id, EndTurnMode.ForceEnd);

        ServiceResult<EndCampaignTurnOutcome> result = await _handler.Handle(request, CancellationToken.None);

        Assert.That(result.HasSucceeded, Is.False, "Should fail when GetNextCampaignTurnAsync fails.");
    }

    [Test, Description("A Turn with no open Player Turn row is refused before anything is written, because ending it would lose that Turn from the record.")]
    public async Task ShouldFailWithoutWritingWhenTheTurnHasNoPlayerTurn()
    {
        // Everything is in order except the row every Turn is opened with.
        CurrentCampaignTurn currentTurn = Player1Turn() with { PlayerTurnId = null };
        ArrangeValidPlayer(currentTurn);

        EndCampaignTurnRequest request = new EndCampaignTurnRequest(_gameId, _player1Id, EndTurnMode.ForceEnd);

        ServiceResult<EndCampaignTurnOutcome> result = await _handler.Handle(request, CancellationToken.None);

        await using AscendantEmpireContext verifyCtx = await DbFactory.CreateDbContextAsync();
        TurnQueueParticipant participant = verifyCtx.TurnQueueParticipants.First(p => p.Id == _participant1Id);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False, "A Turn with no Player Turn row should not be ended.");
            Assert.That(participant.Status, Is.EqualTo(TurnQueueParticipantStatus.TakingTurn), "The refusal should come before any end-of-turn write.");
        }

        // The end-of-turn processes act on the Turn's row, so they must not have been run without one.
        await _endCampaignTurnPipeline.DidNotReceive().ProcessPipelineAsync(Arg.Any<TurnPipelineContextData>());
    }

    // =========================================================================
    // Handle — ForceEnd, last turn in round (player 2 of 2)
    // =========================================================================

    /// <summary>
    /// Sets up the DB so player 2 is the one currently taking their turn (last in round),
    /// and player 1 is Pending (will be first in the next round).
    /// </summary>
    private async Task ArrangePlayer2TakingTurnAsync()
    {
        // Re-seed the queue with player 2 taking their turn.
        await using AscendantEmpireContext ctx = await DbFactory.CreateDbContextAsync();
        await ctx.TurnQueueParticipants
            .Where(p => p.Id == _participant1Id)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.Status, TurnQueueParticipantStatus.TurnCompleted_Manual));
        await ctx.TurnQueueParticipants
            .Where(p => p.Id == _participant2Id)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.Status, TurnQueueParticipantStatus.TakingTurn));

        // Hand the Player Turn over as well, exactly as ending player 1's Turn would have: close theirs, then open player 2's.
        ServiceResultBase closePlayer1TurnResult = await _playerTurnService.ClosePlayerTurnAsync(ctx, turnQueueId: _queueId, expectedPlayerId: _player1Id);
        ServiceResult<int> openPlayer2TurnResult = await _playerTurnService.OpenPlayerTurnAsync(ctx, turnQueueId: _queueId, playerId: _player2Id, roundNumber: 1);

        Assert.That(closePlayer1TurnResult.HasSucceeded && openPlayer2TurnResult.HasSucceeded, Is.True, "Player 2's Turn should have been handed over for the test.");

        _player2TurnId = openPlayer2TurnResult.SuccessValue;
    }

    [Test, Description("The last player force-ending closes the round too, rolling the queue over to a new one.")]
    public async Task ShouldSucceedWithRoundEndedWhenForceEndLastTurnInRound()
    {
        await ArrangePlayer2TakingTurnAsync();

        CurrentCampaignTurn currentTurn = Player2Turn();
        ArrangeValidPlayer(currentTurn);

        // Opening the round puts everyone back to Pending; player 1 holds the lowest turn order, so they lead the new round.
        CurrentCampaignTurn nextRoundFirstTurn = Player1Turn() with { CurrentRoundNumber = 2, PlayerTurnId = null };
        _campaignTurnService
            .GetFirstCampaignTurnAsync(Arg.Any<AscendantEmpireContext>(), _gameId, Arg.Any<CancellationToken>())
            .Returns(ServiceResult<CurrentCampaignTurn>.Succeed(nextRoundFirstTurn));

        EndCampaignTurnRequest request = new EndCampaignTurnRequest(_gameId, _player2Id, EndTurnMode.ForceEnd);

        ServiceResult<EndCampaignTurnOutcome> result = await _handler.Handle(request, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.True);
            Assert.That(result.SuccessValue!.TurnEnded, Is.True);
            Assert.That(result.SuccessValue.RoundEnded, Is.True);
            Assert.That(result.SuccessValue.PossibleActions, Is.Empty);
        }
    }

    [Test, Description("The persisted half of a round rollover: the new round's first player is active and the turn clock restarts.")]
    public async Task ShouldUpdateDbCorrectlyWhenForceEndLastTurnInRound()
    {
        await ArrangePlayer2TakingTurnAsync();

        CurrentCampaignTurn currentTurn = Player2Turn();
        ArrangeValidPlayer(currentTurn);

        CurrentCampaignTurn nextRoundFirstTurn = Player1Turn() with { CurrentRoundNumber = 2, PlayerTurnId = null };
        _campaignTurnService
            .GetFirstCampaignTurnAsync(Arg.Any<AscendantEmpireContext>(), _gameId, Arg.Any<CancellationToken>())
            .Returns(ServiceResult<CurrentCampaignTurn>.Succeed(nextRoundFirstTurn));

        EndCampaignTurnRequest request = new EndCampaignTurnRequest(_gameId, _player2Id, EndTurnMode.ForceEnd);
        await _handler.Handle(request, CancellationToken.None);

        await using AscendantEmpireContext verifyCtx = await DbFactory.CreateDbContextAsync();
        TurnQueueParticipant p1 = verifyCtx.TurnQueueParticipants.First(p => p.Id == _participant1Id);
        TurnQueueParticipant p2 = verifyCtx.TurnQueueParticipants.First(p => p.Id == _participant2Id);
        TurnQueue queue = verifyCtx.TurnQueues.First(q => q.Id == _queueId);

        using (Assert.EnterMultipleScope())
        {
            // Player 2 ended their turn.
            Assert.That(p2.Status, Is.EqualTo(TurnQueueParticipantStatus.Pending),
                "Player 2 closed the round, but opening the next one puts every active participant back to Pending — they owe a turn in it too.");
            Assert.That(p2.LastTurnEndedAt, Is.Not.Null, "Player 2 LastTurnEndedAt should be stamped.");

            // Player 1 starts the new round's turn.
            Assert.That(p1.Status, Is.EqualTo(TurnQueueParticipantStatus.TakingTurn), "Player 1 should be TakingTurn at the start of the new round.");
            Assert.That(p1.LastTurnStartedAt, Is.Not.Null, "Player 1 LastTurnStartedAt should be stamped for the new round.");

            // Queue reflects the start-of-turn for the next round.
            Assert.That(queue.CurrentTurnStartAt, Is.Not.Null, "Queue CurrentTurnStartAt should be set for the first turn of the new round.");
            Assert.That(queue.CurrentScheduledTurnEndAt, Is.Not.Null, "Queue CurrentScheduledTurnEndAt should be set.");
            Assert.That(queue.CurrentActualTurnEndAt, Is.Null, "Queue CurrentActualTurnEndAt should be null at the start of a new turn.");
        }
    }

    [Test, Description("A round is not rolled over when its opening player cannot be resolved, which would leave the game unplayable.")]
    public async Task ShouldFailWhenGetFirstCampaignTurnFailsDuringForceEndLastTurn()
    {
        await ArrangePlayer2TakingTurnAsync();

        CurrentCampaignTurn currentTurn = Player2Turn();
        ArrangeValidPlayer(currentTurn);

        _campaignTurnService
            .GetFirstCampaignTurnAsync(Arg.Any<AscendantEmpireContext>(), _gameId, Arg.Any<CancellationToken>())
            .Returns(ServiceResult<CurrentCampaignTurn>.Fail("first turn lookup failed"));

        EndCampaignTurnRequest request = new EndCampaignTurnRequest(_gameId, _player2Id, EndTurnMode.ForceEnd);

        ServiceResult<EndCampaignTurnOutcome> result = await _handler.Handle(request, CancellationToken.None);

        Assert.That(result.HasSucceeded, Is.False, "Should fail when GetFirstCampaignTurnAsync fails.");
    }

    [Test, Description("A rollover opens the new round before anyone plays it: the round number moves on and every active participant is Pending again.")]
    public async Task ShouldAdvanceRoundAndResetParticipantsWhenForceEndLastTurnInRound()
    {
        await ArrangePlayer2TakingTurnAsync();

        CurrentCampaignTurn currentTurn = Player2Turn();
        ArrangeValidPlayer(currentTurn);

        CurrentCampaignTurn nextRoundFirstTurn = Player1Turn() with { CurrentRoundNumber = 2, PlayerTurnId = null };
        _campaignTurnService
            .GetFirstCampaignTurnAsync(Arg.Any<AscendantEmpireContext>(), _gameId, Arg.Any<CancellationToken>())
            .Returns(ServiceResult<CurrentCampaignTurn>.Succeed(nextRoundFirstTurn));

        EndCampaignTurnRequest request = new EndCampaignTurnRequest(_gameId, _player2Id, EndTurnMode.ForceEnd);
        await _handler.Handle(request, CancellationToken.None);

        await using AscendantEmpireContext verifyCtx = await DbFactory.CreateDbContextAsync();
        TurnQueue queue = verifyCtx.TurnQueues.First(q => q.Id == _queueId);
        TurnQueueParticipant p2 = verifyCtx.TurnQueueParticipants.First(p => p.Id == _participant2Id);

        using (Assert.EnterMultipleScope())
        {
            // Without this the queue sits on the same round for ever, and GetFirstCampaignTurnAsync finds nobody Pending to lead the next one.
            Assert.That(queue.CurrentRoundNumber, Is.EqualTo(2), "The queue should have moved on to the next round.");
            Assert.That(p2.Status, Is.EqualTo(TurnQueueParticipantStatus.Pending), "Player 2 should owe a turn in the new round.");
        }
    }
}
