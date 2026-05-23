using AscendantEmpire.Application.Common;
using AscendantEmpire.BackendContracts.Turns.GetCurrentCampaignTurn;
using AscendantEmpire.Domain.Enums.Turns;

namespace AscendantEmpire.Tests.Features.Turns.Services;

/// <summary>
/// Integration tests for <see cref="Application.Features.Turns.Services.CampaignTurnService.GetCurrentCampaignTurnAsync"/>.
/// </summary>
[TestFixture]
public class GetCurrentCampaignTurnTests : CampaignTurnServiceTestBase
{
    // -------------------------------------------------------------------------
    // GameId validation
    // -------------------------------------------------------------------------

    [TestCase(0, Description = "GameId of zero fails the GreaterThan(0) guard.")]
    [TestCase(-1, Description = "Negative GameId fails the GreaterThan(0) guard.")]
    public async Task ShouldFailWhenGameIdIsNotGreaterThanZero(int invalidGameId)
    {
        ServiceResultBase.ServiceResult<CurrentCampaignTurn> result =
            await Service.GetCurrentCampaignTurnAsync(invalidGameId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False, "Result should not succeed for an invalid GameId.");
            Assert.That(result.Errors, Has.Some.EqualTo("Game Id must be greater than 0."));
        }
    }

    // -------------------------------------------------------------------------
    // Not found — no matching participant
    // -------------------------------------------------------------------------

    /// <summary>
    /// When there is no Campaign TurnQueue for the game at all, the service should return <c>NotFound</c>.
    /// </summary>
    [Test]
    public async Task ShouldReturnNotFoundWhenNoTurnQueueExists()
    {
        int gameId = await SeedGameWithPlayersAsync(_numberOfPlayers);

        ServiceResultBase.ServiceResult<CurrentCampaignTurn> result =
            await Service.GetCurrentCampaignTurnAsync(gameId);

        Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.NotFound), "Should return NotFound when no Campaign TurnQueue exists.");
    }

    /// <summary>
    /// When a Campaign TurnQueue exists but no participant has <see cref="TurnQueueParticipantStatus.TakingTurn"/> status
    /// (e.g. all are still <see cref="TurnQueueParticipantStatus.Pending"/>), the service should return <c>NotFound</c>.
    /// </summary>
    [Test]
    public async Task ShouldReturnNotFoundWhenNoParticipantIsTakingTurn()
    {
        int gameId = await SeedGameWithPlayersAsync(_numberOfPlayers);
        await SeedCampaignTurnQueueAsync(gameId, TurnQueueParticipantStatus.Pending, TurnQueueParticipantStatus.Pending);

        ServiceResultBase.ServiceResult<CurrentCampaignTurn> result = await Service.GetCurrentCampaignTurnAsync(gameId);

        Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.NotFound), "Should return NotFound when no participant has TakingTurn status.");
    }

    // -------------------------------------------------------------------------
    // Success — single active participant
    // -------------------------------------------------------------------------

    /// <summary>
    /// When exactly one participant has <see cref="TurnQueueParticipantStatus.TakingTurn"/> status and is active in the lane, the service should return its details.
    /// </summary>
    [Test]
    public async Task ShouldReturnCurrentTurnWhenOneParticipantIsTakingTurn()
    {
        int gameId = await SeedGameWithPlayersAsync(_numberOfPlayers);
        await SeedCampaignTurnQueueAsync(gameId, TurnQueueParticipantStatus.TakingTurn, TurnQueueParticipantStatus.Pending);

        ServiceResultBase.ServiceResult<CurrentCampaignTurn> result = await Service.GetCurrentCampaignTurnAsync(gameId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.True, "Result should succeed when one participant is TakingTurn.");
            Assert.That(result.SuccessValue.GameId, Is.EqualTo(gameId));
            Assert.That(result.SuccessValue.TurnOrder, Is.EqualTo(1), "The participant with TurnOrder 1 is TakingTurn.");
            Assert.That(result.SuccessValue.CurrentRoundNumber, Is.EqualTo(1));
        }
    }

    /// <summary>
    /// When the active participant is the last in the round (highest TurnOrder among active participants),
    /// <see cref="CurrentCampaignTurn.IsLastTurnInRound"/> should be <c>true</c>.
    /// </summary>
    [Test]
    public async Task ShouldSetIsLastTurnInRoundTrueForHighestTurnOrderParticipant()
    {
        int gameId = await SeedGameWithPlayersAsync(_numberOfPlayers);

        // Player with TurnOrder 2 (highest) is TakingTurn.
        await SeedCampaignTurnQueueAsync(gameId, TurnQueueParticipantStatus.Pending, TurnQueueParticipantStatus.TakingTurn);

        ServiceResultBase.ServiceResult<CurrentCampaignTurn> result = await Service.GetCurrentCampaignTurnAsync(gameId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.True, "Result should succeed.");
            Assert.That(result.SuccessValue.IsLastTurnInRound, Is.True, "IsLastTurnInRound should be true for the participant with the highest TurnOrder.");
        }
    }

    /// <summary>
    /// When the active participant is not the last in the round, <see cref="CurrentCampaignTurn.IsLastTurnInRound"/> should be <c>false</c>.
    /// </summary>
    [Test]
    public async Task ShouldSetIsLastTurnInRoundFalseForNonHighestTurnOrderParticipant()
    {
        int gameId = await SeedGameWithPlayersAsync(_numberOfPlayers);

        // Player with TurnOrder 1 (not the highest) is TakingTurn.
        await SeedCampaignTurnQueueAsync(gameId, TurnQueueParticipantStatus.TakingTurn, TurnQueueParticipantStatus.Pending);

        ServiceResultBase.ServiceResult<CurrentCampaignTurn> result = await Service.GetCurrentCampaignTurnAsync(gameId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.True, "Result should succeed.");
            Assert.That(result.SuccessValue.IsLastTurnInRound, Is.False, "IsLastTurnInRound should be false for a participant that is not the last in the round.");
        }
    }
}
