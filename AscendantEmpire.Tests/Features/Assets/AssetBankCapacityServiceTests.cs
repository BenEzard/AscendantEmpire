using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Assets.Classes;
using AscendantEmpire.Application.Features.Assets.Services;
using AscendantEmpire.Application.Features.Settlements.Classes;
using AscendantEmpire.Application.Helpers.ScaledNumerics;
using AscendantEmpire.Domain.Enums.Settlements;

using Microsoft.Extensions.Logging.Abstractions;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Tests.Features.Assets;

/// <summary>
/// Tests for <see cref="AssetBankCapacityService"/>: that a Settlement's free capacity is its tier pool less what it holds of the class, class by class.
/// </summary>
/// <remarks>
/// In-memory, per <c>testing.md</c>: the service reads two projections and writes nothing. No Settlement has a dedicated store, because a dedicated store is
/// granted by a building and no building model exists - so the pool-only path these cases exercise is the only live one.
/// </remarks>
[TestFixture]
internal class AssetBankCapacityServiceTests : AssetBankTestBase
{
    /// <summary>A Granary Asset, for the cases that need a second storage class drawing on the same pool.</summary>
    private const string _FODDER_CODE = "RawMaterials.RawMaterials.Fodder";

    private const int _SETTLEMENT_ID = 31;
    private const int _CAMP_SETTLEMENT_ID = 32;

    private AssetBankCapacityService _service = null!;

    [SetUp]
    public void BuildService()
    {
        _service = new AssetBankCapacityService(NullLogger<AssetBankCapacityService>.Instance);
    }

    [Test, Description("A village's free capacity is its tier pool less what it already holds of that class, which is the whole live path until buildings grant dedicated stores.")]
    public async Task ShouldReportTheTierPoolLessWhatIsHeldOfTheClass()
    {
        await CreateSettlementAsync(_SETTLEMENT_ID);
        await CreateBankAsync(_playerId, _SETTLEMENT_ID, new Dictionary<string, long> { [_OAK_LOG_CODE] = 4 });

        SettlementStorageCapacity capacity = await GetCapacityAsync(_SETTLEMENT_ID);

        Assert.That(capacity.SpaceAvailable(await GetStorageClassIdAsync(_OAK_LOG_CODE)), Is.EqualTo(PoolOf(SettlementTier.Village) - Raw(4)));
    }

    [Test, Description("Two Assets of one class draw on one pool, so what either has free counts both.")]
    public async Task ShouldCountEveryAssetOfOneClassAgainstThatClass()
    {
        await CreateSettlementAsync(_SETTLEMENT_ID);
        await CreateBankAsync(_playerId, _SETTLEMENT_ID, new Dictionary<string, long>
        {
            [_OAK_LOG_CODE] = 4,
            [_CEDAR_LOG_CODE] = 6
        });

        SettlementStorageCapacity capacity = await GetCapacityAsync(_SETTLEMENT_ID);

        Assert.That(capacity.SpaceAvailable(await GetStorageClassIdAsync(_OAK_LOG_CODE)), Is.EqualTo(PoolOf(SettlementTier.Village) - Raw(10)));
    }

    [Test, Description("Classes share the tier pool, so stock in one class reduces what another has free.")]
    public async Task ShouldReduceAnotherClassesFreeCapacityWhenThePoolIsUsed()
    {
        await CreateSettlementAsync(_SETTLEMENT_ID);
        await CreateBankAsync(_playerId, _SETTLEMENT_ID, new Dictionary<string, long>
        {
            [_OAK_LOG_CODE] = 4,
            [_FODDER_CODE] = 2
        });

        SettlementStorageCapacity capacity = await GetCapacityAsync(_SETTLEMENT_ID);

        int granaryId = await GetStorageClassIdAsync(_FODDER_CODE);
        int storageYardId = await GetStorageClassIdAsync(_OAK_LOG_CODE);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(granaryId, Is.Not.EqualTo(storageYardId));
            Assert.That(capacity.SpaceAvailable(granaryId), Is.EqualTo(PoolOf(SettlementTier.Village) - Raw(6)));
        }
    }

    [Test, Description("A settlement id with no row fails with an error naming it, rather than answering for a settlement that was never found.")]
    public async Task ShouldFailWhenASettlementIdHasNoRow()
    {
        await CreateSettlementAsync(_SETTLEMENT_ID);

        const int missingSettlementId = 999;

        ServiceResult<IReadOnlyDictionary<int, SettlementStorageCapacity>> result =
            await CapacitiesAsync([_SETTLEMENT_ID, missingSettlementId]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(string.Join("; ", result.Errors), Does.Contain(missingSettlementId.ToString()));
        }
    }

    [Test, Description("A settlement holding nothing has its whole pool free, since a bank with no balance rows holds nothing rather than an unknown amount.")]
    public async Task ShouldReportTheWholePoolWhenTheSettlementHoldsNothing()
    {
        await CreateSettlementAsync(_SETTLEMENT_ID);
        await CreateBankAsync(_playerId, _SETTLEMENT_ID, new Dictionary<string, long>());

        SettlementStorageCapacity capacity = await GetCapacityAsync(_SETTLEMENT_ID);

        Assert.That(capacity.SpaceAvailable(await GetStorageClassIdAsync(_OAK_LOG_CODE)), Is.EqualTo(PoolOf(SettlementTier.Village)));
    }

    private static long PoolOf(SettlementTier tier)
    {
        SettlementStorageRules.TryGetGenericPoolCapacity(tier, out long rawCapacity);

        return rawCapacity;
    }

    private static long Raw(long wholeUnits)
    {
        return ScaledAmount.FromWhole(wholeUnits).RawValue;
    }

    private async Task<SettlementStorageCapacity> GetCapacityAsync(int settlementId)
    {
        ServiceResult<IReadOnlyDictionary<int, SettlementStorageCapacity>> result = await CapacitiesAsync([settlementId]);

        Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));

        return result.SuccessValue![settlementId];
    }

    private async Task<ServiceResult<IReadOnlyDictionary<int, SettlementStorageCapacity>>> CapacitiesAsync(IReadOnlyCollection<int> settlementIds)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        return await _service.GetSettlementCapacitiesAsync(context, settlementIds);
    }
}
