using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Domain.Entities.Turns;
using AscendantEmpire.Domain.Enums.Turns;

using Microsoft.EntityFrameworkCore;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Tests.Features.Turns.Services;

/// <summary>
/// Pins how a Player Turn opens and closes, including the one-open-turn-per-lane invariant the whole intelligence feature rests on.
/// </summary>
/// <remarks>
/// <para>
/// Containerised because two of the rules under test <b>are</b> the database: <c>UX_PlayerTurn_OpenTurnPerQueue</c> is a filtered unique index, which the
/// in-memory provider ignores entirely and would pass while proving nothing, and the close uses <c>ExecuteUpdateAsync</c>. Per <c>docs/testing.md</c>, both
/// put this at the SQL Server tier.
/// </para>
/// <para>
/// The self-healing cases matter more than they look. A lane that could not hand the turn on is a game nobody can finish, so "open closes a stale turn" is
/// not defensive padding - it is the other half of the unique index, and without it a single failed start-of-turn would jam the lane permanently.
/// </para>
/// </remarks>
[TestFixture]
internal class PlayerTurnLifecycleTests : PlayerTurnTestBase
{
    [Test, Description("An opened Turn records who is playing, in which Round, from when - and starts its counter at zero, meaning nothing recorded yet.")]
    public async Task ShouldRecordTheTurnWhenOpenedAsync()
    {
        DateTimeOffset before = DateTimeOffset.UtcNow;

        int playerTurnId = await OpenTurnAsync(CampaignQueueId, FirstPlayerId);

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();
        PlayerTurn turn = await context.PlayerTurns.SingleAsync(t => t.Id == playerTurnId);

        Assert.Multiple(() =>
        {
            Assert.That(turn.TurnQueueId, Is.EqualTo(CampaignQueueId));
            Assert.That(turn.PlayerId, Is.EqualTo(FirstPlayerId));
            Assert.That(turn.RoundNumber, Is.EqualTo(_ROUND_NUMBER));
            Assert.That(turn.EndedAt, Is.Null, "A freshly opened Turn is open.");
            Assert.That(turn.LastAllocatedSequence, Is.Zero, "Nothing has been recorded during the Turn yet.");
            Assert.That(turn.StartedAt, Is.GreaterThanOrEqualTo(before));
        });
    }

    [Test, Description("A retried start of Turn reuses the row the Player already has rather than colliding with the one-open-Turn index.")]
    public async Task ShouldReuseTheOpenTurnWhenTheSamePlayerOpensAgainAsync()
    {
        int firstOpenId = await OpenTurnAsync(CampaignQueueId, FirstPlayerId);
        int secondOpenId = await OpenTurnAsync(CampaignQueueId, FirstPlayerId);

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        Assert.Multiple(async () =>
        {
            Assert.That(secondOpenId, Is.EqualTo(firstOpenId), "The second open should hand back the same Turn, not start a new one.");
            Assert.That(await context.PlayerTurns.CountAsync(t => t.TurnQueueId == CampaignQueueId), Is.EqualTo(1));
        });
    }

    [Test, Description("A stale Turn left open by a failed hand-over is closed rather than refused, because a jammed lane is a Game nobody can finish.")]
    public async Task ShouldCloseAStaleOpenTurnWhenAnotherPlayerStartsAsync()
    {
        int staleTurnId = await OpenTurnAsync(CampaignQueueId, FirstPlayerId);
        int newTurnId = await OpenTurnAsync(CampaignQueueId, SecondPlayerId);

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        PlayerTurn staleTurn = await context.PlayerTurns.SingleAsync(t => t.Id == staleTurnId);
        PlayerTurn newTurn = await context.PlayerTurns.SingleAsync(t => t.Id == newTurnId);

        Assert.Multiple(() =>
        {
            Assert.That(newTurnId, Is.Not.EqualTo(staleTurnId));
            Assert.That(staleTurn.EndedAt, Is.Not.Null, "The stale Turn should have been closed, not left open alongside the new one.");
            Assert.That(newTurn.EndedAt, Is.Null);
        });
    }

    [Test, Description("A Turn opened in a later Round supersedes one still open from an earlier Round, so a Round boundary cannot leave two Turns running.")]
    public async Task ShouldCloseAnEarlierRoundsOpenTurnWhenTheSamePlayerStartsANewRoundAsync()
    {
        int firstRoundTurnId = await OpenTurnAsync(CampaignQueueId, FirstPlayerId, roundNumber: 1);
        int secondRoundTurnId = await OpenTurnAsync(CampaignQueueId, FirstPlayerId, roundNumber: 2);

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        Assert.Multiple(async () =>
        {
            Assert.That(secondRoundTurnId, Is.Not.EqualTo(firstRoundTurnId), "A new Round is a new Turn even for the same Player.");
            Assert.That((await context.PlayerTurns.SingleAsync(t => t.Id == firstRoundTurnId)).EndedAt, Is.Not.Null);
            Assert.That(await context.PlayerTurns.CountAsync(t => t.TurnQueueId == CampaignQueueId && t.EndedAt == null), Is.EqualTo(1));
        });
    }

