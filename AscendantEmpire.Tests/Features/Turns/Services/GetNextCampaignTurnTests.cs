using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.BackendContracts.Turns.GetCurrentCampaignTurn;
using AscendantEmpire.Domain.Entities.Turns;
using AscendantEmpire.Domain.Enums.Turns;

namespace AscendantEmpire.Tests.Features.Turns.Services;

/// <summary>
/// Integration tests for <see cref="Application.Features.Turns.Services.CampaignTurnService.GetNextCampaignTurnAsync"/>.
/// </summary>
[TestFixture]
public class GetNextCampaignTurnTests : CampaignTurnServiceTestBase
{
    // -------------------------------------------------------------------------
    // GameId validation
    // -------------------------------------------------------------------------

    [TestCase(0, Description = "GameId of zero fails the GreaterThan(0) guard.")]
    [TestCase(-1, Description = "Negative GameId fails the GreaterThan(0) guard.")]
    public async Task ShouldFailWhenGameIdIsNotGreaterThanZero(int invalidGameId)
    {
        ServiceResultBase.ServiceResult<CurrentCampaignTurn> result =
            await Service.GetNextCampaignTurnAsync(invalidGameId, turnOrderOfLastPlayer: 1);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False, "Result should not succeed for an invalid GameId.");
            Assert.That(result.Errors, Has.Some.EqualTo("Game Id must be greater than 0."));
        }
    }

    // -------------------------------------------------------------------------
    // Not found
    // -------------------------------------------------------------------------

    /// <summary>
    /// When there is no Campaign TurnQueue for the game at all, the service should return <c>NotFound</c>.
    /// </summary>
    [Test]
    public async Task ShouldReturnNotFoundWhenNoTurnQueueExists()
    {
        int gameId = await SeedGameWithPlayersAsync(_numberOfPlayers);

        ServiceResultBase.ServiceResult<CurrentCampaignTurn> result =
            await Service.GetNextCampaignTurnAsync(gameId, turnOrderOfLastPlayer: 0);

        Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.NotFound), "Should return NotFound when no Campaign TurnQueue exists.");
    }

    /// <summary>
    /// When all participants have <see cref="TurnQueueParticipantStatus.TakingTurn"/> status
    /// (none are Pending), the service should return <c>NotFound</c>.
    /// </summary>
    [Test]
    public async Task ShouldReturnNotFoundWhenNoPendingParticipantExistsAfterLastPlayer()
    {
        int gameId = await SeedGameWithPlayersAsync(_numberOfPlayers);

        // Both participants are TakingTurn — no Pending participant follows.
        await SeedCampaignTurnQueueAsync(gameId, TurnQueueParticipantStatus.TakingTurn, TurnQueueParticipantStatus.TakingTurn);

        ServiceResultBase.ServiceResult<CurrentCampaignTurn> result =
            await Service.GetNextCampaignTurnAsync(gameId, turnOrderOfLastPlayer: 0);

        Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.NotFound), "Should return NotFound when no Pending participant follows the last player.");
    }

    /// <summary>
    /// When a Pending participant exists but its TurnOrder is not greater than <paramref name="turnOrderOfLastPlayer"/>,
    /// the service should return <c>NotFound</c>.
    /// </summary>
    [Test]
    public async Task ShouldReturnNotFoundWhenPendingParticipantTurnOrderIsNotHigherThanLastPlayer()
    {
        int gameId = await SeedGameWithPlayersAsync(_numberOfPlayers);

        // Player 1 (TurnOrder 1) is Pending, Player 2 (TurnOrder 2) is TakingTurn.
        await SeedCampaignTurnQueueAsync(gameId, TurnQueueParticipantStatus.Pending, TurnQueueParticipantStatus.TakingTurn);

        // Pass turnOrderOfLastPlayer = 2, so TurnOrder 1 does not qualify.
        ServiceResultBase.ServiceResult<CurrentCampaignTurn> result =
            await Service.GetNextCampaignTurnAsync(gameId, turnOrderOfLastPlayer: 2);

        Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.NotFound), "Should return NotFound when no Pending participant has a TurnOrder greater than turnOrderOfLastPlayer.");
    }

    // -------------------------------------------------------------------------
    // Success
    // -------------------------------------------------------------------------

    /// <summary>
    /// When a Pending participant with a higher TurnOrder exists, the service should return its details.
    /// </summary>
    [Test]
    public async Task ShouldReturnNextParticipantWhenPendingParticipantFollowsLastPlayer()
    {
        int gameId = await SeedGameWithPlayersAsync(_numberOfPlayers);

        // Player 1 (TurnOrder 1) is TakingTurn, Player 2 (TurnOrder 2) is Pending — Player 2 is next.
        await SeedCampaignTurnQueueAsync(gameId, TurnQueueParticipantStatus.TakingTurn, TurnQueueParticipantStatus.Pending);

        ServiceResultBase.ServiceResult<CurrentCampaignTurn> result =
            await Service.GetNextCampaignTurnAsync(gameId, turnOrderOfLastPlayer: 1);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.True, "Result should succeed when a Pending participant follows the last player.");
            Assert.That(result.SuccessValue.GameId, Is.EqualTo(gameId));
            Assert.That(result.SuccessValue.TurnOrder, Is.EqualTo(2), "The participant with TurnOrder 2 should be returned as the next turn.");
            Assert.That(result.SuccessValue.CurrentRoundNumber, Is.EqualTo(1));
        }
    }

    /// <summary>
    /// When the next participant has the highest TurnOrder among all active participants,
    /// <see cref="CurrentCampaignTurn.IsLastTurnInRound"/> should be <c>true</c>.
    /// </summary>
    [Test]
    public async Task ShouldSetIsLastTurnInRoundTrueWhenNextParticipantHasHighestTurnOrder()
    {
        int gameId = await SeedGameWithPlayersAsync(_numberOfPlayers);

        // Player 2 (TurnOrder 2, highest) is next and Pending.
        await SeedCampaignTurnQueueAsync(gameId, TurnQueueParticipantStatus.TakingTurn, TurnQueueParticipantStatus.Pending);

        ServiceResultBase.ServiceResult<CurrentCampaignTurn> result =
            await Service.GetNextCampaignTurnAsync(gameId, turnOrderOfLastPlayer: 1);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.True, "Result should succeed.");
            Assert.That(result.SuccessValue.IsLastTurnInRound, Is.True, "IsLastTurnInRound should be true when the next participant has the highest TurnOrder.");
        }
    }

    /// <summary>
    /// When the next participant does not have the highest TurnOrder among all active participants,
    /// <see cref="CurrentCampaignTurn.IsLastTurnInRound"/> should be <c>false</c>.
    /// </summary>
    /// <remarks>
    /// Three participants are seeded: TurnOrder 1 (<see cref="TurnQueueParticipantStatus.TurnCompleted_Manual"/>),
    /// TurnOrder 2 (Pending — the next turn), TurnOrder 3 (<see cref="TurnQueueParticipantStatus.TurnCompleted_Manual"/>).
    /// The service returns TurnOrder 2, and because a participant with TurnOrder 3 is still active in the lane,
    /// IsLastTurnInRound must be false.
    /// A third player is needed here because the service uses SingleOrDefaultAsync; only one Pending participant
    /// may match the filter, yet the max active TurnOrder must exceed the result.
    /// </remarks>
    [Test]
    public async Task ShouldSetIsLastTurnInRoundFalseWhenNextParticipantIsNotLast()
    {
        int gameId = await SeedGameWithPlayersAsync(3);

        await using AscendantEmpireContext ctx = await DbFactory.CreateDbContextAsync();

        List<int> playerIds = [.. ctx.Players
            .Where(p => p.GameId == gameId)
            .OrderBy(p => p.Id)
            .Select(p => p.Id)
            .Take(3)];

        var queue = new TurnQueue
        {
            GameId = gameId,
            QueueType = TurnQueueType.Campaign,
            BattleId = null,
            CurrentRoundNumber = 1,
            IsCompleted = false,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        ctx.TurnQueues.Add(queue);
        await ctx.SaveChangesAsync();

        // TurnOrder 1 and 3 are TurnCompleted_Manual (still active in the lane),
        // TurnOrder 2 is the sole Pending participant that follows TurnOrder 1.
        // The max active TurnOrder is 3, so TurnOrder 2 is not the last.
        ctx.TurnQueueParticipants.AddRange(
            new TurnQueueParticipant { TurnQueueId = queue.Id, PlayerId = playerIds[0], TurnOrder = 1, Status = TurnQueueParticipantStatus.TurnCompleted_Manual, IsActiveInLane = true },
            new TurnQueueParticipant { TurnQueueId = queue.Id, PlayerId = playerIds[1], TurnOrder = 2, Status = TurnQueueParticipantStatus.Pending,              IsActiveInLane = true },
            new TurnQueueParticipant { TurnQueueId = queue.Id, PlayerId = playerIds[2], TurnOrder = 3, Status = TurnQueueParticipantStatus.TurnCompleted_Manual, IsActiveInLane = true });
        await ctx.SaveChangesAsync();

        ServiceResultBase.ServiceResult<CurrentCampaignTurn> result =
            await Service.GetNextCampaignTurnAsync(gameId, turnOrderOfLastPlayer: 1);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.True, "Result should succeed.");
            Assert.That(result.SuccessValue.TurnOrder, Is.EqualTo(2), "The Pending participant with TurnOrder 2 should be returned as the next turn.");
            Assert.That(result.SuccessValue.IsLastTurnInRound, Is.False, "IsLastTurnInRound should be false when a higher-TurnOrder active participant still exists.");
        }
    }
}
