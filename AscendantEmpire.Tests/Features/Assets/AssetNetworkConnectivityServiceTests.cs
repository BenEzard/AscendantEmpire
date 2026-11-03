using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Assets.Classes;
using AscendantEmpire.Application.Features.Assets.Services;
using AscendantEmpire.Domain.Entities.Assets;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Tests.Features.Assets;

/// <summary>
/// Tests for <see cref="AssetNetworkConnectivityService"/>: that it keys a new bank, isolates banks that should not share a network, and reports the two ways
/// a derived key goes stale.
/// </summary>
/// <remarks>
/// In-memory, per <c>testing.md</c>: the service does nothing but read the Player's banks, compare keys and assign new ones, and no rule it enforces lives in
/// a database constraint.
/// </remarks>
[TestFixture]
internal class AssetNetworkConnectivityServiceTests : AssetBankTestBase
{
    private const int _SETTLEMENT_ID = 41;
    private const int _SECOND_SETTLEMENT_ID = 42;
    private const int _FAR_SETTLEMENT_ID = 77;

    private AssetNetworkConnectivityService _service = null!;

    [SetUp]
    public void BuildService()
    {
        _service = new AssetNetworkConnectivityService(NullLogger<AssetNetworkConnectivityService>.Instance);
    }

    [Test, Description("A newly staged bank is given a key by this service, because nothing else is allowed to write NetworkKey and an unkeyed bank is unreachable by its own settlement.")]
    public void ShouldAssignAFreshKeyToANewBank()
    {
        var bank = new AssetBank { PlayerId = _playerId, SettlementId = _SETTLEMENT_ID };

        _service.AssignKeyToNewBank(bank);

        Assert.That(bank.NetworkKey, Is.Not.EqualTo(Guid.Empty));
    }

    [Test, Description("An unkeyed bank is keyed by a recompute, so a bank saved before anything keyed it does not stay cut off from its own settlement.")]
    public async Task ShouldKeyAnUnkeyedBankWhenRecomputing()
    {
        int assetBankId = await CreateBankAsync(_playerId, _SETTLEMENT_ID, new Dictionary<string, long>(), Guid.Empty);

        await RecomputeAsync(_playerId);

        Assert.That(await ReadKeyAsync(assetBankId), Is.Not.EqualTo(Guid.Empty));
    }

    [Test, Description("Two banks sharing a key are split apart, because nothing connects a player's settlements yet and a shared key would let them draw on each other.")]
    public async Task ShouldSplitTwoBanksThatShareAKeyWhenRecomputing()
    {
        Guid sharedKey = Guid.NewGuid();

        int firstBankId = await CreateBankAsync(_playerId, _SETTLEMENT_ID, new Dictionary<string, long>(), sharedKey);
        int secondBankId = await CreateBankAsync(_playerId, _SECOND_SETTLEMENT_ID, new Dictionary<string, long>(), sharedKey);

        await RecomputeAsync(_playerId);

        Guid firstKey = await ReadKeyAsync(firstBankId);
        Guid secondKey = await ReadKeyAsync(secondBankId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(firstKey, Is.EqualTo(sharedKey), "The lowest bank id keeps the key, so the same input always gives the same output.");
            Assert.That(secondKey, Is.Not.EqualTo(sharedKey), "And the other is moved to a network of its own.");
            Assert.That(secondKey, Is.Not.EqualTo(Guid.Empty));
        }
    }

    [Test, Description("A bank already in a correct network of one keeps its key, so a recompute does not churn rows it has nothing to say about.")]
    public async Task ShouldLeaveACorrectlyKeyedBankAloneWhenRecomputing()
    {
        Guid key = Guid.NewGuid();

        int assetBankId = await CreateBankAsync(_playerId, _SETTLEMENT_ID, new Dictionary<string, long>(), key);

        await RecomputeAsync(_playerId);

        Assert.That(await ReadKeyAsync(assetBankId), Is.EqualTo(key));
    }