    [Test, Description("Closing stamps the end time and leaves the counter alone, because what a Turn handed out is history rather than state to reset.")]
    public async Task ShouldStampTheEndTimeAndKeepTheCounterWhenClosedAsync()
    {
        int playerTurnId = await OpenTurnAsync(CampaignQueueId, FirstPlayerId);

        await using AscendantEmpireContext allocateContext = await DbFactory.CreateDbContextAsync();
        await Service.AllocateSequenceAsync(allocateContext, playerTurnId, count: 3);

        await using AscendantEmpireContext closeContext = await DbFactory.CreateDbContextAsync();
        ServiceResultBase closeResult = await Service.ClosePlayerTurnAsync(closeContext, CampaignQueueId, FirstPlayerId);

        await using AscendantEmpireContext readContext = await DbFactory.CreateDbContextAsync();
        PlayerTurn turn = await readContext.PlayerTurns.SingleAsync(t => t.Id == playerTurnId);

        Assert.Multiple(() =>
        {
            Assert.That(closeResult.HasSucceeded, Is.True, () => string.Join("; ", closeResult.Errors));
            Assert.That(turn.EndedAt, Is.Not.Null);
            Assert.That(turn.LastAllocatedSequence, Is.EqualTo(3), "Closing must not rewind what the Turn already handed out.");
        });
    }

    [Test, Description("A lane with nothing open refuses to close, because a Turn ending with no row means something handed it on without recording it.")]
    public async Task ShouldFailWhenClosingALaneWithNoOpenTurnAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        ServiceResultBase result = await Service.ClosePlayerTurnAsync(context, CampaignQueueId, FirstPlayerId);

        Assert.That(result.HasSucceeded, Is.False, "Ending a Turn that was never opened should be refused, not waved through.");
    }

    [Test, Description("The lane's open Turn is closed even when it belongs to a different Player, because there is only one and it is over either way.")]
    public async Task ShouldCloseTheOpenTurnWhenItBelongsToAnotherPlayerAsync()
    {
        int playerTurnId = await OpenTurnAsync(CampaignQueueId, FirstPlayerId);

        await using AscendantEmpireContext closeContext = await DbFactory.CreateDbContextAsync();
        ServiceResultBase result = await Service.ClosePlayerTurnAsync(closeContext, CampaignQueueId, SecondPlayerId);

        await using AscendantEmpireContext readContext = await DbFactory.CreateDbContextAsync();

        Assert.Multiple(async () =>
        {
            Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));
            Assert.That((await readContext.PlayerTurns.SingleAsync(t => t.Id == playerTurnId)).EndedAt, Is.Not.Null);
        });
    }

    [Test, Description("The database itself refuses a second open Turn in one lane, so the invariant survives code that bypasses the service.")]
    public async Task ShouldRejectASecondOpenTurnInOneLaneAsync()
    {
        await OpenTurnAsync(CampaignQueueId, FirstPlayerId);

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        // Added directly, not through the service, because the point is that the index holds even when nothing in code is looking.
        context.PlayerTurns.Add(new PlayerTurn
        {
            TurnQueueId = CampaignQueueId,
            PlayerId = SecondPlayerId,
            RoundNumber = _ROUND_NUMBER,
            StartedAt = DateTimeOffset.UtcNow,
        });

        Assert.That(async () => await context.SaveChangesAsync(), Throws.InstanceOf<DbUpdateException>());
    }

    [Test, Description("The index is filtered on EndedAt, so a lane accumulates closed Turns without ever colliding with itself.")]
    public async Task ShouldAllowManyClosedTurnsInOneLaneAsync()
    {
        for (int round = 1; round <= 3; round++)
        {
            await OpenTurnAsync(CampaignQueueId, FirstPlayerId, roundNumber: round);
            await OpenTurnAsync(CampaignQueueId, SecondPlayerId, roundNumber: round);
        }

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        Assert.Multiple(async () =>
        {
            Assert.That(await context.PlayerTurns.CountAsync(t => t.TurnQueueId == CampaignQueueId), Is.EqualTo(6));
            Assert.That(await context.PlayerTurns.CountAsync(t => t.TurnQueueId == CampaignQueueId && t.EndedAt == null), Is.EqualTo(1));
        });
    }

    [Test, Description("The one-open-Turn rule is per lane, so a Battle lane can run a Turn while the Campaign lane has one open.")]
    public async Task ShouldAllowAnOpenTurnInEachLaneAsync()
    {
        await using AscendantEmpireContext seedContext = await DbFactory.CreateDbContextAsync();
        int battleQueueId = await AddQueueAsync(seedContext, TurnQueueType.Battle);

        await OpenTurnAsync(CampaignQueueId, FirstPlayerId);
        await OpenTurnAsync(battleQueueId, FirstPlayerId);

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        Assert.That(await context.PlayerTurns.CountAsync(t => t.EndedAt == null), Is.EqualTo(2));
    }
}
