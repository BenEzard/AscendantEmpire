using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Turns.Services;
using AscendantEmpire.Domain.Entities.Turns;
using AscendantEmpire.Domain.Enums.Turns;

using Microsoft.EntityFrameworkCore;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Tests.Features.Turns.Services;

/// <summary>
/// Pins the per-Turn sequence counter: that it is monotonic, that it is atomic under concurrency, and that a closed Turn hands out nothing.
/// </summary>
/// <remarks>
/// <para>
/// Containerised, and unavoidably so. The allocator is a raw <c>UPDATE ... OUTPUT</c>, which the in-memory provider cannot execute at all, and the claim
/// being tested - that concurrent callers each get a distinct number without a retry loop - can only be demonstrated against a real engine taking real row
/// locks. Per <c>docs/testing.md</c> that is the SQL Server tier by both routes.
/// </para>
/// <para>
/// This counter is what lets three separate activity feeds interleave into one order across a Turn. If it ever handed the same number to two writers, the
/// feeds would sort into an order that never happened and nothing downstream could tell - which is why the concurrency case below is the important one.
/// </para>
/// </remarks>
[TestFixture]
internal class PlayerTurnSequenceTests : PlayerTurnTestBase
{
    [Test, Description("The counter is a high-water mark starting at zero, so the very first thing recorded in a Turn is sequence one.")]
    public async Task ShouldReturnOneForTheFirstAllocationAsync()
    {
        int playerTurnId = await OpenTurnAsync(CampaignQueueId, FirstPlayerId);

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();
        ServiceResult<TurnSequenceBlock> result = await Service.AllocateSequenceAsync(context, playerTurnId);

        Assert.Multiple(() =>
        {
            Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));
            Assert.That(result.SuccessValue.First, Is.EqualTo(1));
            Assert.That(result.SuccessValue.Last, Is.EqualTo(1));
        });
    }

    [Test, Description("Successive allocations climb by one and leave the counter at the highest value handed out, which is what orders the feeds.")]
    public async Task ShouldIncreaseByOneOnEachAllocationAsync()
    {
        int playerTurnId = await OpenTurnAsync(CampaignQueueId, FirstPlayerId);

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        List<int> allocated = [];

        for (int allocation = 0; allocation < 3; allocation++)
        {
            ServiceResult<TurnSequenceBlock> result = await Service.AllocateSequenceAsync(context, playerTurnId);

            Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));

            allocated.Add(result.SuccessValue.First);
        }

        await using AscendantEmpireContext readContext = await DbFactory.CreateDbContextAsync();
        PlayerTurn turn = await readContext.PlayerTurns.SingleAsync(t => t.Id == playerTurnId);

        Assert.Multiple(() =>
        {
            Assert.That(allocated, Is.EqualTo(new[] { 1, 2, 3 }));
            Assert.That(turn.LastAllocatedSequence, Is.EqualTo(3));
        });
    }

    [Test, Description("A block reserves a contiguous run in one statement, which is what a Force moving several tiles needs so its steps stay together.")]
    public async Task ShouldReserveAContiguousRunWhenAllocatingABlockAsync()
    {
        int playerTurnId = await OpenTurnAsync(CampaignQueueId, FirstPlayerId);

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();
        ServiceResult<TurnSequenceBlock> result = await Service.AllocateSequenceAsync(context, playerTurnId, count: 5);

        await using AscendantEmpireContext readContext = await DbFactory.CreateDbContextAsync();
        PlayerTurn turn = await readContext.PlayerTurns.SingleAsync(t => t.Id == playerTurnId);

        Assert.Multiple(() =>
        {
            Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));
            Assert.That(result.SuccessValue.First, Is.EqualTo(1));
            Assert.That(result.SuccessValue.Last, Is.EqualTo(5));
            Assert.That(turn.LastAllocatedSequence, Is.EqualTo(5));
        });
    }

    [Test, Description("A block picks up where the last one stopped, so two writers in one Turn never overlap even when each takes several numbers.")]
    public async Task ShouldContinueFromTheHighWaterMarkOnASecondBlockAsync()
    {
        int playerTurnId = await OpenTurnAsync(CampaignQueueId, FirstPlayerId);

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        await Service.AllocateSequenceAsync(context, playerTurnId, count: 3);
        ServiceResult<TurnSequenceBlock> second = await Service.AllocateSequenceAsync(context, playerTurnId, count: 2);

        Assert.Multiple(() =>
        {
            Assert.That(second.HasSucceeded, Is.True, () => string.Join("; ", second.Errors));
            Assert.That(second.SuccessValue.First, Is.EqualTo(4));
            Assert.That(second.SuccessValue.Last, Is.EqualTo(5));
        });
    }

    [Test, Description("Twenty callers racing on one Turn get twenty distinct numbers, which is the atomicity the raw UPDATE ... OUTPUT exists to provide.")]
    public async Task ShouldHandOutDistinctNumbersToConcurrentCallersAsync()
    {
        const int _CONCURRENT_CALLERS = 20;

        int playerTurnId = await OpenTurnAsync(CampaignQueueId, FirstPlayerId);

        // Each caller gets its own context, and therefore its own connection - a shared context would serialise them in the client and prove nothing about
        // the database.
        IEnumerable<Task<ServiceResult<TurnSequenceBlock>>> allocations = Enumerable
            .Range(0, _CONCURRENT_CALLERS)
            .Select(_ => Service.AllocateSequenceAsync(playerTurnId));

        ServiceResult<TurnSequenceBlock>[] results = await Task.WhenAll(allocations);

        int[] allocated = [.. results.Where(r => r.HasSucceeded).Select(r => r.SuccessValue.First).OrderBy(value => value)];

        await using AscendantEmpireContext readContext = await DbFactory.CreateDbContextAsync();
        PlayerTurn turn = await readContext.PlayerTurns.SingleAsync(t => t.Id == playerTurnId);

        Assert.Multiple(() =>
        {
            Assert.That(results.Select(r => r.HasSucceeded), Is.All.True, "Every caller should have been served rather than losing a concurrency race.");
            Assert.That(allocated, Is.EqualTo(Enumerable.Range(1, _CONCURRENT_CALLERS)), "The numbers handed out should be exactly 1..n, with no duplicates and no gaps.");
            Assert.That(turn.LastAllocatedSequence, Is.EqualTo(_CONCURRENT_CALLERS));
        });
    }

    [Test, Description("A closed Turn refuses to allocate, because appending a feed row after the fold has run is intelligence that silently never arrives.")]
    public async Task ShouldRefuseToAllocateFromAClosedTurnAsync()
    {
        int playerTurnId = await OpenTurnAsync(CampaignQueueId, FirstPlayerId);

        await using AscendantEmpireContext closeContext = await DbFactory.CreateDbContextAsync();
        await Service.ClosePlayerTurnAsync(closeContext, CampaignQueueId, FirstPlayerId);

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();
        ServiceResult<TurnSequenceBlock> result = await Service.AllocateSequenceAsync(context, playerTurnId);

        await using AscendantEmpireContext readContext = await DbFactory.CreateDbContextAsync();
        PlayerTurn turn = await readContext.PlayerTurns.SingleAsync(t => t.Id == playerTurnId);

        Assert.Multiple(() =>
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(turn.LastAllocatedSequence, Is.Zero, "A refused allocation must not have moved the counter.");
        });
    }

    [Test, Description("An unknown Turn is a failure rather than a zero, so a caller cannot mistake 'no such Turn' for 'allocated nothing'.")]
    public async Task ShouldFailWhenTheTurnDoesNotExistAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        ServiceResult<TurnSequenceBlock> result = await Service.AllocateSequenceAsync(context, playerTurnId: 999_999);

        Assert.That(result.HasSucceeded, Is.False);
    }

    [TestCase(0, Description = "Zero numbers is not a meaningful reservation, so it is refused rather than quietly returning an empty run.")]
    [TestCase(-1, Description = "A negative count would rewind the counter and re-issue numbers already handed out, so it is refused outright.")]
    public async Task ShouldRefuseANonPositiveCountAsync(int count)
    {
        int playerTurnId = await OpenTurnAsync(CampaignQueueId, FirstPlayerId);

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();
        ServiceResult<TurnSequenceBlock> result = await Service.AllocateSequenceAsync(context, playerTurnId, count);

        await using AscendantEmpireContext readContext = await DbFactory.CreateDbContextAsync();
        PlayerTurn turn = await readContext.PlayerTurns.SingleAsync(t => t.Id == playerTurnId);

        Assert.Multiple(() =>
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(turn.LastAllocatedSequence, Is.Zero);
        });
    }

    [Test, Description("The counter belongs to the Turn, not the Game, so two lanes running at once number their own records independently.")]
    public async Task ShouldCountIndependentlyInEachLaneAsync()
    {
        await using AscendantEmpireContext seedContext = await DbFactory.CreateDbContextAsync();
        int battleQueueId = await AddQueueAsync(seedContext, TurnQueueType.Battle);

        int campaignTurnId = await OpenTurnAsync(CampaignQueueId, FirstPlayerId);
        int battleTurnId = await OpenTurnAsync(battleQueueId, FirstPlayerId);

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        await Service.AllocateSequenceAsync(context, campaignTurnId, count: 4);
        ServiceResult<TurnSequenceBlock> battleResult = await Service.AllocateSequenceAsync(context, battleTurnId);

        Assert.Multiple(() =>
        {
            Assert.That(battleResult.HasSucceeded, Is.True, () => string.Join("; ", battleResult.Errors));
            Assert.That(battleResult.SuccessValue.First, Is.EqualTo(1), "The Battle lane's Turn should start its own count, not continue the Campaign lane's.");
        });
    }
}
