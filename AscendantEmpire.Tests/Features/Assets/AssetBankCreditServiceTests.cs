using AscendantEmpire.Application.Common;
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
/// Tests for <see cref="AssetBankCreditService"/>: that a credit fills the emptiest banks of a network first, stops at each Settlement's capacity, and reports
/// whatever it could place nowhere.
/// </summary>
/// <remarks>
/// In-memory, per <c>testing.md</c>: the service reads three projections, adds and updates balance rows, and leaves the save to its caller. The composite key it
/// relies on is enforced in code - it looks a bank's row up, and consults the change tracker, before inserting one - rather than left to the database to refuse.
/// </remarks>
[TestFixture]
internal class AssetBankCreditServiceTests : AssetBankTestBase
{
    /// <summary>A Granary Asset, for the case that needs a second storage class drawing on the same pool.</summary>
    private const string _FODDER_CODE = "RawMaterials.RawMaterials.Fodder";

    private const int _FIRST_SETTLEMENT_ID = 41;
    private const int _SECOND_SETTLEMENT_ID = 42;
    private const int _THIRD_SETTLEMENT_ID = 43;
    private const int _CAMP_SETTLEMENT_ID = 44;

    /// <summary>A Settlement no fixture gives a bank, for the refusal that says a credit has to arrive somewhere.</summary>
    private const int _BANKLESS_SETTLEMENT_ID = 45;

    /// <summary>A village's whole generic pool, in the whole units the fixtures arrange stock in.</summary>
    /// <remarks>Read from the rules rather than restated, so repricing a tier moves these cases with it instead of failing them.</remarks>
    private static readonly long _VILLAGE_POOL_WHOLE_UNITS = PoolInWholeUnits(SettlementTier.Village);

    private static readonly long _CAMP_POOL_WHOLE_UNITS = PoolInWholeUnits(SettlementTier.Camp);

    private AssetBankCreditService _service = null!;

    [SetUp]
    public void BuildService()
    {
        _service = new AssetBankCreditService(
            new AssetBankCapacityService(NullLogger<AssetBankCapacityService>.Instance),
            NullLogger<AssetBankCreditService>.Instance);
    }

