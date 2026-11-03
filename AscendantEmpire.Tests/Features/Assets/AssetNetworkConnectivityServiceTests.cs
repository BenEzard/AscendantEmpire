using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Assets.Classes;
using AscendantEmpire.Application.Features.Assets.Services;
using AscendantEmpire.Domain.Entities.Assets;
using AscendantEmpire.Domain.Enums.Settlements;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Tests.Features.Assets;

/// <summary>
/// Tests for <see cref="AssetNetworkConnectivityService"/>: that it keys a new bank, groups a camp with the settlement it feeds, isolates banks that should not
/// share a network, and reports the three ways a derived key goes stale.
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

    // A camp and the village it feeds. The camp takes the lower id, so the village keeping the camp's key is the ordering rule working rather than a coincidence.
    private const int _CAMP_SETTLEMENT_ID = 11;
    private const int _PARENT_SETTLEMENT_ID = 12;

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

    [Test, Description("A captured bank is given a fresh key rather than keeping its old one, so it cannot stay in a network with the banks its former owner still holds.")]
    public void ShouldAssignAFreshKeyToACapturedBank()
    {
        var bank = new AssetBank { PlayerId = _playerId, SettlementId = _SETTLEMENT_ID, NetworkKey = Guid.NewGuid() };

        Guid keyBeforeCapture = bank.NetworkKey;

        _service.AssignKeyToCapturedBank(bank);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(bank.NetworkKey, Is.Not.EqualTo(keyBeforeCapture));
            Assert.That(bank.NetworkKey, Is.Not.EqualTo(Guid.Empty), "An unkeyed bank would reach nothing at all, its own Settlement included.");
        }
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

    [Test, Description("A camp is re-keyed into its parent settlement's network, because sharing the parent's key is what lets it spend from the parent's stock.")]
    public async Task ShouldPutACampInItsParentSettlementsNetworkWhenRecomputing()
    {
        await CreateSettlementAsync(_CAMP_SETTLEMENT_ID, SettlementTier.Camp, parentSettlementId: _PARENT_SETTLEMENT_ID);
        await CreateSettlementAsync(_PARENT_SETTLEMENT_ID);

        Guid campKey = Guid.NewGuid();

        int campBankId = await CreateBankAsync(_playerId, _CAMP_SETTLEMENT_ID, new Dictionary<string, long>(), campKey);
        int parentBankId = await CreateBankAsync(_playerId, _PARENT_SETTLEMENT_ID, new Dictionary<string, long>());

        await RecomputeAsync(_playerId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await ReadKeyAsync(campBankId), Is.EqualTo(campKey), "The network's lowest bank id is what keeps its key.");
            Assert.That(await ReadKeyAsync(parentBankId), Is.EqualTo(campKey), "And the parent joins it rather than the other way round.");
        }
    }

    [Test, Description("A camp and its parent recomputed twice change nothing the second time, so a founding and a later conquest do not churn the network they agree about.")]
    public async Task ShouldChangeNothingWhenACampGroupIsRecomputedTwice()
    {
        await CreateSettlementAsync(_CAMP_SETTLEMENT_ID, SettlementTier.Camp, parentSettlementId: _PARENT_SETTLEMENT_ID);
        await CreateSettlementAsync(_PARENT_SETTLEMENT_ID);

        int campBankId = await CreateBankAsync(_playerId, _CAMP_SETTLEMENT_ID, new Dictionary<string, long>());
        int parentBankId = await CreateBankAsync(_playerId, _PARENT_SETTLEMENT_ID, new Dictionary<string, long>());

        await RecomputeAsync(_playerId);

        Guid campKeyAfterOneRun = await ReadKeyAsync(campBankId);

        await RecomputeAsync(_playerId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await ReadKeyAsync(campBankId), Is.EqualTo(campKeyAfterOneRun));
            Assert.That(await ReadKeyAsync(parentBankId), Is.EqualTo(campKeyAfterOneRun));
        }
    }

    [Test, Description("A camp whose parent settlement is now another Player's is put in a network of its own, which is what stops its deliveries and leaves it banking for itself.")]
    public async Task ShouldIsolateACampWhoseParentSettlementBelongsToAnotherPlayer()
    {
        await CreateSettlementAsync(_CAMP_SETTLEMENT_ID, SettlementTier.Camp, parentSettlementId: _PARENT_SETTLEMENT_ID);
        await CreateSettlementAsync(_PARENT_SETTLEMENT_ID);

        Guid sharedKey = Guid.NewGuid();

        int campBankId = await CreateBankAsync(_playerId, _CAMP_SETTLEMENT_ID, new Dictionary<string, long>(), sharedKey);
        await CreateBankAsync(_otherPlayerId, _PARENT_SETTLEMENT_ID, new Dictionary<string, long>(), sharedKey);

        await RecomputeAsync(_playerId);

        Assert.That(await ReadKeyAsync(campBankId), Is.EqualTo(sharedKey),
            "Its own key is left alone - it is the only bank of this Player, so there is nothing for it to be at odds with.");

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        ServiceResult<IReadOnlyCollection<AssetNetworkFault>> result = await _service.FindFaultsForPlayerAsync(context, _playerId, CancellationToken.None);

        Assert.That(result.SuccessValue, Is.Empty, "And a key the other Player happens to carry is not this Player's fault to fix.");
    }

    [Test, Description("A camp in a network of its own when it should be in its parent's is reported as Unjoined, which costs the Player reach rather than letting them overspend.")]
    public async Task ShouldReportACampThatHasNotJoinedItsParentSettlement()
    {
        await CreateSettlementAsync(_CAMP_SETTLEMENT_ID, SettlementTier.Camp, parentSettlementId: _PARENT_SETTLEMENT_ID);
        await CreateSettlementAsync(_PARENT_SETTLEMENT_ID);

        await CreateBankAsync(_playerId, _CAMP_SETTLEMENT_ID, new Dictionary<string, long>());
        int parentBankId = await CreateBankAsync(_playerId, _PARENT_SETTLEMENT_ID, new Dictionary<string, long>());

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        ServiceResult<IReadOnlyCollection<AssetNetworkFault>> result = await _service.FindFaultsForPlayerAsync(context, _playerId, CancellationToken.None);

        Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));

        Assert.That(result.SuccessValue!.Select(fault => (fault.AssetBankId, fault.Kind)), Is.EquivalentTo(new[]
        {
            (parentBankId, AssetNetworkFaultKind.Unjoined)
        }), "The camp holds the key the network should carry, so it is the parent that is out of place.");
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
