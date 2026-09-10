using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.MatchMaking.Handlers;
using AscendantEmpire.Application.Features.Turns.Services;
using AscendantEmpire.Application.Features.Turns.Services.TurnPipeline;
using AscendantEmpire.Application.Features.Turns.Services.TurnPipeline.Campaign.StartRound;
using AscendantEmpire.Application.Features.Turns.Services.TurnPipeline.Campaign.StartTurn;
using AscendantEmpire.Domain.Entities.Identity;
using AscendantEmpire.Domain.Entities.Turns;
using AscendantEmpire.Domain.Enums.Games;
using AscendantEmpire.Domain.Enums.Turns;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Tests.Features.MatchMaking;

/// <summary>
/// Tests for <see cref="ClaimPlayerSlotHandler"/>, and in particular for the Game starting when its last slot is taken.
/// </summary>
/// <remarks>
/// The handler opens an explicit transaction and the start it triggers writes through <c>ExecuteUpdateAsync</c>; neither works on the
/// in-memory provider, so these need a real SQL Server. The round service is real rather than mocked, because what these tests are for is
/// the seam between claiming a seat and the Game actually becoming playable.
/// </remarks>
[TestFixture]
public class ClaimPlayerSlotHandlerTests : GameSqlServerTestBase
{
    private ICampaignRoundService _campaignRoundService = null!;
    private ClaimPlayerSlotHandler _handler = null!;

    private int _gameId;
    private int _player1Id; // TurnOrder 1
    private int _player2Id; // TurnOrder 2
    private int _participant1Id;
    private int _user1Id;
    private int _user2Id;

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

        // A Game whose setup has finished but whose seats are all free - exactly what GameSetupService now leaves behind.
        await seedContext.GameIndex
            .Where(g => g.Id == _gameId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(g => g.CreationStatus, GameCreationStatus.SetupComplete)
                .SetProperty(g => g.PlayStatus, GamePlayStatus.AwaitingPlayers));

        _participant1Id = await SeedTurnQueueAsync();

        _user1Id = await SeedUserAsync("claimer-one");
        _user2Id = await SeedUserAsync("claimer-two");

        var startCampaignRoundPipeline = Substitute.For<ITurnServicePipeline<IStartCampaignRoundProcess>>();
        var startCampaignTurnPipeline = Substitute.For<ITurnServicePipeline<IStartCampaignTurnProcess>>();

        startCampaignRoundPipeline.ProcessPipelineAsync(Arg.Any<TurnPipelineContextData>()).Returns(ServiceResultBase.Succeed());
        startCampaignTurnPipeline.ProcessPipelineAsync(Arg.Any<TurnPipelineContextData>()).Returns(ServiceResultBase.Succeed());

        _campaignRoundService = new CampaignRoundService(
            new NullLogger<CampaignRoundService>(),
            new CampaignTurnService(new NullLogger<CampaignTurnService>(), DbFactory),
            new PlayerTurnService(new NullLogger<PlayerTurnService>(), DbFactory),
            startCampaignRoundPipeline,
            startCampaignTurnPipeline);

