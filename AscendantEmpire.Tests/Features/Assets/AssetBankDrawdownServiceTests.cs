using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Assets.Classes;
using AscendantEmpire.Application.Features.Assets.Services;
using AscendantEmpire.Application.Helpers.ScaledNumerics;
using AscendantEmpire.BackendContracts.Assets;

using Microsoft.Extensions.Logging.Abstractions;

namespace AscendantEmpire.Tests.Features.Assets;

/// <summary>
/// Tests the single spender: what an expense takes, out of which banks, in what order, and how it refuses what the Player cannot afford.
/// </summary>
/// <remarks>
/// <para>See <see cref="AssetBankTestBase"/> for the tier and the seeded fixture.</para>
/// <para>
/// Every case saves the context after the call, whatever the result. That is what pins "a refused expense stages nothing": a failure that had written to a
/// balance would survive the save and show up in the assertion.
/// </para>
/// </remarks>
[TestFixture]
internal class AssetBankDrawdownServiceTests : AssetBankTestBase
{
    private const int _SETTLEMENT_ID = 41;
    private const int _SECOND_SETTLEMENT_ID = 42;
    private const int _THIRD_SETTLEMENT_ID = 43;
    private const int _FAR_SETTLEMENT_ID = 77;

    // Building an expense never reads the catalogue, so a bare id stands in wherever a case only cares about what the factories refuse to build.
    private const int _ANY_ASSET_ID = 1;

    // The grouped arm of the population category: every Asset beneath it is Derived, so none of it is bankable.
    private const string _GROUP_MY_PEOPLE_CODE = "Society.Population.MyPeople";

    private AssetBankDrawdownService _service = null!;

    [SetUp]
    public void CreateService()
    {
        _service = new AssetBankDrawdownService(NullLogger<AssetBankDrawdownService>.Instance);
    }

    // ---------------------------------------------------------------------------------------------------------------------------------------------------
    // At one Settlement - the bank that Settlement draws on, and that bank alone.
    // ---------------------------------------------------------------------------------------------------------------------------------------------------

