using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Turns.Services;
using AscendantEmpire.Application.Features.Turns.Services.TurnPipeline;
using AscendantEmpire.Application.Features.Turns.Services.TurnPipeline.Campaign.StartRound;
using AscendantEmpire.Application.Features.Turns.Services.TurnPipeline.Campaign.StartTurn;
using AscendantEmpire.Domain.Entities.Turns;
using AscendantEmpire.Domain.Enums.Games;
using AscendantEmpire.Domain.Enums.Turns;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

namespace AscendantEmpire.Tests.Features.Turns.Services;

/// <summary>
/// Tests for <see cref="ICampaignRoundService"/>, the shared machinery behind both starting a Game and rolling a Round over.
/// </summary>
/// <remarks>
/// Every write here goes through <c>ExecuteUpdateAsync</c>, which the in-memory provider cannot run, so these need a real SQL Server.
/// The turn lookups are the real <see cref="CampaignTurnService"/> rather than a mock, because what makes these tests worth having is
/// that the service's writes and that service's reads agree about which Participant is taking their Turn.
/// </remarks>
[TestFixture]
public class CampaignRoundServiceTests : GameSqlServerTestBase
{
    private ITurnServicePipeline<IStartCampaignRoundProcess> _startCampaignRoundPipeline = null!;
    private ITurnServicePipeline<IStartCampaignTurnProcess> _startCampaignTurnPipeline = null!;
    private ICampaignTurnService _campaignTurnService = null!;
    private ICampaignRoundService _service = null!;

    private int _gameId;
    private int _queueId;
    private int _player1Id;
    private int _player2Id;
    private int _participant1Id; // TurnOrder 1
    private int _participant2Id; // TurnOrder 2

    [SetUp]
    public async Task SetUp()
    {
        await SeedGameAsync(2, 2, 2);
        _gameId = await SeedGameWithPlayersAsync(2);

        await using AscendantEmpireContext seedContext = await DbFactory.CreateDbContextAsync();

        List<int> playerIds = [.. seedContext.Players
            .Where(p => p.GameId == _gameId)
            .OrderBy(p => p.Id)
            .Select(p => p.Id)];

        _player1Id = playerIds[0];
        _player2Id = playerIds[1];

        (_queueId, _participant1Id, _participant2Id) = await SeedTurnQueueAsync();

        _startCampaignRoundPipeline = Substitute.For<ITurnServicePipeline<IStartCampaignRoundProcess>>();
        _startCampaignTurnPipeline = Substitute.For<ITurnServicePipeline<IStartCampaignTurnProcess>>();

        _startCampaignRoundPipeline.ProcessPipelineAsync(Arg.Any<TurnPipelineContextData>()).Returns(ServiceResultBase.Succeed());
        _startCampaignTurnPipeline.ProcessPipelineAsync(Arg.Any<TurnPipelineContextData>()).Returns(ServiceResultBase.Succeed());

        _campaignTurnService = new CampaignTurnService(new NullLogger<CampaignTurnService>(), DbFactory);

        _service = new CampaignRoundService(
            new NullLogger<CampaignRoundService>(),
            _campaignTurnService,
            _startCampaignRoundPipeline,
            _startCampaignTurnPipeline);
    }

    /// <summary>
    /// Seeds a Campaign Turn Queue on Round 1 with two Participants, both Pending and nobody taking a Turn - the state game setup leaves behind.
    /// </summary>
    private async Task<(int queueId, int participant1Id, int participant2Id)> SeedTurnQueueAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        var queue = new TurnQueue
        {
            GameId = _gameId,
            QueueType = TurnQueueType.Campaign,
            BattleId = null,
            CurrentRoundNumber = 1,
            CurrentTurnStartAt = null,
            CurrentScheduledTurnEndAt = null,
            CurrentActualTurnEndAt = null,
            IsCompleted = false,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            TurnQueueParticipants =
            [
                new TurnQueueParticipant
                {
                    PlayerId = _player1Id,
                    TurnOrder = 1,
                    Status = TurnQueueParticipantStatus.Pending,
                    IsActiveInLane = true
                },
                new TurnQueueParticipant
                {
                    PlayerId = _player2Id,
                    TurnOrder = 2,
                    Status = TurnQueueParticipantStatus.Pending,
                    IsActiveInLane = true
                }
            ]
        };

