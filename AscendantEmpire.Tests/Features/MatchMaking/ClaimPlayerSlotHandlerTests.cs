using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.MatchMaking.Handlers;
using AscendantEmpire.Domain.Entities.Identity;
using AscendantEmpire.Domain.Entities.Turns;
using AscendantEmpire.Domain.Enums.Games;
using AscendantEmpire.Domain.Enums.Turns;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Tests.Features.MatchMaking;

/// <summary>
/// Tests for <see cref="ClaimPlayerSlotHandler"/>: who may take a seat, and that taking one never starts the Game.
/// </summary>
/// <remarks>
/// <para>
/// A claim can no longer be the last event in filling a lobby - the player still has to say who they are in the seat - so the Game start moved to
/// <c>UpdatePlayerDefinitionHandler</c>. The tests below pin that it did not come back: a full lobby must still read as waiting.
/// <c>StartOfGameTests</c> covers the start itself, end to end.
/// </para>
/// <para>
/// Tier 3 because the fixture arranges through <c>ExecuteUpdateAsync</c> and leans on the real Game setup helpers, neither of which the in-memory
/// provider reproduces.
/// </para>
/// </remarks>
[TestFixture]
public class ClaimPlayerSlotHandlerTests : GameSqlServerTestBase
{
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

        _handler = new ClaimPlayerSlotHandler(new NullLogger<ClaimPlayerSlotHandler>(), DbFactory);
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
    // Claiming a seat never starts the Game
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

    [Test, Description("Claiming the last free seat leaves the Game in the lobby, because a seated player who has not defined themselves has no identity to play as.")]
    public async Task ShouldLeaveTheGameWaitingWhenTheLastSlotIsClaimed()
    {
        await _handler.Handle(new ClaimPlayerSlotRequest(_user1Id, _gameId, _player1Id), CancellationToken.None);

        ServiceResult<Empty> result = await _handler.Handle(new ClaimPlayerSlotRequest(_user2Id, _gameId, _player2Id), CancellationToken.None);

        GamePlayStatus playStatus = await ReadPlayStatusAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.True);
            Assert.That(playStatus, Is.EqualTo(GamePlayStatus.AwaitingPlayers));
        }
    }

    [Test, Description("A full lobby hands nobody a Turn, so the Turn clock stays unset until the last player has defined themselves.")]
    public async Task ShouldHandNobodyATurnWhenTheLastSlotIsClaimed()
    {
        await _handler.Handle(new ClaimPlayerSlotRequest(_user1Id, _gameId, _player1Id), CancellationToken.None);
        await _handler.Handle(new ClaimPlayerSlotRequest(_user2Id, _gameId, _player2Id), CancellationToken.None);

        await using AscendantEmpireContext verifyContext = await DbFactory.CreateDbContextAsync();
        TurnQueueParticipant participant1 = verifyContext.TurnQueueParticipants.First(p => p.Id == _participant1Id);
        TurnQueue queue = verifyContext.TurnQueues.First(q => q.GameId == _gameId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(participant1.Status, Is.EqualTo(TurnQueueParticipantStatus.Pending), "No Turn should have been handed out by a claim.");
            Assert.That(queue.CurrentTurnStartAt, Is.Null, "The Turn clock must not be running before play begins.");
            Assert.That(queue.CurrentScheduledTurnEndAt, Is.Null, "A Turn that has not begun has no scheduled end.");
        }
    }

    // =========================================================================
    // Refusals
    // =========================================================================

    [Test, Description("A user who already holds a seat here cannot take a second one, so one person can never play a Game against themselves.")]
    public async Task ShouldRefuseWhenTheUserAlreadyHoldsASlotInThisGame()
    {
        await _handler.Handle(new ClaimPlayerSlotRequest(_user1Id, _gameId, _player1Id), CancellationToken.None);

        ServiceResult<Empty> result = await _handler.Handle(new ClaimPlayerSlotRequest(_user1Id, _gameId, _player2Id), CancellationToken.None);

        Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.Conflict));
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

    [Test, Description("A seat left free by an inactive slot is still claimable, and taking it is still not what starts the Game.")]
    public async Task ShouldLeaveTheGameWaitingWhenTheOnlyRemainingFreeSlotIsNotActive()
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
            Assert.That(playStatus, Is.EqualTo(GamePlayStatus.AwaitingPlayers));
        }
    }
}
