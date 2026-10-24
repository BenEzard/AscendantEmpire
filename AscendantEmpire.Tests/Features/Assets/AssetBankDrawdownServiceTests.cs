using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Assets.Classes;
using AscendantEmpire.Application.Features.Assets.Services;
using AscendantEmpire.Application.Helpers.ScaledNumerics;
using AscendantEmpire.BackendContracts.Assets;

using Microsoft.Extensions.Logging.Abstractions;

namespace AscendantEmpire.Tests.Features.Assets;

/// <summary>
/// Tests the single spender: what an expense takes, in what order, and how it refuses what the Player cannot afford.
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
    private const int _FAR_SETTLEMENT_ID = 77;

    // The grouped arm of the population category: every Asset beneath it is Derived, so none of it is bankable.
    private const string _GROUP_MY_PEOPLE_CODE = "Society.Population.MyPeople";

    private AssetBankDrawdownService _service = null!;

    [SetUp]
    public void CreateService()
    {
        _service = new AssetBankDrawdownService(NullLogger<AssetBankDrawdownService>.Instance);
    }

    [Test, Description("An Asset expense takes only the Asset it names, leaving the rest of the group alone however the ordering would have walked it.")]
    public async Task ShouldDebitOnlyTheNamedAsset()
    {
        int assetBankId = await CreateBankAsync(
            _playerId,
            [_SETTLEMENT_ID],
            new Dictionary<string, long> { [_OAK_LOG_CODE] = 10, [_GENERIC_LOG_CODE] = 10 });

        ServiceResultBase result = await DebitAssetAsync(_OAK_LOG_CODE, whole: 4);

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
            [_SETTLEMENT_ID],
            new Dictionary<string, long> { [_OAK_LOG_CODE] = 4, [_GENERIC_LOG_CODE] = 100 });

        ServiceResultBase result = await DebitAssetAsync(_OAK_LOG_CODE, whole: 10);

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
            [_SETTLEMENT_ID],
            new Dictionary<string, long> { [_OAK_LOG_CODE] = 4, [_GENERIC_LOG_CODE] = 20, [_CEDAR_LOG_CODE] = 5 });

        ServiceResultBase result = await DebitGroupAsync(_GROUP_LOGS_CODE, whole: 10);

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
            [_SETTLEMENT_ID],
            new Dictionary<string, long> { [_OAK_LOG_CODE] = 4, [_GENERIC_LOG_CODE] = 2 });

        ServiceResultBase result = await DebitGroupAsync(_GROUP_LOGS_CODE, whole: 10);

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
            [_SETTLEMENT_ID],
            new Dictionary<string, long> { [_OAK_LOG_CODE] = 4, [_GENERIC_LOG_CODE] = 2 });

        int logsGroupId = await GetAssetGroupIdAsync(_GROUP_LOGS_CODE);

        ServiceResultBase result = await DebitGroupAsync(_GROUP_LOGS_CODE, whole: 10);

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
        int assetBankId = await CreateBankAsync(_playerId, [_SETTLEMENT_ID], new Dictionary<string, long> { [_OAK_LOG_CODE] = 4 });

        ServiceResultBase result = await DebitAssetAsync(_OAK_LOG_CODE, whole: 4);

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
            [_SETTLEMENT_ID],
            new Dictionary<string, long> { [_OAK_LOG_CODE] = 4, [_OAK_PLANK_CODE] = 100 });

        ServiceResultBase result = await DebitGroupAsync(_GROUP_LOGS_CODE, whole: 10);

        Dictionary<string, long> balances = await ReadBalancesAsync(assetBankId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.Conflict));
            Assert.That(result.Errors.Single(), Does.Contain("4 available"), "only the group's own Assets count towards what is available");
            Assert.That(balances[_OAK_PLANK_CODE], Is.EqualTo(100));
        }
    }

    [Test, Description("Debiting a Derived Asset is a programming error rather than a shortfall: population is counted off what already records it and is not something a Player ran out of.")]
    public async Task ShouldRefuseADerivedAssetAsAProgrammingError()
    {
        await CreateBankAsync(_playerId, [_SETTLEMENT_ID], new Dictionary<string, long>());

        ServiceResultBase result = await DebitAssetAsync(_CIVILIANS_CODE, whole: 1);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.InternalServerError));
            Assert.That(result.Errors.Single(), Does.Contain("Civilians"));
        }
    }

    [Test, Description("A group whose every Asset is Derived has no balances to order, so naming it is the same mistake as naming a Derived Asset outright.")]
    public async Task ShouldRefuseAGroupWithNoBankableAssetAsAProgrammingError()
    {
        await CreateBankAsync(_playerId, [_SETTLEMENT_ID], new Dictionary<string, long>());

        ServiceResultBase result = await DebitGroupAsync(_GROUP_MY_PEOPLE_CODE, whole: 1);

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

        ServiceResultBase result = await DebitAssetAsync(_TECHNOLOGY_EXPERIENCE_CODE, whole: 4);

        (int directedPoints, _) = await ReadResearchPointsAsync(_playerId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));
            Assert.That(directedPoints, Is.EqualTo(6));
        }
    }

    [Test, Description("Diffusion points are not part of the spendable pool: they arrive attached to a neighbour rather than as a budget, so an expense cannot consume them.")]
    public async Task ShouldNotSpendDiffusionPoints()
    {
        await CreateResearchStateAsync(_playerId, unspentDirectedPoints: 4, unspentDiffusionPoints: 100);

        ServiceResultBase result = await DebitAssetAsync(_TECHNOLOGY_EXPERIENCE_CODE, whole: 10);

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

        ServiceResultBase result = await DebitAssetAsync(_CULTURE_EXPERIENCE_CODE, whole: 1);

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

        ServiceResultBase result = await DebitAsync(AssetExpense.OfAsset(
            playerId: _playerId,
            settlementId: _SETTLEMENT_ID,
            assetId: await GetAssetIdAsync(_TECHNOLOGY_EXPERIENCE_CODE),
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
        ServiceResultBase result = await DebitAssetAsync(_TECHNOLOGY_EXPERIENCE_CODE, whole: 1);

        Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.NotFound));
    }

    [TestCase(0, Description = "A zero expense is a caller that did not mean to call, so it reads as a programming error rather than as a success that changed nothing.")]
    [TestCase(-5, Description = "A negative expense is a credit by another name, and production rather than the drawdown owns crediting a balance.")]
    public async Task ShouldRefuseANonPositiveExpense(int whole)
    {
        int assetBankId = await CreateBankAsync(_playerId, [_SETTLEMENT_ID], new Dictionary<string, long> { [_OAK_LOG_CODE] = 10 });

        ServiceResultBase result = await DebitAssetAsync(_OAK_LOG_CODE, whole);

        Dictionary<string, long> balances = await ReadBalancesAsync(assetBankId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.InternalServerError));
            Assert.That(balances[_OAK_LOG_CODE], Is.EqualTo(10));
        }
    }

    [Test, Description("A Settlement with no asset bank has nothing to spend from, which is a not-found rather than a shortfall of zero.")]
    public async Task ShouldAnswerNotFoundWhenTheSettlementHasNoBank()
    {
        await CreateBankAsync(_playerId, [_SETTLEMENT_ID], new Dictionary<string, long> { [_OAK_LOG_CODE] = 10 });

        ServiceResultBase result = await DebitAsync(AssetExpense.OfAsset(
            playerId: _playerId,
            settlementId: _FAR_SETTLEMENT_ID,
            assetId: await GetAssetIdAsync(_OAK_LOG_CODE),
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
        int rivalBankId = await CreateBankAsync(_otherPlayerId, [_SETTLEMENT_ID], new Dictionary<string, long> { [_OAK_LOG_CODE] = 100 });

        ServiceResultBase result = await DebitAssetAsync(_OAK_LOG_CODE, whole: 10);

        Dictionary<string, long> balances = await ReadBalancesAsync(rivalBankId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.NotFound));
            Assert.That(balances[_OAK_LOG_CODE], Is.EqualTo(100));
        }
    }

    [Test, Description("An Asset that is not in the catalogue is a not-found, so a stale id reads as a missing row rather than as an empty balance.")]
    public async Task ShouldAnswerNotFoundForAnUnknownAsset()
    {
        await CreateBankAsync(_playerId, [_SETTLEMENT_ID], new Dictionary<string, long>());

        ServiceResultBase result = await DebitAsync(AssetExpense.OfAsset(
            playerId: _playerId,
            settlementId: _SETTLEMENT_ID,
            assetId: 999_999,
            amount: ScaledAmount.FromWhole(1)));

        Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.NotFound));
    }

    [Test, Description("An AssetGroup that is not in the catalogue is a not-found, reported against the group rather than silently resolving to no candidates.")]
    public async Task ShouldAnswerNotFoundForAnUnknownAssetGroup()
    {
        await CreateBankAsync(_playerId, [_SETTLEMENT_ID], new Dictionary<string, long>());

        ServiceResultBase result = await DebitAsync(AssetExpense.OfGroup(
            playerId: _playerId,
            settlementId: _SETTLEMENT_ID,
            assetGroupId: 999_999,
            amount: ScaledAmount.FromWhole(1)));

        Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.NotFound));
    }

    [Test, Description("The debit is staged on the caller's context and not saved, so a purchase that abandons its own save leaves the balance alone.")]
    public async Task ShouldNotCommitTheDebitWithoutTheCallersSave()
    {
        int assetBankId = await CreateBankAsync(_playerId, [_SETTLEMENT_ID], new Dictionary<string, long> { [_OAK_LOG_CODE] = 10 });

        await using (AscendantEmpireContext context = await DbFactory.CreateDbContextAsync())
        {
            ServiceResultBase result = await _service.DebitAsync(
                context,
                AssetExpense.OfAsset(
                    playerId: _playerId,
                    settlementId: _SETTLEMENT_ID,
                    assetId: await GetAssetIdAsync(_OAK_LOG_CODE),
                    amount: ScaledAmount.FromWhole(4)),
                CancellationToken.None);

            Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));
        }

        Dictionary<string, long> balances = await ReadBalancesAsync(assetBankId);

        Assert.That(balances[_OAK_LOG_CODE], Is.EqualTo(10));
    }

    private async Task<ServiceResultBase> DebitAssetAsync(string assetCode, int whole)
    {
        return await DebitAsync(AssetExpense.OfAsset(
            playerId: _playerId,
            settlementId: _SETTLEMENT_ID,
            assetId: await GetAssetIdAsync(assetCode),
            amount: ScaledAmount.FromWhole(whole)));
    }

    private async Task<ServiceResultBase> DebitGroupAsync(string groupCode, int whole)
    {
        return await DebitAsync(AssetExpense.OfGroup(
            playerId: _playerId,
            settlementId: _SETTLEMENT_ID,
            assetGroupId: await GetAssetGroupIdAsync(groupCode),
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