    [Test, Description("A credit fills the emptiest banks up to a common water-line and leaves the fullest alone, which is the mirror of the debit's levelling and the whole of why a depleted settlement re-levels.")]
    public async Task ShouldFillTheEmptiestBanksUpToACommonWaterLine()
    {
        Guid networkKey = Guid.NewGuid();

        await CreateSettlementAsync(_FIRST_SETTLEMENT_ID);
        await CreateSettlementAsync(_SECOND_SETTLEMENT_ID);
        await CreateSettlementAsync(_THIRD_SETTLEMENT_ID);

        int emptiestBankId = await CreateBankAsync(_playerId, _FIRST_SETTLEMENT_ID, new Dictionary<string, long> { [_OAK_LOG_CODE] = 4 }, networkKey);
        int middleBankId = await CreateBankAsync(_playerId, _SECOND_SETTLEMENT_ID, new Dictionary<string, long> { [_OAK_LOG_CODE] = 30 }, networkKey);
        int fullestBankId = await CreateBankAsync(_playerId, _THIRD_SETTLEMENT_ID, new Dictionary<string, long> { [_OAK_LOG_CODE] = 50 }, networkKey);

        IReadOnlyList<AssetCreditPlacement> placements = await CreditAsync(await BuildCreditAsync(_FIRST_SETTLEMENT_ID, _OAK_LOG_CODE, 40));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await ReadBalancesAsync(emptiestBankId), Is.EquivalentTo(new Dictionary<string, long> { [_OAK_LOG_CODE] = 37 }));
            Assert.That(await ReadBalancesAsync(middleBankId), Is.EquivalentTo(new Dictionary<string, long> { [_OAK_LOG_CODE] = 37 }));
            Assert.That(await ReadBalancesAsync(fullestBankId), Is.EquivalentTo(new Dictionary<string, long> { [_OAK_LOG_CODE] = 50 }));
            Assert.That(placements.Single().RawUnplaced, Is.Zero);
        }
    }

    [Test, Description("A bank with no row for the Asset is the emptiest there is, and is given one, because a missing row and a zero row mean the same thing to every reader.")]
    public async Task ShouldCreateABalanceRowInABankHoldingNoneOfTheAsset()
    {
        Guid networkKey = Guid.NewGuid();

        await CreateSettlementAsync(_FIRST_SETTLEMENT_ID);
        await CreateSettlementAsync(_SECOND_SETTLEMENT_ID);

        await CreateBankAsync(_playerId, _FIRST_SETTLEMENT_ID, new Dictionary<string, long> { [_OAK_LOG_CODE] = 20 }, networkKey);
        int emptyBankId = await CreateBankAsync(_playerId, _SECOND_SETTLEMENT_ID, new Dictionary<string, long>(), networkKey);

        await CreditAsync(await BuildCreditAsync(_FIRST_SETTLEMENT_ID, _OAK_LOG_CODE, 10));

        Assert.That(await ReadBalancesAsync(emptyBankId), Is.EquivalentTo(new Dictionary<string, long> { [_OAK_LOG_CODE] = 10 }));
    }

    [Test, Description("Each bank stops at its own settlement's capacity, so a camp in a village's network takes its small pool's worth and the village takes the rest.")]
    public async Task ShouldBoundEachBankByItsOwnSettlementsCapacity()
    {
        Guid networkKey = Guid.NewGuid();

        await CreateSettlementAsync(_CAMP_SETTLEMENT_ID, SettlementTier.Camp);
        await CreateSettlementAsync(_FIRST_SETTLEMENT_ID);

        int campBankId = await CreateBankAsync(_playerId, _CAMP_SETTLEMENT_ID, new Dictionary<string, long>(), networkKey);
        int villageBankId = await CreateBankAsync(_playerId, _FIRST_SETTLEMENT_ID, new Dictionary<string, long>(), networkKey);

        IReadOnlyList<AssetCreditPlacement> placements = await CreditAsync(
            await BuildCreditAsync(_FIRST_SETTLEMENT_ID, _OAK_LOG_CODE, _CAMP_POOL_WHOLE_UNITS * 5));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await ReadBalancesAsync(campBankId), Is.EquivalentTo(new Dictionary<string, long> { [_OAK_LOG_CODE] = _CAMP_POOL_WHOLE_UNITS }));
            Assert.That(await ReadBalancesAsync(villageBankId), Is.EquivalentTo(new Dictionary<string, long> { [_OAK_LOG_CODE] = _CAMP_POOL_WHOLE_UNITS * 4 }));
            Assert.That(placements.Single().RawUnplaced, Is.Zero);
        }
    }

    [Test, Description("A credit the network has only partial room for places what fits and reports the rest, rather than being refused whole and throwing a Turn's yield away over a unit.")]
    public async Task ShouldPlaceWhatFitsAndReportTheRestWhenTheNetworkIsNearlyFull()
    {
        await CreateSettlementAsync(_FIRST_SETTLEMENT_ID);

        int bankId = await CreateBankAsync(
            _playerId,
            _FIRST_SETTLEMENT_ID,
            new Dictionary<string, long> { [_OAK_LOG_CODE] = _VILLAGE_POOL_WHOLE_UNITS - 3 });

        IReadOnlyList<AssetCreditPlacement> placements = await CreditAsync(await BuildCreditAsync(_FIRST_SETTLEMENT_ID, _OAK_LOG_CODE, 10));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await ReadBalancesAsync(bankId), Is.EquivalentTo(new Dictionary<string, long> { [_OAK_LOG_CODE] = _VILLAGE_POOL_WHOLE_UNITS }));
            Assert.That(placements.Single().RawPlaced, Is.EqualTo(Raw(3)));
            Assert.That(placements.Single().RawUnplaced, Is.EqualTo(Raw(7)));
        }
    }

    [Test, Description("A credit no bank in the network has room for writes nothing at all and comes back as a success carrying the whole amount unplaced, because a full store is a consequence rather than a fault.")]
    public async Task ShouldWriteNothingAndReportTheWholeAmountWhenEveryBankIsFull()
    {
        Guid networkKey = Guid.NewGuid();

        await CreateSettlementAsync(_FIRST_SETTLEMENT_ID);
        await CreateSettlementAsync(_SECOND_SETTLEMENT_ID);

        int firstBankId = await CreateBankAsync(
            _playerId,
            _FIRST_SETTLEMENT_ID,
            new Dictionary<string, long> { [_OAK_LOG_CODE] = _VILLAGE_POOL_WHOLE_UNITS },
            networkKey);

        int secondBankId = await CreateBankAsync(
            _playerId,
            _SECOND_SETTLEMENT_ID,
            new Dictionary<string, long> { [_CEDAR_LOG_CODE] = _VILLAGE_POOL_WHOLE_UNITS },
            networkKey);

        IReadOnlyList<AssetCreditPlacement> placements = await CreditAsync(await BuildCreditAsync(_FIRST_SETTLEMENT_ID, _OAK_LOG_CODE, 10));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await ReadBalancesAsync(firstBankId), Is.EquivalentTo(new Dictionary<string, long> { [_OAK_LOG_CODE] = _VILLAGE_POOL_WHOLE_UNITS }));
            Assert.That(await ReadBalancesAsync(secondBankId), Is.EquivalentTo(new Dictionary<string, long> { [_CEDAR_LOG_CODE] = _VILLAGE_POOL_WHOLE_UNITS }));
            Assert.That(placements.Single().RawPlaced, Is.Zero);
            Assert.That(placements.Single().RawUnplaced, Is.EqualTo(Raw(10)));
        }
    }

    [Test, Description("The leftover raw units a water-line cannot divide go to the lower bank id between two equally empty banks, so the same credit against the same banks always writes the same rows.")]
    public async Task ShouldGiveTheLeftoverUnitToTheLowerBankIdWhenTwoBanksAreEquallyEmpty()
    {
        Guid networkKey = Guid.NewGuid();

        await CreateSettlementAsync(_FIRST_SETTLEMENT_ID);
        await CreateSettlementAsync(_SECOND_SETTLEMENT_ID);

        int lowerBankId = await CreateBankAsync(_playerId, _FIRST_SETTLEMENT_ID, new Dictionary<string, long>(), networkKey);
        int higherBankId = await CreateBankAsync(_playerId, _SECOND_SETTLEMENT_ID, new Dictionary<string, long>(), networkKey);

        await CreditAsync(AssetCredit.AtSettlement(_playerId, _FIRST_SETTLEMENT_ID, await GetAssetIdAsync(_OAK_LOG_CODE), ScaledAmount.FromRaw(3)));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await ReadRawBalancesAsync(lowerBankId), Is.EquivalentTo(new Dictionary<string, long> { [_OAK_LOG_CODE] = 2 }));
            Assert.That(await ReadRawBalancesAsync(higherBankId), Is.EquivalentTo(new Dictionary<string, long> { [_OAK_LOG_CODE] = 1 }));
        }
    }

    [Test, Description("A second credit of the same storage class sees what the first placed, which is what stops two Assets of one class each being told the whole pool is free.")]
    public async Task ShouldLetASecondCreditSeeWhatTheFirstPlaced()
    {
        await CreateSettlementAsync(_FIRST_SETTLEMENT_ID);

        int bankId = await CreateBankAsync(_playerId, _FIRST_SETTLEMENT_ID, new Dictionary<string, long>());

        long half = _VILLAGE_POOL_WHOLE_UNITS / 2;

        IReadOnlyList<AssetCreditPlacement> placements = await CreditAsync(
            await BuildCreditAsync(_FIRST_SETTLEMENT_ID, _OAK_LOG_CODE, half),
            await BuildCreditAsync(_FIRST_SETTLEMENT_ID, _CEDAR_LOG_CODE, half + 10));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await ReadBalancesAsync(bankId), Is.EquivalentTo(new Dictionary<string, long>
            {
                [_OAK_LOG_CODE] = half,
                [_CEDAR_LOG_CODE] = _VILLAGE_POOL_WHOLE_UNITS - half
            }));
            Assert.That(placements[0].RawUnplaced, Is.Zero);
            Assert.That(placements[1].RawUnplaced, Is.EqualTo(Raw(10)));
        }
    }

    [Test, Description("A classes' own pool is shared, so a credit of one Asset is bounded by what another class of the same settlement already holds.")]
    public async Task ShouldCountAnotherClassesStockAgainstTheSamePool()
    {
        await CreateSettlementAsync(_FIRST_SETTLEMENT_ID);

        int bankId = await CreateBankAsync(
            _playerId,
            _FIRST_SETTLEMENT_ID,
            new Dictionary<string, long> { [_FODDER_CODE] = _VILLAGE_POOL_WHOLE_UNITS - 2 });

        IReadOnlyList<AssetCreditPlacement> placements = await CreditAsync(await BuildCreditAsync(_FIRST_SETTLEMENT_ID, _OAK_LOG_CODE, 10));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await ReadBalancesAsync(bankId), Is.EquivalentTo(new Dictionary<string, long>
            {
                [_FODDER_CODE] = _VILLAGE_POOL_WHOLE_UNITS - 2,
                [_OAK_LOG_CODE] = 2
            }));
            Assert.That(placements.Single().RawUnplaced, Is.EqualTo(Raw(8)));
        }
    }

    [Test, Description("An excluded bank is filled with nothing however empty it is, which is what keeps a camp delivery out of another camp's small store.")]
    public async Task ShouldFillNothingIntoAnExcludedBank()
    {
        Guid networkKey = Guid.NewGuid();

        await CreateSettlementAsync(_FIRST_SETTLEMENT_ID);
        await CreateSettlementAsync(_SECOND_SETTLEMENT_ID);

        int arrivalBankId = await CreateBankAsync(_playerId, _FIRST_SETTLEMENT_ID, new Dictionary<string, long> { [_OAK_LOG_CODE] = 20 }, networkKey);
        int excludedBankId = await CreateBankAsync(_playerId, _SECOND_SETTLEMENT_ID, new Dictionary<string, long>(), networkKey);

        await CreditAsync(AssetCredit.AtSettlement(
            _playerId,
            _FIRST_SETTLEMENT_ID,
            await GetAssetIdAsync(_OAK_LOG_CODE),
            ScaledAmount.FromWhole(10),
            excludedAssetBankIds: [excludedBankId]));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await ReadBalancesAsync(arrivalBankId), Is.EquivalentTo(new Dictionary<string, long> { [_OAK_LOG_CODE] = 30 }));
            Assert.That(await ReadBalancesAsync(excludedBankId), Is.Empty);
        }
    }

    [Test, Description("A credit whose whole network is excluded is placed nowhere rather than failing, because an exhausted set of destinations is a full network by another route.")]
    public async Task ShouldReportTheWholeAmountUnplacedWhenEveryBankIsExcluded()
    {
        await CreateSettlementAsync(_FIRST_SETTLEMENT_ID);

        int bankId = await CreateBankAsync(_playerId, _FIRST_SETTLEMENT_ID, new Dictionary<string, long>());

        IReadOnlyList<AssetCreditPlacement> placements = await CreditAsync(AssetCredit.AtSettlement(
            _playerId,
            _FIRST_SETTLEMENT_ID,
            await GetAssetIdAsync(_OAK_LOG_CODE),
            ScaledAmount.FromWhole(10),
            excludedAssetBankIds: [bankId]));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await ReadBalancesAsync(bankId), Is.Empty);
            Assert.That(placements.Single().RawUnplaced, Is.EqualTo(Raw(10)));
        }
    }

    [Test, Description("A bank in another network is not filled, because a network is what a settlement reaches and a credit cannot travel further than an expense can.")]
    public async Task ShouldNotFillABankInAnotherNetwork()
    {
        await CreateSettlementAsync(_FIRST_SETTLEMENT_ID);
        await CreateSettlementAsync(_SECOND_SETTLEMENT_ID);

        int arrivalBankId = await CreateBankAsync(_playerId, _FIRST_SETTLEMENT_ID, new Dictionary<string, long> { [_OAK_LOG_CODE] = 20 });
        int unreachableBankId = await CreateBankAsync(_playerId, _SECOND_SETTLEMENT_ID, new Dictionary<string, long>());

        await CreditAsync(await BuildCreditAsync(_FIRST_SETTLEMENT_ID, _OAK_LOG_CODE, 10));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await ReadBalancesAsync(arrivalBankId), Is.EquivalentTo(new Dictionary<string, long> { [_OAK_LOG_CODE] = 30 }));
            Assert.That(await ReadBalancesAsync(unreachableBankId), Is.Empty);
        }
    }

    [Test, Description("An unkeyed bank reaches nothing but itself, because Guid.Empty is the absence of a key rather than a network two banks share.")]
    public async Task ShouldReachNothingButItselfWhenTheArrivalBankIsUnkeyed()
    {
        await CreateSettlementAsync(_FIRST_SETTLEMENT_ID);
        await CreateSettlementAsync(_SECOND_SETTLEMENT_ID);

        int arrivalBankId = await CreateBankAsync(_playerId, _FIRST_SETTLEMENT_ID, new Dictionary<string, long>(), Guid.Empty);
        int otherUnkeyedBankId = await CreateBankAsync(_playerId, _SECOND_SETTLEMENT_ID, new Dictionary<string, long>(), Guid.Empty);

        await CreditAsync(await BuildCreditAsync(_FIRST_SETTLEMENT_ID, _OAK_LOG_CODE, 10));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await ReadBalancesAsync(arrivalBankId), Is.EquivalentTo(new Dictionary<string, long> { [_OAK_LOG_CODE] = 10 }));
            Assert.That(await ReadBalancesAsync(otherUnkeyedBankId), Is.Empty);
        }
    }

    [Test, Description("Another Player's bank carrying the same key is not filled, because connectivity is assessed one Player at a time and a key alone would cross the ownership boundary.")]
    public async Task ShouldNotFillAnotherPlayersBankSharingTheKey()
    {
        Guid networkKey = Guid.NewGuid();

        await CreateSettlementAsync(_FIRST_SETTLEMENT_ID);
        await CreateSettlementAsync(_SECOND_SETTLEMENT_ID);

        int arrivalBankId = await CreateBankAsync(_playerId, _FIRST_SETTLEMENT_ID, new Dictionary<string, long>(), networkKey);
        int rivalBankId = await CreateBankAsync(_otherPlayerId, _SECOND_SETTLEMENT_ID, new Dictionary<string, long>(), networkKey);

        await CreditAsync(await BuildCreditAsync(_FIRST_SETTLEMENT_ID, _OAK_LOG_CODE, 10));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await ReadBalancesAsync(arrivalBankId), Is.EquivalentTo(new Dictionary<string, long> { [_OAK_LOG_CODE] = 10 }));
            Assert.That(await ReadBalancesAsync(rivalBankId), Is.Empty);
        }
    }

    [Test, Description("A second call on one context adds to the row the first created rather than inserting a duplicate of the composite key, because a query cannot see what has not been saved.")]
    public async Task ShouldAddToARowAnEarlierCallCreatedOnTheSameContext()
    {
        await CreateSettlementAsync(_FIRST_SETTLEMENT_ID);

        int bankId = await CreateBankAsync(_playerId, _FIRST_SETTLEMENT_ID, new Dictionary<string, long>());

        AssetCredit credit = await BuildCreditAsync(_FIRST_SETTLEMENT_ID, _OAK_LOG_CODE, 10);

        await using (AscendantEmpireContext context = await DbFactory.CreateDbContextAsync())
        {
            await AssertCreditSucceedsAsync(context, [credit]);
            await AssertCreditSucceedsAsync(context, [credit]);

            await context.SaveChangesAsync();
        }

        Assert.That(await ReadBalancesAsync(bankId), Is.EquivalentTo(new Dictionary<string, long> { [_OAK_LOG_CODE] = 20 }));
    }

    [Test, Description("A credit to a settlement with no bank of the Player's is a not-found, because the credit arrives there and a settlement that cannot bank cannot receive.")]
    public async Task ShouldRefuseACreditToASettlementWithNoBank()
    {
        await CreateSettlementAsync(_BANKLESS_SETTLEMENT_ID);

        ServiceResult<IReadOnlyList<AssetCreditPlacement>> result = await CreditExpectingFailureAsync(
            await BuildCreditAsync(_BANKLESS_SETTLEMENT_ID, _OAK_LOG_CODE, 10));

        Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.NotFound));
    }

    [Test, Description("A credit of an Asset that is not kept in a settlement bank is a server error rather than a shortfall of room, because population has no balance row to add to and never will.")]
    public async Task ShouldRefuseACreditOfAnAssetThatIsNotKeptInABank()
    {
        await CreateSettlementAsync(_FIRST_SETTLEMENT_ID);
        await CreateBankAsync(_playerId, _FIRST_SETTLEMENT_ID, new Dictionary<string, long>());

        ServiceResult<IReadOnlyList<AssetCreditPlacement>> result = await CreditExpectingFailureAsync(
            await BuildCreditAsync(_FIRST_SETTLEMENT_ID, _CIVILIANS_CODE, 10));

        Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.InternalServerError));
    }

    [Test, Description("A credit naming an Asset that is not in the catalogue is a not-found, so a caller is told which id it got wrong rather than seeing the credit vanish.")]
    public async Task ShouldRefuseACreditOfAnAssetThatIsNotInTheCatalogue()
    {
        await CreateSettlementAsync(_FIRST_SETTLEMENT_ID);
        await CreateBankAsync(_playerId, _FIRST_SETTLEMENT_ID, new Dictionary<string, long>());

        ServiceResult<IReadOnlyList<AssetCreditPlacement>> result = await CreditExpectingFailureAsync(
            AssetCredit.AtSettlement(_playerId, _FIRST_SETTLEMENT_ID, assetId: -1, ScaledAmount.FromWhole(10)));

        Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.NotFound));
    }

    [Test, Description("An empty batch is a success that writes nothing, so a Turn with no production does not have to guard the call.")]
    public async Task ShouldWriteNothingForAnEmptyBatch()
    {
        IReadOnlyList<AssetCreditPlacement> placements = await CreditAsync();

        Assert.That(placements, Is.Empty);
    }

    [TestCase(0, Description = "A zero credit is a caller that did not mean to call, so it cannot be built rather than being quietly placed as nothing.")]
    [TestCase(-10, Description = "A negative credit is an expense by another name, which the drawdown owns, so it cannot be built into a credit at all.")]
    public void ShouldRefuseToBuildACreditForANonPositiveAmount(int wholeAmount)
    {
        Assert.That(
            () => AssetCredit.AtSettlement(_playerId, _FIRST_SETTLEMENT_ID, assetId: 1, ScaledAmount.FromWhole(wholeAmount)),
            Throws.InstanceOf<ArgumentOutOfRangeException>());
    }

    /// <summary>Builds a credit of whole units arriving at one Settlement, looking the Asset's id up by its code.</summary>
    private async Task<AssetCredit> BuildCreditAsync(int settlementId, string assetCode, long wholeAmount)
    {
        return AssetCredit.AtSettlement(_playerId, settlementId, await GetAssetIdAsync(assetCode), ScaledAmount.FromWhole(wholeAmount));
    }

    /// <summary>Credits on its own context and saves, which is what an end-of-Turn process does.</summary>
    private async Task<IReadOnlyList<AssetCreditPlacement>> CreditAsync(params AssetCredit[] credits)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        IReadOnlyList<AssetCreditPlacement> placements = await AssertCreditSucceedsAsync(context, credits);

        await context.SaveChangesAsync();

        return placements;
    }

    private async Task<ServiceResult<IReadOnlyList<AssetCreditPlacement>>> CreditExpectingFailureAsync(params AssetCredit[] credits)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        ServiceResult<IReadOnlyList<AssetCreditPlacement>> result = await _service.CreditAsync(context, credits, CancellationToken.None);

        Assert.That(result.HasSucceeded, Is.False, "the credit was expected to be refused.");

        return result;
    }

    private async Task<IReadOnlyList<AssetCreditPlacement>> AssertCreditSucceedsAsync(AscendantEmpireContext context, IReadOnlyList<AssetCredit> credits)
    {
        ServiceResult<IReadOnlyList<AssetCreditPlacement>> result = await _service.CreditAsync(context, credits, CancellationToken.None);

        Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));

        return result.SuccessValue!;
    }

    private static long Raw(long wholeAmount)
    {
        return ScaledAmount.FromWhole(wholeAmount).RawValue;
    }

    private static long PoolInWholeUnits(SettlementTier tier)
    {
        Assert.That(SettlementStorageRules.TryGetGenericPoolCapacity(tier, out long rawCapacity), Is.True);

        return ScaledAmount.FromRaw(rawCapacity).ToWholeRoundedDown();
    }
}
