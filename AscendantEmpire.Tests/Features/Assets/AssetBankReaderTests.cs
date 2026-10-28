using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Assets.Services;
using AscendantEmpire.Application.Helpers.ScaledNumerics;
using AscendantEmpire.BackendContracts.Assets.GetAssetBanks;
using AscendantEmpire.Domain.Entities.Assets;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Tests.Features.Assets;

/// <summary>
/// Tests the one place a bank's balances are read, in both of the shapes the design doc calls for.
/// </summary>
/// <remarks>See <see cref="AssetBankTestBase"/> for the tier and the seeded fixture.</remarks>
[TestFixture]
internal class AssetBankReaderTests : AssetBankTestBase
{
    private const int _SETTLEMENT_ID = 41;
    private const int _CONNECTED_SETTLEMENT_ID = 42;
    private const int _FAR_SETTLEMENT_ID = 77;

    private AssetBankReader _reader = null!;

    [SetUp]
    public void CreateReader()
    {
        _reader = new AssetBankReader();
    }

    [Test, Description("A one-bank read hands back the bank's balances together with every Settlement that draws on it, since a quantity alone could be added to a pool it cannot reach.")]
    public async Task ShouldReturnTheBanksHoldingsAndMembershipsForOneSettlement()
    {
        int assetBankId = await CreateBankAsync(
            _playerId,
            [_SETTLEMENT_ID, _CONNECTED_SETTLEMENT_ID],
            new Dictionary<string, long> { [_OAK_LOG_CODE] = 4 });

        AssetBankHoldings bank = await ReadForSettlementAsync(_SETTLEMENT_ID);

        Assert.Multiple(() =>
        {
            Assert.That(bank.AssetBankId, Is.EqualTo(assetBankId));
            Assert.That(bank.PlayerId, Is.EqualTo(_playerId));
            Assert.That(bank.SettlementIds, Is.EqualTo(new[] { _SETTLEMENT_ID, _CONNECTED_SETTLEMENT_ID }));
            Assert.That(bank.Holdings.Single().AssetCode, Is.EqualTo(_OAK_LOG_CODE));
            Assert.That(bank.Holdings.Single().RawQuantity, Is.EqualTo(ScaledAmount.FromWhole(4).RawValue));
        });
    }

    [Test, Description("Quantities come back raw rather than rounded, so a sub-unit holding cannot read as empty to one caller and as stock to another.")]
    public async Task ShouldReturnTheScaledQuantityUnrounded()
    {
        int assetBankId = await CreateBankAsync(_playerId, [_SETTLEMENT_ID], new Dictionary<string, long>());

        await SetRawBalanceAsync(assetBankId, _OAK_LOG_CODE, ScaledAmount.FromDecimal(0.4m).RawValue);

        AssetBankHoldings bank = await ReadForSettlementAsync(_SETTLEMENT_ID);

        Assert.That(bank.Holdings.Single().RawQuantity, Is.EqualTo(400));
    }