    [Test, Description("A recompute run twice changes nothing the second time, which is what lets a conquest recompute both players without ordering the two calls.")]
    public async Task ShouldChangeNothingWhenRecomputedTwice()
    {
        Guid sharedKey = Guid.NewGuid();

        int firstBankId = await CreateBankAsync(_playerId, _SETTLEMENT_ID, new Dictionary<string, long>(), sharedKey);
        int secondBankId = await CreateBankAsync(_playerId, _SECOND_SETTLEMENT_ID, new Dictionary<string, long>(), sharedKey);

        await RecomputeAsync(_playerId);

        Guid firstKeyAfterOneRun = await ReadKeyAsync(firstBankId);
        Guid secondKeyAfterOneRun = await ReadKeyAsync(secondBankId);

        await RecomputeAsync(_playerId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await ReadKeyAsync(firstBankId), Is.EqualTo(firstKeyAfterOneRun));
            Assert.That(await ReadKeyAsync(secondBankId), Is.EqualTo(secondKeyAfterOneRun));
        }
    }

    [Test, Description("Another Player's banks are untouched, because connectivity is assessed per Player and a key two Players happen to share is not a fault to fix here.")]
    public async Task ShouldIgnoreAnotherPlayersBanksWhenRecomputing()
    {
        Guid sharedKey = Guid.NewGuid();

        await CreateBankAsync(_playerId, _SETTLEMENT_ID, new Dictionary<string, long>(), sharedKey);
        int rivalBankId = await CreateBankAsync(_otherPlayerId, _FAR_SETTLEMENT_ID, new Dictionary<string, long>(), sharedKey);

        await RecomputeAsync(_playerId);

        Assert.That(await ReadKeyAsync(rivalBankId), Is.EqualTo(sharedKey));
    }

    [Test, Description("A Player with no banks recomputes successfully and writes nothing, since a player mid-game may hold no settlement at all.")]
    public async Task ShouldSucceedWhenThePlayerHasNoBanks()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        ServiceResultBase result = await _service.RecomputeForPlayerAsync(context, _playerId, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));
            Assert.That(context.ChangeTracker.HasChanges(), Is.False, "Nothing is staged, so there is nothing for the caller to save.");
        }
    }

    [Test, Description("The consistency check reports an unkeyed bank and a shared key, which are the only two ways an isolated network rule can be violated.")]
    public async Task ShouldReportUnkeyedAndSharedKeyFaults()
    {
        Guid sharedKey = Guid.NewGuid();

        int unkeyedBankId = await CreateBankAsync(_playerId, _SETTLEMENT_ID, new Dictionary<string, long>(), Guid.Empty);
        await CreateBankAsync(_playerId, _SECOND_SETTLEMENT_ID, new Dictionary<string, long>(), sharedKey);
        int sharingBankId = await CreateBankAsync(_playerId, _FAR_SETTLEMENT_ID, new Dictionary<string, long>(), sharedKey);

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        ServiceResult<IReadOnlyCollection<AssetNetworkFault>> result =
            await _service.FindFaultsForPlayerAsync(context, _playerId, CancellationToken.None);

        Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));

        IReadOnlyCollection<AssetNetworkFault> faults = result.SuccessValue!;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(faults.Select(fault => (fault.AssetBankId, fault.Kind)), Is.EquivalentTo(new[]
            {
                (unkeyedBankId, AssetNetworkFaultKind.Unkeyed),
                (sharingBankId, AssetNetworkFaultKind.SharedKey)
            }), "The lowest bank id of a shared key is the one that keeps it, so only the later one is at fault.");
            Assert.That(context.ChangeTracker.HasChanges(), Is.False, "The check reports and does not write.");
        }
    }

    [Test, Description("The check reports nothing for banks that are each in their own network, so a clean empire does not read as broken.")]
    public async Task ShouldReportNoFaultsWhenEveryBankIsIsolated()
    {
        await CreateBankAsync(_playerId, _SETTLEMENT_ID, new Dictionary<string, long>());
        await CreateBankAsync(_playerId, _SECOND_SETTLEMENT_ID, new Dictionary<string, long>());

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        ServiceResult<IReadOnlyCollection<AssetNetworkFault>> result =
            await _service.FindFaultsForPlayerAsync(context, _playerId, CancellationToken.None);

        Assert.That(result.SuccessValue, Is.Empty);
    }

    /// <summary>Recomputes on its own context and saves, which is what the caller of a re-key does.</summary>
    private async Task RecomputeAsync(int playerId)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        ServiceResultBase result = await _service.RecomputeForPlayerAsync(context, playerId, CancellationToken.None);

        Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));

        await context.SaveChangesAsync();
    }

    private async Task<Guid> ReadKeyAsync(int assetBankId)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        return await context.AssetBanks
            .Where(bank => bank.Id == assetBankId)
            .Select(bank => bank.NetworkKey)
            .SingleAsync();
    }
}