    [Test, Description("A settlement expense is met from the bank carrying that SettlementId, so a spend lands on the Settlement's own store.")]
    public async Task ShouldDebitTheBankKeyedOnTheSettlement()
    {
        int assetBankId = await CreateBankAsync(_playerId, _SETTLEMENT_ID, new Dictionary<string, long> { [_OAK_LOG_CODE] = 10 });

        ServiceResultBase result = await DebitAssetAtSettlementAsync(_OAK_LOG_CODE, whole: 4);

        Dictionary<string, long> balances = await ReadBalancesAsync(assetBankId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));
            Assert.That(balances[_OAK_LOG_CODE], Is.EqualTo(6));
        }
    }

    [Test, Description("An Asset expense takes only the Asset it names, leaving the rest of the group alone however the ordering would have walked it.")]
    public async Task ShouldDebitOnlyTheNamedAsset()
    {
        int assetBankId = await CreateBankAsync(
            _playerId,
            _SETTLEMENT_ID,
            new Dictionary<string, long> { [_OAK_LOG_CODE] = 10, [_GENERIC_LOG_CODE] = 10 });

        ServiceResultBase result = await DebitAssetAtSettlementAsync(_OAK_LOG_CODE, whole: 4);

        Dictionary<string, long> balances = await ReadBalancesAsync(assetBankId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));
            Assert.That(balances[_OAK_LOG_CODE], Is.EqualTo(6));
            Assert.That(balances[_GENERIC_LOG_CODE], Is.EqualTo(10));
        }
    }

    [Test, Description("An Asset expense short of the Asset it named fails rather than falling through to the group, since a caller naming Oak has already decided it does not want Generic.")]
    public async Task ShouldRefuseAnAssetExpenseWithoutFallingThroughToTheGroup()
    {
        int assetBankId = await CreateBankAsync(
            _playerId,
            _SETTLEMENT_ID,
            new Dictionary<string, long> { [_OAK_LOG_CODE] = 4, [_GENERIC_LOG_CODE] = 100 });

        ServiceResultBase result = await DebitAssetAtSettlementAsync(_OAK_LOG_CODE, whole: 10);

        Dictionary<string, long> balances = await ReadBalancesAsync(assetBankId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.Conflict));
            Assert.That(result.Errors.Single(), Is.EqualTo("Not enough Oak Log: 10 needed, 4 available."));
            Assert.That(balances[_OAK_LOG_CODE], Is.EqualTo(4));
            Assert.That(balances[_GENERIC_LOG_CODE], Is.EqualTo(100));
        }
    }

    [Test, Description("A group expense takes each Asset to nothing before touching the next, in the catalogue's order, and stops as soon as the amount is met.")]
    public async Task ShouldSpendAGroupExpenseInOrderZeroingEachAssetFirst()
    {
        // Seeded sort orders space by 10 in declaration order, so the spend order is Oak, Generic, Cedar.
        int assetBankId = await CreateBankAsync(
            _playerId,
            _SETTLEMENT_ID,
            new Dictionary<string, long> { [_OAK_LOG_CODE] = 4, [_GENERIC_LOG_CODE] = 20, [_CEDAR_LOG_CODE] = 5 });

        ServiceResultBase result = await DebitGroupAtSettlementAsync(_GROUP_LOGS_CODE, whole: 10);

        Dictionary<string, long> balances = await ReadBalancesAsync(assetBankId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));
            Assert.That(balances.ContainsKey(_OAK_LOG_CODE), Is.False, "the first Asset in the ordering is zeroed, and an emptied row is deleted");
            Assert.That(balances[_GENERIC_LOG_CODE], Is.EqualTo(14), "the remaining 6 comes out of the next Asset in the ordering");
            Assert.That(balances[_CEDAR_LOG_CODE], Is.EqualTo(5), "the walk stops once the amount is met");
        }
    }

    [Test, Description("A group expense the group cannot cover between them leaves every balance exactly as it was, rather than zeroing the first and then discovering the shortfall.")]
    public async Task ShouldLeaveTheBankUntouchedWhenAGroupExpenseCannotBeCovered()
    {
        int assetBankId = await CreateBankAsync(
            _playerId,
            _SETTLEMENT_ID,
            new Dictionary<string, long> { [_OAK_LOG_CODE] = 4, [_GENERIC_LOG_CODE] = 2 });

        ServiceResultBase result = await DebitGroupAtSettlementAsync(_GROUP_LOGS_CODE, whole: 10);

        Dictionary<string, long> balances = await ReadBalancesAsync(assetBankId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.Conflict));
            Assert.That(balances[_OAK_LOG_CODE], Is.EqualTo(4));
            Assert.That(balances[_GENERIC_LOG_CODE], Is.EqualTo(2));
        }
    }

    [Test, Description("A refusal names the group rather than one of its Assets and says what was short, because the caller asked for Logs and has to tell the Player why it was refused.")]
    public async Task ShouldReportWhatWasShortAndByHowMuch()
    {
        await CreateBankAsync(
            _playerId,
            _SETTLEMENT_ID,
            new Dictionary<string, long> { [_OAK_LOG_CODE] = 4, [_GENERIC_LOG_CODE] = 2 });

        int logsGroupId = await GetAssetGroupIdAsync(_GROUP_LOGS_CODE);

        ServiceResultBase result = await DebitGroupAtSettlementAsync(_GROUP_LOGS_CODE, whole: 10);

        var shortfall = result.ErrorDetail as AssetShortfall;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Errors.Single(), Is.EqualTo("Not enough Logs: 10 needed, 6 available."));
            Assert.That(shortfall, Is.Not.Null, "the caller may have to offer the missing amount on the market rather than just print the sentence");
            Assert.That(shortfall!.AssetGroupId, Is.EqualTo(logsGroupId));
            Assert.That(shortfall.AssetId, Is.Null);
            Assert.That(shortfall.DisplayName, Is.EqualTo("Logs"));
            Assert.That(shortfall.RawRequired, Is.EqualTo(ScaledAmount.FromWhole(10).RawValue));
            Assert.That(shortfall.RawAvailable, Is.EqualTo(ScaledAmount.FromWhole(6).RawValue));
            Assert.That(shortfall.RawShortfall, Is.EqualTo(ScaledAmount.FromWhole(4).RawValue));
        }
    }

    [Test, Description("A balance taken to nothing is deleted rather than left at zero, so a bank does not accumulate a row per Asset it has ever briefly held.")]
    public async Task ShouldDeleteABalanceTakenToNothing()
    {
        int assetBankId = await CreateBankAsync(_playerId, _SETTLEMENT_ID, new Dictionary<string, long> { [_OAK_LOG_CODE] = 4 });

        ServiceResultBase result = await DebitAssetAtSettlementAsync(_OAK_LOG_CODE, whole: 4);

        Dictionary<string, long> balances = await ReadBalancesAsync(assetBankId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));
            Assert.That(balances, Is.Empty);
        }
    }

    [Test, Description("A group expense never reaches an Asset outside the group it named, so a bank full of Planks cannot pay for Logs.")]
    public async Task ShouldNotReachAnAssetOutsideTheNamedGroup()
    {
        int assetBankId = await CreateBankAsync(
            _playerId,
            _SETTLEMENT_ID,
            new Dictionary<string, long> { [_OAK_LOG_CODE] = 4, [_OAK_PLANK_CODE] = 100 });

        ServiceResultBase result = await DebitGroupAtSettlementAsync(_GROUP_LOGS_CODE, whole: 10);

        Dictionary<string, long> balances = await ReadBalancesAsync(assetBankId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.Conflict));
            Assert.That(result.Errors.Single(), Does.Contain("4 available"), "only the group's own Assets count towards what is available");
            Assert.That(balances[_OAK_PLANK_CODE], Is.EqualTo(100));
        }
    }

    [Test, Description("A Settlement expense is met from its own bank only, so a second bank holding plenty does not rescue it and is left untouched.")]
    public async Task ShouldNotReachASecondBankForASettlementExpense()
    {
        int ownBankId = await CreateBankAsync(_playerId, _SETTLEMENT_ID, new Dictionary<string, long> { [_OAK_LOG_CODE] = 4 });
        int otherBankId = await CreateBankAsync(_playerId, _SECOND_SETTLEMENT_ID, new Dictionary<string, long> { [_OAK_LOG_CODE] = 100 });

        ServiceResultBase result = await DebitAssetAtSettlementAsync(_OAK_LOG_CODE, whole: 10);

        Dictionary<string, long> ownBalances = await ReadBalancesAsync(ownBankId);
        Dictionary<string, long> otherBalances = await ReadBalancesAsync(otherBankId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.Conflict));
            Assert.That(result.Errors.Single(), Does.Contain("4 available"), "only the Settlement's own bank counts towards what is available");
            Assert.That(ownBalances[_OAK_LOG_CODE], Is.EqualTo(4));
            Assert.That(otherBalances[_OAK_LOG_CODE], Is.EqualTo(100), "reaching the nearest bank that holds the remainder is RS-27, and is not this");
        }
    }

    [Test, Description("A Settlement with no asset bank has nothing to spend from, which is a not-found rather than a shortfall of zero.")]
    public async Task ShouldAnswerNotFoundWhenTheSettlementHasNoBank()
    {
        await CreateBankAsync(_playerId, _SETTLEMENT_ID, new Dictionary<string, long> { [_OAK_LOG_CODE] = 10 });

        ServiceResultBase result = await DebitAsync(AssetExpense.AtSettlement(
            playerId: _playerId,
            settlementId: _FAR_SETTLEMENT_ID,
            target: SpendTarget.Asset(await GetAssetIdAsync(_OAK_LOG_CODE)),
            amount: ScaledAmount.FromWhole(1)));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.NotFound));
            Assert.That(result.Errors.Single(), Does.Contain($"Settlement {_FAR_SETTLEMENT_ID}"));
        }
    }

    [Test, Description("Naming another Player's Settlement reaches no bank at all, so a Player cannot spend out of a rival's pool by passing their settlement id.")]
    public async Task ShouldNotReachAnotherPlayersBank()
    {
        int rivalBankId = await CreateBankAsync(_otherPlayerId, _SETTLEMENT_ID, new Dictionary<string, long> { [_OAK_LOG_CODE] = 100 });

        ServiceResultBase result = await DebitAssetAtSettlementAsync(_OAK_LOG_CODE, whole: 10);

        Dictionary<string, long> balances = await ReadBalancesAsync(rivalBankId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.NotFound));
            Assert.That(balances[_OAK_LOG_CODE], Is.EqualTo(100));
        }
    }

    // ---------------------------------------------------------------------------------------------------------------------------------------------------
    // Empire-wide - every bank the Player owns, spread by levelling.
    // ---------------------------------------------------------------------------------------------------------------------------------------------------

    [Test, Description("An Empire-wide expense cuts the fullest banks down to a common level rather than draining one or charging each a proportion.")]
    public async Task ShouldLevelAnEmpireWideExpenseDownToACommonWaterLine()
    {
        int fullBankId = await CreateBankAsync(_playerId, _SETTLEMENT_ID, new Dictionary<string, long> { [_OAK_LOG_CODE] = 50 });
        int middlingBankId = await CreateBankAsync(_playerId, _SECOND_SETTLEMENT_ID, new Dictionary<string, long> { [_OAK_LOG_CODE] = 30 });
        int smallBankId = await CreateBankAsync(_playerId, _THIRD_SETTLEMENT_ID, new Dictionary<string, long> { [_OAK_LOG_CODE] = 4 });

        ServiceResultBase result = await DebitAssetEmpireWideAsync(_OAK_LOG_CODE, whole: 40);

        Dictionary<string, long> fullBalances = await ReadBalancesAsync(fullBankId);
        Dictionary<string, long> middlingBalances = await ReadBalancesAsync(middlingBankId);
        Dictionary<string, long> smallBalances = await ReadBalancesAsync(smallBankId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));
            Assert.That(fullBalances[_OAK_LOG_CODE], Is.EqualTo(20), "the water-line falls at 20, so the fullest bank gives 30 of the 40");
            Assert.That(middlingBalances[_OAK_LOG_CODE], Is.EqualTo(20), "and the next fullest gives the other 10, levelling the two");
            Assert.That(smallBalances[_OAK_LOG_CODE], Is.EqualTo(4), "a bank below the line pays nothing, and keeps the stock its own construction needs");
        }
    }

    [Test, Description("A bank holding far less than the water-line is never touched, however lopsided the Empire's stock is.")]
    public async Task ShouldTakeNothingFromABankBelowTheWaterLine()
    {
        int fullBankId = await CreateBankAsync(_playerId, _SETTLEMENT_ID, new Dictionary<string, long> { [_OAK_LOG_CODE] = 100 });
        int smallBankId = await CreateBankAsync(_playerId, _SECOND_SETTLEMENT_ID, new Dictionary<string, long> { [_OAK_LOG_CODE] = 1 });

        ServiceResultBase result = await DebitAssetEmpireWideAsync(_OAK_LOG_CODE, whole: 10);

        Dictionary<string, long> fullBalances = await ReadBalancesAsync(fullBankId);
        Dictionary<string, long> smallBalances = await ReadBalancesAsync(smallBankId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));
            Assert.That(fullBalances[_OAK_LOG_CODE], Is.EqualTo(90), "the whole cost comes off the only bank above the line");
            Assert.That(smallBalances[_OAK_LOG_CODE], Is.EqualTo(1));
        }
    }

    [Test, Description("An Empire-wide group expense zeroes one Asset across every bank before starting the next, so the Player's ordering holds at the scale the expense is paid at.")]
    public async Task ShouldZeroAnAssetInEveryBankBeforeMovingToTheNextInTheGroup()
    {
        // Spend order is Oak, Generic, Cedar. The Cedar is in the first bank so that a walk which drained a bank rather than an Asset would take it.
        int firstBankId = await CreateBankAsync(
            _playerId,
            _SETTLEMENT_ID,
            new Dictionary<string, long> { [_OAK_LOG_CODE] = 4, [_CEDAR_LOG_CODE] = 100 });

        int secondBankId = await CreateBankAsync(
            _playerId,
            _SECOND_SETTLEMENT_ID,
            new Dictionary<string, long> { [_OAK_LOG_CODE] = 3, [_GENERIC_LOG_CODE] = 20 });

        ServiceResultBase result = await DebitGroupEmpireWideAsync(_GROUP_LOGS_CODE, whole: 10);

        Dictionary<string, long> firstBalances = await ReadBalancesAsync(firstBankId);
        Dictionary<string, long> secondBalances = await ReadBalancesAsync(secondBankId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));
            Assert.That(firstBalances.ContainsKey(_OAK_LOG_CODE), Is.False, "every bank's Oak goes before any later Asset is touched");
            Assert.That(secondBalances.ContainsKey(_OAK_LOG_CODE), Is.False);
            Assert.That(secondBalances[_GENERIC_LOG_CODE], Is.EqualTo(17), "the remaining 3 comes out of the next Asset in the ordering, wherever it is held");
            Assert.That(firstBalances[_CEDAR_LOG_CODE], Is.EqualTo(100), "the walk stops once the amount is met, rather than finishing off the bank it started in");
        }
    }

    [Test, Description("A leftover raw unit goes to the richest bank first and then to the lower bank id, so the same expense against the same banks always spends the same rows.")]
    public async Task ShouldSpendALeftoverRawUnitRichestFirstThenByLowerBankId()
    {
        // Three equal banks and a cost that does not divide by three: 5 out of 30 puts the line at 8.333, leaving two raw units to place.
        int firstBankId = await CreateBankAsync(_playerId, _SETTLEMENT_ID, new Dictionary<string, long> { [_OAK_LOG_CODE] = 10 });
        int secondBankId = await CreateBankAsync(_playerId, _SECOND_SETTLEMENT_ID, new Dictionary<string, long> { [_OAK_LOG_CODE] = 10 });
        int thirdBankId = await CreateBankAsync(_playerId, _THIRD_SETTLEMENT_ID, new Dictionary<string, long> { [_OAK_LOG_CODE] = 10 });

        ServiceResultBase result = await DebitAssetEmpireWideAsync(_OAK_LOG_CODE, whole: 5);

        Dictionary<string, long> firstBalances = await ReadRawBalancesAsync(firstBankId);
        Dictionary<string, long> secondBalances = await ReadRawBalancesAsync(secondBankId);
        Dictionary<string, long> thirdBalances = await ReadRawBalancesAsync(thirdBankId);

        long totalRaw = firstBalances[_OAK_LOG_CODE] + secondBalances[_OAK_LOG_CODE] + thirdBalances[_OAK_LOG_CODE];

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));
            Assert.That(firstBalances[_OAK_LOG_CODE], Is.EqualTo(8_333), "the two lower bank ids carry the leftover unit, every bank being equally rich");
            Assert.That(secondBalances[_OAK_LOG_CODE], Is.EqualTo(8_333));
            Assert.That(thirdBalances[_OAK_LOG_CODE], Is.EqualTo(8_334));
            Assert.That(totalRaw, Is.EqualTo(ScaledAmount.FromWhole(25).RawValue), "exactly the amount asked for is spent, to the raw unit");
        }
    }

    [Test, Description("An Empire-wide expense adds every bank up when deciding what is available, since no boundary between them is being spent across.")]
    public async Task ShouldAddEveryBankUpWhenReportingWhatIsAvailable()
    {
        int firstBankId = await CreateBankAsync(_playerId, _SETTLEMENT_ID, new Dictionary<string, long> { [_OAK_LOG_CODE] = 4 });
        int secondBankId = await CreateBankAsync(_playerId, _SECOND_SETTLEMENT_ID, new Dictionary<string, long> { [_OAK_LOG_CODE] = 2 });

        ServiceResultBase result = await DebitAssetEmpireWideAsync(_OAK_LOG_CODE, whole: 10);

        Dictionary<string, long> firstBalances = await ReadBalancesAsync(firstBankId);
        Dictionary<string, long> secondBalances = await ReadBalancesAsync(secondBankId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.Conflict));
            Assert.That(result.Errors.Single(), Is.EqualTo("Not enough Oak Log: 10 needed, 6 available."), "the two banks are summed, and neither is named");
            Assert.That(firstBalances[_OAK_LOG_CODE], Is.EqualTo(4), "a refused Empire-wide expense stages nothing in any bank");
            Assert.That(secondBalances[_OAK_LOG_CODE], Is.EqualTo(2));
        }
    }

    [Test, Description("A Player who owns no bank at all is short of the whole amount, which is a shortfall rather than a not-found: there is no Settlement for the expense to be missing.")]
    public async Task ShouldRefuseAnEmpireWideExpenseAsAShortfallWhenThePlayerOwnsNoBank()
    {
        ServiceResultBase result = await DebitAssetEmpireWideAsync(_OAK_LOG_CODE, whole: 10);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.Conflict));
            Assert.That(result.Errors.Single(), Does.Contain("0 available"));
        }
    }

    [Test, Description("Empire-wide means the paying Player's Empire, so a rival's banks are neither spent from nor counted towards what is available.")]
    public async Task ShouldNotReachAnotherPlayersBankEmpireWide()
    {
        int ownBankId = await CreateBankAsync(_playerId, _SETTLEMENT_ID, new Dictionary<string, long> { [_OAK_LOG_CODE] = 4 });
        int rivalBankId = await CreateBankAsync(_otherPlayerId, _SECOND_SETTLEMENT_ID, new Dictionary<string, long> { [_OAK_LOG_CODE] = 100 });

        ServiceResultBase result = await DebitAssetEmpireWideAsync(_OAK_LOG_CODE, whole: 10);

        Dictionary<string, long> ownBalances = await ReadBalancesAsync(ownBankId);
        Dictionary<string, long> rivalBalances = await ReadBalancesAsync(rivalBankId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.Conflict));
            Assert.That(result.Errors.Single(), Does.Contain("4 available"));
            Assert.That(ownBalances[_OAK_LOG_CODE], Is.EqualTo(4));
            Assert.That(rivalBalances[_OAK_LOG_CODE], Is.EqualTo(100));
        }
    }

    [Test, Description("An Empire-wide expense taking an Asset out entirely deletes every emptied row, rather than leaving a zero in each bank that held it.")]
    public async Task ShouldDeleteEveryBalanceAnEmpireWideExpenseEmpties()
    {
        int firstBankId = await CreateBankAsync(_playerId, _SETTLEMENT_ID, new Dictionary<string, long> { [_OAK_LOG_CODE] = 4 });
        int secondBankId = await CreateBankAsync(_playerId, _SECOND_SETTLEMENT_ID, new Dictionary<string, long> { [_OAK_LOG_CODE] = 6 });

        ServiceResultBase result = await DebitAssetEmpireWideAsync(_OAK_LOG_CODE, whole: 10);

        Dictionary<string, long> firstBalances = await ReadBalancesAsync(firstBankId);
        Dictionary<string, long> secondBalances = await ReadBalancesAsync(secondBankId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));
            Assert.That(firstBalances, Is.Empty);
            Assert.That(secondBalances, Is.Empty);
        }
    }

    // ---------------------------------------------------------------------------------------------------------------------------------------------------
    // Assets held somewhere other than a bank.
    // ---------------------------------------------------------------------------------------------------------------------------------------------------

    [Test, Description("Debiting a Derived Asset is a programming error rather than a shortfall: population is counted off what already records it and is not something a Player ran out of.")]
    public async Task ShouldRefuseADerivedAssetAsAProgrammingError()
    {
        await CreateBankAsync(_playerId, _SETTLEMENT_ID, new Dictionary<string, long>());

        ServiceResultBase result = await DebitAssetAtSettlementAsync(_CIVILIANS_CODE, whole: 1);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.InternalServerError));
            Assert.That(result.Errors.Single(), Does.Contain("Civilians"));
        }
    }

    [Test, Description("A group whose every Asset is Derived has no balances to order, so naming it is the same mistake as naming a Derived Asset outright.")]
    public async Task ShouldRefuseAGroupWithNoBankableAssetAsAProgrammingError()
    {
        await CreateBankAsync(_playerId, _SETTLEMENT_ID, new Dictionary<string, long>());

        ServiceResultBase result = await DebitGroupAtSettlementAsync(_GROUP_MY_PEOPLE_CODE, whole: 1);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.InternalServerError));
            Assert.That(result.Errors.Single(), Does.Contain("My People"));
        }
    }

    [Test, Description("A Player-scope Asset is debited from the store that already holds its figure rather than from any bank, so no membership is needed to spend it.")]
    public async Task ShouldDebitThePlayerLevelStoreForTechnologyExperience()
    {
        await CreateResearchStateAsync(_playerId, unspentDirectedPoints: 10);

        ServiceResultBase result = await DebitAssetEmpireWideAsync(_TECHNOLOGY_EXPERIENCE_CODE, whole: 4);

        (int directedPoints, _) = await ReadResearchPointsAsync(_playerId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));
            Assert.That(directedPoints, Is.EqualTo(6));
        }
    }

    [Test, Description("A figure held once per Player for the whole game cannot be spent at a Settlement, so naming one is reported rather than quietly ignored.")]
    public async Task ShouldRefuseAPlayerScopeAssetSpentAtASettlement()
    {
        await CreateResearchStateAsync(_playerId, unspentDirectedPoints: 10);

        ServiceResultBase result = await DebitAssetAtSettlementAsync(_TECHNOLOGY_EXPERIENCE_CODE, whole: 4);

        (int directedPoints, _) = await ReadResearchPointsAsync(_playerId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.InternalServerError));
            Assert.That(result.Errors.Single(), Does.Contain("Empire-wide"));
            Assert.That(directedPoints, Is.EqualTo(10), "the refusal leaves the store alone rather than spending it anyway");
        }
    }

    [Test, Description("Diffusion points are not part of the spendable pool: they arrive attached to a neighbour rather than as a budget, so an expense cannot consume them.")]
    public async Task ShouldNotSpendDiffusionPoints()
    {
        await CreateResearchStateAsync(_playerId, unspentDirectedPoints: 4, unspentDiffusionPoints: 100);

        ServiceResultBase result = await DebitAssetEmpireWideAsync(_TECHNOLOGY_EXPERIENCE_CODE, whole: 10);

        (int directedPoints, int diffusionPoints) = await ReadResearchPointsAsync(_playerId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.Conflict));
            Assert.That(result.Errors.Single(), Does.Contain("4 available"));
            Assert.That(directedPoints, Is.EqualTo(4));
            Assert.That(diffusionPoints, Is.EqualTo(100));
        }
    }

    [Test, Description("Culture is held per Player but nothing stores it yet, so spending it is refused loudly rather than written somewhere that is not the number the panel reads.")]
    public async Task ShouldRefuseCultureWhichHasNoPlayerLevelStoreYet()
    {
        await CreateResearchStateAsync(_playerId, unspentDirectedPoints: 100);

        ServiceResultBase result = await DebitAssetEmpireWideAsync(_CULTURE_EXPERIENCE_CODE, whole: 1);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.InternalServerError));
            Assert.That(result.Errors.Single(), Does.Contain("Culture"));
        }
    }

    [Test, Description("The player-level store counts whole points, so a fractional expense is refused rather than rounded into an over- or under-charge.")]
    public async Task ShouldRefuseAFractionalPlayerLevelExpense()
    {
        await CreateResearchStateAsync(_playerId, unspentDirectedPoints: 10);

        ServiceResultBase result = await DebitAsync(AssetExpense.EmpireWide(
            playerId: _playerId,
            target: SpendTarget.Asset(await GetAssetIdAsync(_TECHNOLOGY_EXPERIENCE_CODE)),
            amount: ScaledAmount.FromDecimal(0.5m)));

        (int directedPoints, _) = await ReadResearchPointsAsync(_playerId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.InternalServerError));
            Assert.That(directedPoints, Is.EqualTo(10));
        }
    }

    [Test, Description("A Player with no research state cannot have their points spent, which is a not-found rather than a shortfall: the row should be there.")]
    public async Task ShouldAnswerNotFoundWhenThePlayerHasNoResearchState()
    {
        ServiceResultBase result = await DebitAssetEmpireWideAsync(_TECHNOLOGY_EXPERIENCE_CODE, whole: 1);

        Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.NotFound));
    }

    // ---------------------------------------------------------------------------------------------------------------------------------------------------
    // What an expense has to be, and what the caller owns.
    // ---------------------------------------------------------------------------------------------------------------------------------------------------

    [TestCase(0, Description = "A zero expense is a caller that did not mean to call, and cannot be built at all rather than being refused once it is spent.")]
    [TestCase(-5, Description = "A negative expense is a credit by another name, which production owns, so neither factory will build one.")]
    public void ShouldNotBuildANonPositiveExpense(int whole)
    {
        ScaledAmount amount = ScaledAmount.FromWhole(whole);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(
                () => AssetExpense.AtSettlement(playerId: _playerId, settlementId: _SETTLEMENT_ID, target: SpendTarget.Asset(_ANY_ASSET_ID), amount: amount),
                Throws.InstanceOf<ArgumentOutOfRangeException>());

            Assert.That(
                () => AssetExpense.EmpireWide(playerId: _playerId, target: SpendTarget.Asset(_ANY_ASSET_ID), amount: amount),
                Throws.InstanceOf<ArgumentOutOfRangeException>());
        }
    }

    [Test, Description("An Asset that is not in the catalogue is a not-found, so a stale id reads as a missing row rather than as an empty balance.")]
    public async Task ShouldAnswerNotFoundForAnUnknownAsset()
    {
        await CreateBankAsync(_playerId, _SETTLEMENT_ID, new Dictionary<string, long>());

        ServiceResultBase result = await DebitAsync(AssetExpense.AtSettlement(
            playerId: _playerId,
            settlementId: _SETTLEMENT_ID,
            target: SpendTarget.Asset(999_999),
            amount: ScaledAmount.FromWhole(1)));

        Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.NotFound));
    }

    [Test, Description("An AssetGroup that is not in the catalogue is a not-found, reported against the group rather than silently resolving to no candidates.")]
    public async Task ShouldAnswerNotFoundForAnUnknownAssetGroup()
    {
        await CreateBankAsync(_playerId, _SETTLEMENT_ID, new Dictionary<string, long>());

        ServiceResultBase result = await DebitAsync(AssetExpense.AtSettlement(
            playerId: _playerId,
            settlementId: _SETTLEMENT_ID,
            target: SpendTarget.Group(999_999),
            amount: ScaledAmount.FromWhole(1)));

        Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.NotFound));
    }

    [Test, Description("The debit is staged on the caller's context and not saved, so a purchase that abandons its own save leaves the balance alone.")]
    public async Task ShouldNotCommitTheDebitWithoutTheCallersSave()
    {
        int assetBankId = await CreateBankAsync(_playerId, _SETTLEMENT_ID, new Dictionary<string, long> { [_OAK_LOG_CODE] = 10 });

        await using (AscendantEmpireContext context = await DbFactory.CreateDbContextAsync())
        {
            ServiceResultBase result = await _service.DebitAsync(
                context,
                AssetExpense.AtSettlement(
                    playerId: _playerId,
                    settlementId: _SETTLEMENT_ID,
                    target: SpendTarget.Asset(await GetAssetIdAsync(_OAK_LOG_CODE)),
                    amount: ScaledAmount.FromWhole(4)),
                CancellationToken.None);

            Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));
        }

        Dictionary<string, long> balances = await ReadBalancesAsync(assetBankId);

        Assert.That(balances[_OAK_LOG_CODE], Is.EqualTo(10));
    }

    private async Task<ServiceResultBase> DebitAssetAtSettlementAsync(string assetCode, int whole)
    {
        return await DebitAsync(AssetExpense.AtSettlement(
            playerId: _playerId,
            settlementId: _SETTLEMENT_ID,
            target: SpendTarget.Asset(await GetAssetIdAsync(assetCode)),
            amount: ScaledAmount.FromWhole(whole)));
    }

    private async Task<ServiceResultBase> DebitGroupAtSettlementAsync(string groupCode, int whole)
    {
        return await DebitAsync(AssetExpense.AtSettlement(
            playerId: _playerId,
            settlementId: _SETTLEMENT_ID,
            target: SpendTarget.Group(await GetAssetGroupIdAsync(groupCode)),
            amount: ScaledAmount.FromWhole(whole)));
    }

    private async Task<ServiceResultBase> DebitAssetEmpireWideAsync(string assetCode, int whole)
    {
        return await DebitAsync(AssetExpense.EmpireWide(
            playerId: _playerId,
            target: SpendTarget.Asset(await GetAssetIdAsync(assetCode)),
            amount: ScaledAmount.FromWhole(whole)));
    }

    private async Task<ServiceResultBase> DebitGroupEmpireWideAsync(string groupCode, int whole)
    {
        return await DebitAsync(AssetExpense.EmpireWide(
            playerId: _playerId,
            target: SpendTarget.Group(await GetAssetGroupIdAsync(groupCode)),
            amount: ScaledAmount.FromWhole(whole)));
    }

    /// <summary>
    /// Runs the debit and then saves, whatever it answered.
    /// </summary>
    /// <remarks>
    /// Saving after a refusal is the point: the service promises to stage nothing on failure, so anything it had written to a balance would be committed here
    /// and the arrangement's figures would not read back.
    /// </remarks>
    private async Task<ServiceResultBase> DebitAsync(AssetExpense expense)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        ServiceResultBase result = await _service.DebitAsync(context, expense, CancellationToken.None);

        await context.SaveChangesAsync();

        return result;
    }
}