    [Test, Description("A Settlement with no membership row has no bank, which is a not-found rather than an empty bank that a caller might try to spend from.")]
    public async Task ShouldAnswerNotFoundWhenTheSettlementHasNoMembership()
    {
        await CreateBankAsync(_playerId, [_SETTLEMENT_ID], new Dictionary<string, long> { [_OAK_LOG_CODE] = 4 });

        ServiceResult<AssetBankHoldings> result = await SendForSettlementAsync(_FAR_SETTLEMENT_ID);

        Assert.Multiple(() =>
        {
            Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.NotFound));
            Assert.That(result.Errors.Single(), Does.Contain($"Settlement {_FAR_SETTLEMENT_ID}"));
        });
    }

    [Test, Description("A Settlement whose bank belongs to another Player reads as having none, so asking after someone else's holdings cannot confirm that they exist.")]
    public async Task ShouldAnswerNotFoundWhenTheSettlementsBankBelongsToAnotherPlayer()
    {
        await CreateBankAsync(_otherPlayerId, [_SETTLEMENT_ID], new Dictionary<string, long> { [_OAK_LOG_CODE] = 4 });

        ServiceResult<AssetBankHoldings> result = await SendForSettlementAsync(_SETTLEMENT_ID);

        Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.NotFound));
    }

    [Test, Description("The all-banks read keeps each pool separate rather than merging totals, because two banks exist precisely because their Settlements cannot reach each other.")]
    public async Task ShouldReturnEachOfThePlayersBanksSeparately()
    {
        int firstBankId = await CreateBankAsync(_playerId, [_SETTLEMENT_ID], new Dictionary<string, long> { [_OAK_LOG_CODE] = 4 });
        int secondBankId = await CreateBankAsync(_playerId, [_FAR_SETTLEMENT_ID], new Dictionary<string, long> { [_OAK_LOG_CODE] = 6 });

        IReadOnlyCollection<AssetBankHoldings> banks = await ReadForPlayerAsync(_playerId);

        Assert.Multiple(() =>
        {
            Assert.That(banks.Select(bank => bank.AssetBankId), Is.EqualTo(new[] { firstBankId, secondBankId }));
            Assert.That(banks.Select(bank => bank.Holdings.Single().RawQuantity), Is.EqualTo(new[]
            {
                ScaledAmount.FromWhole(4).RawValue,
                ScaledAmount.FromWhole(6).RawValue
            }));
        });
    }

    [Test, Description("The all-banks read never returns another Player's bank, which is what stops a summary ticker counting a rival's stock.")]
    public async Task ShouldNotReturnAnotherPlayersBank()
    {
        int ownBankId = await CreateBankAsync(_playerId, [_SETTLEMENT_ID], new Dictionary<string, long> { [_OAK_LOG_CODE] = 4 });
        await CreateBankAsync(_otherPlayerId, [_FAR_SETTLEMENT_ID], new Dictionary<string, long> { [_OAK_LOG_CODE] = 999 });

        IReadOnlyCollection<AssetBankHoldings> banks = await ReadForPlayerAsync(_playerId);

        Assert.That(banks.Single().AssetBankId, Is.EqualTo(ownBankId));
    }

    [Test, Description("A Player who has founded no settlement owns no banks, and that is an empty list rather than a not-found: nothing is missing.")]
    public async Task ShouldReturnAnEmptyListForAPlayerWithNoBanks()
    {
        ServiceResult<IReadOnlyCollection<AssetBankHoldings>> result = await SendForPlayerAsync(_playerId);

        Assert.Multiple(() =>
        {
            Assert.That(result.HasSucceeded, Is.True);
            Assert.That(result.SuccessValue, Is.Empty);
        });
    }

    [Test, Description("A bank whose last membership has been repointed still comes back with its balances, because an orphaned bank holds stock until it is folded away.")]
    public async Task ShouldReturnABankThatHasNoMemberships()
    {
        int assetBankId = await CreateBankAsync(_playerId, [], new Dictionary<string, long> { [_OAK_LOG_CODE] = 4 });

        AssetBankHoldings bank = (await ReadForPlayerAsync(_playerId)).Single();

        Assert.Multiple(() =>
        {
            Assert.That(bank.AssetBankId, Is.EqualTo(assetBankId));
            Assert.That(bank.SettlementIds, Is.Empty);
            Assert.That(bank.Holdings, Is.Not.Empty);
        });
    }

    [Test, Description("Holdings come back in the catalogue's display order rather than insertion order, so a panel and a group drawdown walk the same Assets in the same sequence.")]
    public async Task ShouldReturnHoldingsInDisplayOrder()
    {
        // Credited back to front. Seeded sort orders space by 10 in declaration order, which is Oak, Generic, Cedar.
        await CreateBankAsync(
            _playerId,
            [_SETTLEMENT_ID],
            new Dictionary<string, long>
            {
                [_CEDAR_LOG_CODE] = 1,
                [_GENERIC_LOG_CODE] = 2,
                [_OAK_LOG_CODE] = 3
            });

        AssetBankHoldings bank = await ReadForSettlementAsync(_SETTLEMENT_ID);

        Assert.That(bank.Holdings.Select(holding => holding.AssetCode), Is.EqualTo(new[] { _OAK_LOG_CODE, _GENERIC_LOG_CODE, _CEDAR_LOG_CODE }));
    }

    [Test, Description("Each holding carries the group its Asset belongs to, so a caller weighing a group expense can see which rows it would walk without a second catalogue read.")]
    public async Task ShouldCarryTheAssetsGroupOnEachHolding()
    {
        await CreateBankAsync(_playerId, [_SETTLEMENT_ID], new Dictionary<string, long> { [_OAK_LOG_CODE] = 4 });

        int logsGroupId = await GetAssetGroupIdAsync(_GROUP_LOGS_CODE);

        AssetBankHoldings bank = await ReadForSettlementAsync(_SETTLEMENT_ID);

        Assert.That(bank.Holdings.Single().AssetGroupId, Is.EqualTo(logsGroupId));
    }

    /// <summary>
    /// Overwrites one balance with a raw scaled figure, for the cases a whole number cannot express.
    /// </summary>
    private async Task SetRawBalanceAsync(int assetBankId, string assetCode, long rawQuantity)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        int assetId = await GetAssetIdAsync(assetCode);

        context.AssetBankBalances.Add(new AssetBankBalance
        {
            AssetBankId = assetBankId,
            AssetId = assetId,
            Quantity = rawQuantity
        });

        await context.SaveChangesAsync();
    }

    private async Task<AssetBankHoldings> ReadForSettlementAsync(int settlementId)
    {
        ServiceResult<AssetBankHoldings> result = await SendForSettlementAsync(settlementId);

        Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));

        return result.SuccessValue!;
    }

    private async Task<ServiceResult<AssetBankHoldings>> SendForSettlementAsync(int settlementId)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        return await _reader.GetForSettlementAsync(context, _playerId, settlementId, CancellationToken.None);
    }

    private async Task<IReadOnlyCollection<AssetBankHoldings>> ReadForPlayerAsync(int playerId)
    {
        ServiceResult<IReadOnlyCollection<AssetBankHoldings>> result = await SendForPlayerAsync(playerId);

        Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));

        return result.SuccessValue!;
    }

    private async Task<ServiceResult<IReadOnlyCollection<AssetBankHoldings>>> SendForPlayerAsync(int playerId)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        return await _reader.GetForPlayerAsync(context, playerId, CancellationToken.None);
    }
}
