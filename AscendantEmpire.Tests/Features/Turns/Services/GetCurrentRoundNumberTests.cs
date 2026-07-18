using AscendantEmpire.Application.Data;
using AscendantEmpire.Domain.Entities.Turns;
using AscendantEmpire.Domain.Enums.Turns;

using Microsoft.EntityFrameworkCore;

namespace AscendantEmpire.Tests.Features.Turns.Services;

/// <summary>
/// Integration tests for <see cref="Application.Features.Turns.Services.CampaignTurnService.GetCurrentRoundNumberAsync"/>.
/// </summary>
/// <remarks>
/// The distinguishing behaviour from <c>GetCurrentCampaignTurnAsync</c> is that this lookup does not require any
/// participant to be actively taking their turn, and it falls back to the <c>0</c> sentinel rather than <c>NotFound</c>
/// when there is no active campaign queue — the cases below pin both of those down.
/// </remarks>
[TestFixture]
public class GetCurrentRoundNumberTests : CampaignTurnServiceTestBase
{
    /// <summary>
    /// With no Campaign TurnQueue for the game at all, the lookup should yield the <c>0</c> sentinel rather than throw or fault.
    /// </summary>
    [Test]
    public async Task ShouldReturnZeroWhenNoCampaignTurnQueueExists()
    {
        int gameId = await SeedGameWithPlayersAsync(_numberOfPlayers);

        int roundNumber = await Service.GetCurrentRoundNumberAsync(gameId);

        Assert.That(roundNumber, Is.EqualTo(0), "Should return the 0 sentinel when no Campaign TurnQueue exists.");
    }

    /// <summary>
    /// An active Campaign TurnQueue's round number should be returned even when no participant is TakingTurn
    /// (both seeded as <see cref="TurnQueueParticipantStatus.Pending"/>) — this is exactly the case where
    /// <c>GetCurrentCampaignTurnAsync</c> would return NotFound.
    /// </summary>
    [Test]
    public async Task ShouldReturnRoundNumberWhenActiveQueueExistsWithNoParticipantTakingTurn()
    {
        int gameId = await SeedGameWithPlayersAsync(_numberOfPlayers);

        // SeedCampaignTurnQueueAsync seeds the queue with CurrentRoundNumber = 1.
        await SeedCampaignTurnQueueAsync(gameId, TurnQueueParticipantStatus.Pending, TurnQueueParticipantStatus.Pending);

        int roundNumber = await Service.GetCurrentRoundNumberAsync(gameId);

        Assert.That(roundNumber, Is.EqualTo(1), "Should return the active queue's round number regardless of participant status.");
    }

    /// <summary>
    /// A completed Campaign TurnQueue is not "active", so it should be excluded and the lookup fall back to <c>0</c> —
    /// this guards the <c>!q.IsCompleted</c> filter.
    /// </summary>
    [Test]
    public async Task ShouldReturnZeroWhenOnlyQueueIsCompleted()
    {
        int gameId = await SeedGameWithPlayersAsync(_numberOfPlayers);
        await SeedCampaignTurnQueueAsync(gameId, TurnQueueParticipantStatus.Pending, TurnQueueParticipantStatus.Pending);

        // Flip the seeded queue to completed on a fresh context so it no longer counts as active.
        await using (AscendantEmpireContext ctx = await DbFactory.CreateDbContextAsync())
        {
            TurnQueue queue = await ctx.TurnQueues.SingleAsync(q => q.GameId == gameId);
            queue.IsCompleted = true;
            await ctx.SaveChangesAsync();
        }

        int roundNumber = await Service.GetCurrentRoundNumberAsync(gameId);

        Assert.That(roundNumber, Is.EqualTo(0), "A completed queue is not active, so the lookup should return the 0 sentinel.");
    }
}
