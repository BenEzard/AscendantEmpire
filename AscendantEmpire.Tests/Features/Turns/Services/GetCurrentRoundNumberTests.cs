using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Domain.Entities.Turns;
using AscendantEmpire.Domain.Enums.Turns;

using Microsoft.EntityFrameworkCore;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Tests.Features.Turns.Services;

/// <summary>
/// Integration tests for both <c>GetCurrentRoundNumberAsync</c> overloads on
/// <see cref="Application.Features.Turns.Services.CampaignTurnService"/>.
/// </summary>
/// <remarks>
/// <para>
/// The distinguishing behaviour from <c>GetCurrentCampaignTurnAsync</c> is that this lookup does not require any
/// participant to be actively taking their turn — the cases below pin that down for both overloads.
/// </para>
/// <para>
/// Where the two differ is the answer when no lane is live. The game-scoped overload falls back to the <c>0</c>
/// sentinel, because its callers stamp turn-scoped rows that may legitimately predate the campaign. The
/// context-scoped overload answers <c>NotFound</c> instead, because its callers stamp a Round onto a row or measure
/// a countdown against one and have nothing to do with a Round that is not real. Both halves are pinned here, since
/// a caller picking the wrong overload is exactly the mistake the pair invites.
/// </para>
/// </remarks>
[TestFixture]
public class GetCurrentRoundNumberTests : CampaignTurnServiceTestBase
{
    /// <summary>
    /// With no Campaign TurnQueue for the game at all, the lookup should yield the <c>0</c> sentinel rather than throw or fault.
    /// </summary>
    [Test, Description("No Campaign queue yields the 0 sentinel rather than throwing; callers treat this lookup as always-answerable.")]
    public async Task ShouldReturnZeroWhenNoCampaignTurnQueueExists()
    {
        int gameId = await SeedGameWithPlayersAsync(_numberOfPlayers);

        int roundNumber = await Service.GetCurrentRoundNumberAsync(gameId);

        Assert.That(roundNumber, Is.Zero, "Should return the 0 sentinel when no Campaign TurnQueue exists.");
    }

    /// <summary>
    /// An active Campaign TurnQueue's round number should be returned even when no participant is TakingTurn
    /// (both seeded as <see cref="TurnQueueParticipantStatus.Pending"/>) — this is exactly the case where
    /// <c>GetCurrentCampaignTurnAsync</c> would return NotFound.
    /// </summary>
    [Test, Description("The round number belongs to the queue, not to whoever is mid-turn, so all-Pending still answers it.")]
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
    [Test, Description("Guards the IsCompleted filter: a finished queue still holds a plausible round number that must not be reported.")]
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

        Assert.That(roundNumber, Is.Zero, "A completed queue is not active, so the lookup should return the 0 sentinel.");
    }

    // --- The context-scoped overload -------------------------------------------------

    [Test, Description("The context-scoped overload answers the live lane's round, which is the figure its callers stamp onto rows inside their own transaction.")]
    public async Task ShouldReturnTheRoundThroughTheCallersContext()
    {
        int gameId = await SeedGameWithPlayersAsync(_numberOfPlayers);

        // SeedCampaignTurnQueueAsync seeds the queue with CurrentRoundNumber = 1.
        await SeedCampaignTurnQueueAsync(gameId, TurnQueueParticipantStatus.Pending, TurnQueueParticipantStatus.Pending);

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        ServiceResult<int> result = await Service.GetCurrentRoundNumberAsync(context, gameId);

        Assert.Multiple(() =>
        {
            Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));
            Assert.That(result.SuccessValue, Is.EqualTo(1));
        });
    }

    [Test, Description("No lane is NotFound rather than the other overload's 0, because a caller stamping a Round must not record one that never happened.")]
    public async Task ShouldReportNotFoundWhenNoCampaignTurnQueueExists()
    {
        int gameId = await SeedGameWithPlayersAsync(_numberOfPlayers);

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        ServiceResult<int> result = await Service.GetCurrentRoundNumberAsync(context, gameId);

        Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.NotFound));
    }

    [Test, Description("Guards the IsCompleted filter on this overload too: a finished lane still holds the Round the game ended on, which must not reach a caller.")]
    public async Task ShouldReportNotFoundWhenOnlyQueueIsCompleted()
    {
        int gameId = await SeedGameWithPlayersAsync(_numberOfPlayers);
        await SeedCampaignTurnQueueAsync(gameId, TurnQueueParticipantStatus.Pending, TurnQueueParticipantStatus.Pending);

        await using (AscendantEmpireContext seedContext = await DbFactory.CreateDbContextAsync())
        {
            TurnQueue queue = await seedContext.TurnQueues.SingleAsync(q => q.GameId == gameId);
            queue.IsCompleted = true;
            await seedContext.SaveChangesAsync();
        }

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        ServiceResult<int> result = await Service.GetCurrentRoundNumberAsync(context, gameId);

        Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.NotFound));
    }

    [Test, Description("Two live lanes fail rather than one being picked, because a Round read off an arbitrary lane would be stamped onto rows as though it were the game's.")]
    public async Task ShouldFailWhenTwoCampaignQueuesAreLive()
    {
        int gameId = await SeedGameWithPlayersAsync(_numberOfPlayers);
        await SeedCampaignTurnQueueAsync(gameId, TurnQueueParticipantStatus.Pending, TurnQueueParticipantStatus.Pending);

        // A second live lane is a data error the database does not prevent, so it is seeded directly rather than through a helper that would refuse it.
        await using (AscendantEmpireContext seedContext = await DbFactory.CreateDbContextAsync())
        {
            seedContext.TurnQueues.Add(new TurnQueue
            {
                GameId = gameId,
                QueueType = TurnQueueType.Campaign,
                BattleId = null,
                CurrentRoundNumber = 9,
                IsCompleted = false,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });

            await seedContext.SaveChangesAsync();
        }

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        ServiceResult<int> result = await Service.GetCurrentRoundNumberAsync(context, gameId);

        Assert.Multiple(() =>
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(string.Join("; ", result.Errors), Does.Contain("ambiguous"));
        });
    }
}