        _handler = new ClaimPlayerSlotHandler(new NullLogger<ClaimPlayerSlotHandler>(), DbFactory, _campaignRoundService);
    }

    /// <summary>Seeds the Campaign Turn Queue setup would have created: Round 1, everybody Pending, no clock running.</summary>
    /// <returns>The Id of the Participant holding TurnOrder 1.</returns>
    private async Task<int> SeedTurnQueueAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        var queue = new TurnQueue
        {
            GameId = _gameId,
            QueueType = TurnQueueType.Campaign,
            BattleId = null,
            CurrentRoundNumber = 1,
            IsCompleted = false,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            TurnQueueParticipants =
            [
                new TurnQueueParticipant { PlayerId = _player1Id, TurnOrder = 1, Status = TurnQueueParticipantStatus.Pending, IsActiveInLane = true },
                new TurnQueueParticipant { PlayerId = _player2Id, TurnOrder = 2, Status = TurnQueueParticipantStatus.Pending, IsActiveInLane = true }
            ]
        };

        context.TurnQueues.Add(queue);
        await context.SaveChangesAsync();

        return queue.TurnQueueParticipants.Single(p => p.TurnOrder == 1).Id;
    }

    private async Task<int> SeedUserAsync(string username)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        var user = new User
        {
            Username = username,
            PasswordHash = string.Empty,
            CreatedAt = DateTimeOffset.UtcNow
        };

        context.Users.Add(user);
        await context.SaveChangesAsync();

        return user.Id;
    }

    private async Task<GamePlayStatus> ReadPlayStatusAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        return await context.GameIndex.Where(g => g.Id == _gameId).Select(g => g.PlayStatus).FirstAsync();
    }

    // =========================================================================
    // Claiming a slot that is not the last one
    // =========================================================================

    [Test, Description("A claim that still leaves a free seat does not start the Game, because the empty seat has nobody to take its Turn.")]
    public async Task ShouldLeaveTheGameWaitingWhenAFreeSlotRemains()
    {
        ServiceResult<Empty> result = await _handler.Handle(new ClaimPlayerSlotRequest(_user1Id, _gameId, _player1Id), CancellationToken.None);

        GamePlayStatus playStatus = await ReadPlayStatusAsync();

        await using AscendantEmpireContext verifyContext = await DbFactory.CreateDbContextAsync();
        TurnQueueParticipant participant1 = verifyContext.TurnQueueParticipants.First(p => p.Id == _participant1Id);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.True);
            Assert.That(playStatus, Is.EqualTo(GamePlayStatus.AwaitingPlayers));
            Assert.That(participant1.Status, Is.EqualTo(TurnQueueParticipantStatus.Pending), "Nobody should be taking a Turn before the Game has started.");
        }
    }

    // =========================================================================
    // Claiming the last slot
    // =========================================================================

    [Test, Description("Claiming the last free seat starts the Game, which is the only thing that ever moves it out of the lobby.")]
    public async Task ShouldStartTheGameWhenTheLastSlotIsClaimed()
    {
        await _handler.Handle(new ClaimPlayerSlotRequest(_user1Id, _gameId, _player1Id), CancellationToken.None);

        ServiceResult<Empty> result = await _handler.Handle(new ClaimPlayerSlotRequest(_user2Id, _gameId, _player2Id), CancellationToken.None);

        GamePlayStatus playStatus = await ReadPlayStatusAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.True);
            Assert.That(playStatus, Is.EqualTo(GamePlayStatus.InProgress));
        }
    }

    [Test, Description("The Game that starts is one that can actually be played: the first Player is taking their Turn and the clock is running.")]
    public async Task ShouldHandTheFirstTurnOutWhenTheLastSlotIsClaimed()
    {
        await _handler.Handle(new ClaimPlayerSlotRequest(_user1Id, _gameId, _player1Id), CancellationToken.None);
        await _handler.Handle(new ClaimPlayerSlotRequest(_user2Id, _gameId, _player2Id), CancellationToken.None);

        await using AscendantEmpireContext verifyContext = await DbFactory.CreateDbContextAsync();
        TurnQueueParticipant participant1 = verifyContext.TurnQueueParticipants.First(p => p.Id == _participant1Id);
        TurnQueue queue = verifyContext.TurnQueues.First(q => q.GameId == _gameId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(participant1.Status, Is.EqualTo(TurnQueueParticipantStatus.TakingTurn), "The Player with the lowest turn order should lead the Game.");
            Assert.That(queue.CurrentTurnStartAt, Is.Not.Null, "The Turn clock should be running now that play has begun.");
            Assert.That(queue.CurrentScheduledTurnEndAt, Is.Not.Null, "The Turn should have a scheduled end.");
        }
    }

    // =========================================================================
    // Refusals - all of which must still hold now the handler also starts games
    // =========================================================================

    [Test, Description("A user who already holds a seat here cannot take a second one, so one person can never play a Game against themselves.")]
    public async Task ShouldRefuseWhenTheUserAlreadyHoldsASlotInThisGame()
    {
        await _handler.Handle(new ClaimPlayerSlotRequest(_user1Id, _gameId, _player1Id), CancellationToken.None);

        ServiceResult<Empty> result = await _handler.Handle(new ClaimPlayerSlotRequest(_user1Id, _gameId, _player2Id), CancellationToken.None);

        GamePlayStatus playStatus = await ReadPlayStatusAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.Conflict));
            Assert.That(playStatus, Is.EqualTo(GamePlayStatus.AwaitingPlayers), "A refused claim leaves a seat free, so the Game must not have started.");
        }
    }

    [Test, Description("A seat somebody else already holds is refused, rather than being quietly handed over to the second caller.")]
    public async Task ShouldRefuseWhenTheSlotIsAlreadyClaimed()
    {
        await _handler.Handle(new ClaimPlayerSlotRequest(_user1Id, _gameId, _player1Id), CancellationToken.None);

        ServiceResult<Empty> result = await _handler.Handle(new ClaimPlayerSlotRequest(_user2Id, _gameId, _player1Id), CancellationToken.None);

        Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.Conflict));
    }

    [Test, Description("A slot id that belongs to no Player in this Game is reported as missing, not as a clash with something that exists.")]
    public async Task ShouldReturnNotFoundWhenTheSlotDoesNotExist()
    {
        ServiceResult<Empty> result = await _handler.Handle(new ClaimPlayerSlotRequest(_user1Id, _gameId, PlayerId: int.MaxValue), CancellationToken.None);

        Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.NotFound));
    }

    [Test, Description("A seat whose Player has been eliminated cannot be taken over, because their Game is finished rather than free to inherit.")]
    public async Task ShouldRefuseWhenTheSlotIsNotActive()
    {
        await using (AscendantEmpireContext arrangeContext = await DbFactory.CreateDbContextAsync())
        {
            await arrangeContext.Players
                .Where(p => p.Id == _player1Id)
                .ExecuteUpdateAsync(setters => setters.SetProperty(p => p.Status, PlayerStatus.Eliminated));
        }

        ServiceResult<Empty> result = await _handler.Handle(new ClaimPlayerSlotRequest(_user1Id, _gameId, _player1Id), CancellationToken.None);

        Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.Conflict));
    }

    [Test, Description("A Game only counts Active slots when deciding it is full, so claiming the last one that is left starts it even though a slot is unclaimed.")]
    public async Task ShouldStartTheGameWhenTheOnlyRemainingFreeSlotIsNotActive()
    {
        // Player 2 will never claim their seat, so Player 1's is the last one that can be taken.
        await using (AscendantEmpireContext arrangeContext = await DbFactory.CreateDbContextAsync())
        {
            await arrangeContext.Players
                .Where(p => p.Id == _player2Id)
                .ExecuteUpdateAsync(setters => setters.SetProperty(p => p.Status, PlayerStatus.ExcludedDueToInactivity));
        }

        ServiceResult<Empty> result = await _handler.Handle(new ClaimPlayerSlotRequest(_user1Id, _gameId, _player1Id), CancellationToken.None);

        GamePlayStatus playStatus = await ReadPlayStatusAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.True);
            Assert.That(playStatus, Is.EqualTo(GamePlayStatus.InProgress));
        }
    }
}