        context.TurnQueues.Add(queue);
        await context.SaveChangesAsync();

        List<TurnQueueParticipant> participants = [.. queue.TurnQueueParticipants.OrderBy(p => p.TurnOrder)];

        return (queue.Id, participants[0].Id, participants[1].Id);
    }

    /// <summary>Puts the Game into the lobby state game setup now leaves it in.</summary>
    private async Task ArrangeAwaitingPlayersAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        await context.GameIndex
            .Where(g => g.Id == _gameId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(g => g.CreationStatus, GameCreationStatus.SetupComplete)
                .SetProperty(g => g.PlayStatus, GamePlayStatus.AwaitingPlayers));
    }

    // =========================================================================
    // StartRoundAsync
    // =========================================================================

    [Test, Description("Opening a Round hands the Turn to the lowest active TurnOrder, because a Round with nobody taking a Turn is a Game nobody can play.")]
    public async Task ShouldGiveTheTurnToTheLowestTurnOrderWhenTheRoundOpens()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        ServiceResultBase result = await _service.StartRoundAsync(context, _queueId, roundNumber: 1, _gameId);

        await using AscendantEmpireContext verifyContext = await DbFactory.CreateDbContextAsync();
        TurnQueueParticipant participant1 = verifyContext.TurnQueueParticipants.First(p => p.Id == _participant1Id);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.True);
            Assert.That(participant1.Status, Is.EqualTo(TurnQueueParticipantStatus.TakingTurn), "The Player leading the Round should be taking their Turn.");
            Assert.That(participant1.LastTurnStartedAt, Is.Not.Null, "The leading Player's Turn should have been stamped as started.");
        }
    }

    [Test, Description("Everyone but the leader is left Pending, so each of them still owes a Turn in the Round that has just opened.")]
    public async Task ShouldLeaveEveryOtherParticipantPendingWhenTheRoundOpens()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        await _service.StartRoundAsync(context, _queueId, roundNumber: 1, _gameId);

        await using AscendantEmpireContext verifyContext = await DbFactory.CreateDbContextAsync();
        TurnQueueParticipant participant2 = verifyContext.TurnQueueParticipants.First(p => p.Id == _participant2Id);

        Assert.That(participant2.Status, Is.EqualTo(TurnQueueParticipantStatus.Pending), "Player 2 has not played yet, so they should still owe a Turn.");
    }

    [Test, Description("A Participant who finished the previous Round is brought back to Pending, otherwise ending a Round would drop them out of the Game for good.")]
    public async Task ShouldBringCompletedParticipantsBackIntoPlayWhenTheRoundOpens()
    {
        await using (AscendantEmpireContext arrangeContext = await DbFactory.CreateDbContextAsync())
        {
            await arrangeContext.TurnQueueParticipants
                .Where(p => p.TurnQueueId == _queueId)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(p => p.Status, TurnQueueParticipantStatus.TurnCompleted_Manual));
        }

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        ServiceResultBase result = await _service.StartRoundAsync(context, _queueId, roundNumber: 2, _gameId);

        await using AscendantEmpireContext verifyContext = await DbFactory.CreateDbContextAsync();
        TurnQueueParticipant participant1 = verifyContext.TurnQueueParticipants.First(p => p.Id == _participant1Id);
        TurnQueueParticipant participant2 = verifyContext.TurnQueueParticipants.First(p => p.Id == _participant2Id);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.True);
            Assert.That(participant1.Status, Is.EqualTo(TurnQueueParticipantStatus.TakingTurn), "The Round's leader should be taking their Turn again.");
            Assert.That(participant2.Status, Is.EqualTo(TurnQueueParticipantStatus.Pending), "Player 2 should owe a Turn in the new Round.");
        }
    }

    [Test, Description("The Queue moves on to the Round being opened and its Turn clock restarts, so the Round number and the clock cannot drift apart.")]
    public async Task ShouldAdvanceTheRoundAndRestartTheClockWhenTheRoundOpens()
    {
        DateTimeOffset before = DateTimeOffset.UtcNow;

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        await _service.StartRoundAsync(context, _queueId, roundNumber: 3, _gameId);

        await using AscendantEmpireContext verifyContext = await DbFactory.CreateDbContextAsync();
        TurnQueue queue = verifyContext.TurnQueues.First(q => q.Id == _queueId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(queue.CurrentRoundNumber, Is.EqualTo(3), "The Queue should be on the Round it was asked to open.");
            Assert.That(queue.CurrentTurnStartAt, Is.Not.Null.And.GreaterThanOrEqualTo(before), "The Turn clock should have started.");
            Assert.That(queue.CurrentActualTurnEndAt, Is.Null, "Nobody has finished the Turn that just began.");
        }
    }

    [Test, Description("A Turn is scheduled to end 24 hours after it starts, which is the one duration the whole application now uses.")]
    public async Task ShouldScheduleTheTurnToEndTwentyFourHoursAfterItStarts()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        await _service.StartRoundAsync(context, _queueId, roundNumber: 1, _gameId);

        await using AscendantEmpireContext verifyContext = await DbFactory.CreateDbContextAsync();
        TurnQueue queue = verifyContext.TurnQueues.First(q => q.Id == _queueId);

        Assert.That(queue.CurrentScheduledTurnEndAt!.Value - queue.CurrentTurnStartAt!.Value,
            Is.EqualTo(TimeSpan.FromHours(24)).Within(TimeSpan.FromSeconds(1)));
    }

    [Test, Description("The Round the service opens is the Round the current-turn lookup then reports, because the two disagreeing is what made a new Game unplayable.")]
    public async Task ShouldLeaveACurrentTurnTheTurnLookupCanFindWhenTheRoundOpens()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        await _service.StartRoundAsync(context, _queueId, roundNumber: 1, _gameId);

        ServiceResultBase.ServiceResult<BackendContracts.Turns.GetCurrentCampaignTurn.CurrentCampaignTurn> currentTurn =
            await _campaignTurnService.GetCurrentCampaignTurnAsync(_gameId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(currentTurn.HasSucceeded, Is.True, "There should be a current Turn to find.");
            Assert.That(currentTurn.SuccessValue!.PlayerId, Is.EqualTo(_player1Id), "The Player leading the Round should be the one whose Turn it is.");
        }
    }

    [Test, Description("A failing start-of-round pipeline stops the Round rather than being ignored, so a process that could not run never silently skips a Round.")]
    public async Task ShouldFailWhenTheStartRoundPipelineFails()
    {
        _startCampaignRoundPipeline
            .ProcessPipelineAsync(Arg.Any<TurnPipelineContextData>())
            .Returns(ServiceResultBase.Fail("start round pipeline failed"));

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        ServiceResultBase result = await _service.StartRoundAsync(context, _queueId, roundNumber: 1, _gameId);

        await using AscendantEmpireContext verifyContext = await DbFactory.CreateDbContextAsync();
        TurnQueueParticipant participant1 = verifyContext.TurnQueueParticipants.First(p => p.Id == _participant1Id);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(participant1.Status, Is.EqualTo(TurnQueueParticipantStatus.Pending), "Nobody should have been handed the Turn once the Round has failed to open.");
        }
    }

    // =========================================================================
    // StartGameAsync
    // =========================================================================

    [Test, Description("Starting a full Game moves it to InProgress, which is what the rest of the application treats as the Game being playable.")]
    public async Task ShouldMoveTheGameToInProgressWhenItStarts()
    {
        await ArrangeAwaitingPlayersAsync();

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        ServiceResultBase result = await _service.StartGameAsync(context, _gameId);

        await using AscendantEmpireContext verifyContext = await DbFactory.CreateDbContextAsync();
        GamePlayStatus playStatus = verifyContext.GameIndex.First(g => g.Id == _gameId).PlayStatus;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.True);
            Assert.That(playStatus, Is.EqualTo(GamePlayStatus.InProgress));
        }
    }

    [Test, Description("Starting a Game opens the Round the setup left waiting rather than skipping past it, so play begins on Round 1.")]
    public async Task ShouldOpenTheFirstRoundWhenTheGameStarts()
    {
        await ArrangeAwaitingPlayersAsync();

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        await _service.StartGameAsync(context, _gameId);

        await using AscendantEmpireContext verifyContext = await DbFactory.CreateDbContextAsync();
        TurnQueue queue = verifyContext.TurnQueues.First(q => q.Id == _queueId);
        TurnQueueParticipant participant1 = verifyContext.TurnQueueParticipants.First(p => p.Id == _participant1Id);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(queue.CurrentRoundNumber, Is.EqualTo(1), "A Game starts on the Round its Queue was created with, not the one after it.");
            Assert.That(participant1.Status, Is.EqualTo(TurnQueueParticipantStatus.TakingTurn), "The first Player should be taking their Turn.");
        }
    }

    [Test, Description("A Game already under way is left exactly as it is, so two Players racing to claim the last slot cannot restart a Round somebody is playing.")]
    public async Task ShouldLeaveTheGameAloneWhenItHasAlreadyStarted()
    {
        await ArrangeAwaitingPlayersAsync();

        await using (AscendantEmpireContext startContext = await DbFactory.CreateDbContextAsync())
        {
            await _service.StartGameAsync(startContext, _gameId);
        }

        // Player 1 plays on: the Turn has moved to Player 2 by the time the second caller arrives.
        await using (AscendantEmpireContext playContext = await DbFactory.CreateDbContextAsync())
        {
            await playContext.TurnQueueParticipants
                .Where(p => p.Id == _participant1Id)
                .ExecuteUpdateAsync(setters => setters.SetProperty(p => p.Status, TurnQueueParticipantStatus.TurnCompleted_Manual));
            await playContext.TurnQueueParticipants
                .Where(p => p.Id == _participant2Id)
                .ExecuteUpdateAsync(setters => setters.SetProperty(p => p.Status, TurnQueueParticipantStatus.TakingTurn));
        }

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        ServiceResultBase result = await _service.StartGameAsync(context, _gameId);

        await using AscendantEmpireContext verifyContext = await DbFactory.CreateDbContextAsync();
        TurnQueueParticipant participant1 = verifyContext.TurnQueueParticipants.First(p => p.Id == _participant1Id);
        TurnQueueParticipant participant2 = verifyContext.TurnQueueParticipants.First(p => p.Id == _participant2Id);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.True, "A Game that is already going is not an error, just nothing left to do.");
            Assert.That(participant1.Status, Is.EqualTo(TurnQueueParticipantStatus.TurnCompleted_Manual), "Player 1 should still have finished their Turn.");
            Assert.That(participant2.Status, Is.EqualTo(TurnQueueParticipantStatus.TakingTurn), "Player 2's Turn should not have been taken away from them.");
        }
    }

    [Test, Description("A Game with no Campaign Turn Queue cannot start, because there is nothing to open a Round on and no way to say whose Turn it is.")]
    public async Task ShouldFailWhenThereIsNoCampaignTurnQueue()
    {
        await ArrangeAwaitingPlayersAsync();

        await using (AscendantEmpireContext arrangeContext = await DbFactory.CreateDbContextAsync())
        {
            await arrangeContext.TurnQueues
                .Where(q => q.Id == _queueId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(q => q.IsCompleted, true));
        }

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        ServiceResultBase result = await _service.StartGameAsync(context, _gameId);

        Assert.That(result.HasSucceeded, Is.False);
    }
}
