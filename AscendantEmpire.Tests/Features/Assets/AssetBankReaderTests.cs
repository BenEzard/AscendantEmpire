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

    [Test, Description("The settlement read hands back every bank sharing the site's NetworkKey, since that is what the Settlement and everything connected to it can spend.")]
    public async Task ShouldReturnTheWholeNetworkForOneSettlement()
    {
        Guid networkKey = Guid.NewGuid();

        int siteBankId = await CreateBankAsync(_playerId, _SETTLEMENT_ID, new Dictionary<string, long> { [_OAK_LOG_CODE] = 4 }, networkKey);
        int connectedBankId = await CreateBankAsync(_playerId, _CONNECTED_SETTLEMENT_ID, new Dictionary<string, long> { [_OAK_LOG_CODE] = 6 }, networkKey);

        AssetNetworkHoldings network = await ReadNetworkAsync(_SETTLEMENT_ID);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(network.NetworkKey, Is.EqualTo(networkKey));
            Assert.That(network.PlayerId, Is.EqualTo(_playerId));
            Assert.That(network.SiteAssetBankId, Is.EqualTo(siteBankId), "The bank of the Settlement that was asked about, which a local-first spend drains first.");
            Assert.That(network.Banks.Select(bank => bank.AssetBankId), Is.EqualTo(new[] { siteBankId, connectedBankId }));
            Assert.That(network.Banks.Select(bank => bank.SettlementId), Is.EqualTo(new[] { _SETTLEMENT_ID, _CONNECTED_SETTLEMENT_ID }));
        }
    }

    [Test, Description("A one-bank read hands back the bank's balances together with the Settlement whose store it is, since a quantity alone says nothing about where the stock physically sits.")]
    public async Task ShouldReturnTheBanksHoldingsAndSettlementForOneSettlement()
    {
        int assetBankId = await CreateBankAsync(
            _playerId,
            _SETTLEMENT_ID,
            new Dictionary<string, long> { [_OAK_LOG_CODE] = 4 });

        AssetBankHoldings bank = await ReadForSettlementAsync(_SETTLEMENT_ID);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(bank.AssetBankId, Is.EqualTo(assetBankId));
            Assert.That(bank.PlayerId, Is.EqualTo(_playerId));
            Assert.That(bank.SettlementId, Is.EqualTo(_SETTLEMENT_ID));
            Assert.That(bank.Holdings.Single().AssetCode, Is.EqualTo(_OAK_LOG_CODE));
            Assert.That(bank.Holdings.Single().RawQuantity, Is.EqualTo(ScaledAmount.FromWhole(4).RawValue));
        }
    }

    [Test, Description("An unconnected Settlement comes back as a network of one, so a caller has one shape to read whether or not the Settlement is joined to anything.")]
    public async Task ShouldReturnANetworkOfOneForAnUnconnectedSettlement()
    {
        int siteBankId = await CreateBankAsync(_playerId, _SETTLEMENT_ID, new Dictionary<string, long> { [_OAK_LOG_CODE] = 4 });
        await CreateBankAsync(_playerId, _FAR_SETTLEMENT_ID, new Dictionary<string, long> { [_OAK_LOG_CODE] = 999 });

        AssetNetworkHoldings network = await ReadNetworkAsync(_SETTLEMENT_ID);

        Assert.That(network.Banks.Select(bank => bank.AssetBankId), Is.EqualTo(new[] { siteBankId }));
    }

    [Test, Description("Two unkeyed banks are never grouped, because Guid.Empty is the absence of a key rather than a network they share - grouping on it would hand one Settlement every bank nothing had keyed yet.")]
    public async Task ShouldNotGroupTwoUnkeyedBanksTogether()
    {
        int siteBankId = await CreateBankAsync(_playerId, _SETTLEMENT_ID, new Dictionary<string, long> { [_OAK_LOG_CODE] = 4 }, Guid.Empty);
        await CreateBankAsync(_playerId, _FAR_SETTLEMENT_ID, new Dictionary<string, long> { [_OAK_LOG_CODE] = 999 }, Guid.Empty);

        AssetNetworkHoldings network = await ReadNetworkAsync(_SETTLEMENT_ID);

        Assert.That(network.Banks.Select(bank => bank.AssetBankId), Is.EqualTo(new[] { siteBankId }));
    }

    [Test, Description("Another Player's bank carrying the same NetworkKey is not in the network, since connectivity is assessed per Player and the key alone is not unique across them.")]
    public async Task ShouldNotReachAnotherPlayersBankWithTheSameKey()
    {
        Guid sharedKey = Guid.NewGuid();

        int siteBankId = await CreateBankAsync(_playerId, _SETTLEMENT_ID, new Dictionary<string, long> { [_OAK_LOG_CODE] = 4 }, sharedKey);
        await CreateBankAsync(_otherPlayerId, _FAR_SETTLEMENT_ID, new Dictionary<string, long> { [_OAK_LOG_CODE] = 999 }, sharedKey);

        AssetNetworkHoldings network = await ReadNetworkAsync(_SETTLEMENT_ID);

        Assert.That(network.Banks.Select(bank => bank.AssetBankId), Is.EqualTo(new[] { siteBankId }));
    }

    [Test, Description("Quantities come back raw rather than rounded, so a sub-unit holding cannot read as empty to one caller and as stock to another.")]
    public async Task ShouldReturnTheScaledQuantityUnrounded()
    {
        int assetBankId = await CreateBankAsync(_playerId, _SETTLEMENT_ID, new Dictionary<string, long>());

        await SetRawBalanceAsync(assetBankId, _OAK_LOG_CODE, ScaledAmount.FromDecimal(0.4m).RawValue);

        AssetBankHoldings bank = await ReadForSettlementAsync(_SETTLEMENT_ID);

        Assert.That(bank.Holdings.Single().RawQuantity, Is.EqualTo(400));
    }

    [Test, Description("A Settlement with no bank is a not-found rather than an empty network that a caller might try to spend from.")]
    public async Task ShouldAnswerNotFoundWhenTheSettlementHasNoBank()
    {
        await CreateBankAsync(_playerId, _SETTLEMENT_ID, new Dictionary<string, long> { [_OAK_LOG_CODE] = 4 });

        ServiceResult<AssetNetworkHoldings> result = await SendForSettlementAsync(_FAR_SETTLEMENT_ID);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.NotFound));
            Assert.That(result.Errors.Single(), Does.Contain($"Settlement {_FAR_SETTLEMENT_ID}"));
        }
    }

    [Test, Description("A Settlement whose bank belongs to another Player reads as having none, so asking after someone else's holdings cannot confirm that they exist.")]
    public async Task ShouldAnswerNotFoundWhenTheSettlementsBankBelongsToAnotherPlayer()
    {
        await CreateBankAsync(_otherPlayerId, _SETTLEMENT_ID, new Dictionary<string, long> { [_OAK_LOG_CODE] = 4 });

        ServiceResult<AssetNetworkHoldings> result = await SendForSettlementAsync(_SETTLEMENT_ID);

        Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.NotFound));
    }

    [Test, Description("\"No bank\" and \"another Player's bank\" give the identical message, so a caller cannot tell the two apart and learn that a Settlement they do not own exists.")]
    public async Task ShouldGiveTheSameNotFoundMessageForNoBankAndForAnotherPlayersBank()
    {
        await CreateBankAsync(_otherPlayerId, _SETTLEMENT_ID, new Dictionary<string, long> { [_OAK_LOG_CODE] = 4 });

        ServiceResult<AssetNetworkHoldings> rivalsSettlement = await SendForSettlementAsync(_SETTLEMENT_ID);
        ServiceResult<AssetNetworkHoldings> unbankedSettlement = await SendForSettlementAsync(_FAR_SETTLEMENT_ID);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(rivalsSettlement.Errors.Single(), Does.Contain($"Settlement {_SETTLEMENT_ID}"));
            Assert.That(unbankedSettlement.Errors.Single(), Does.Contain($"Settlement {_FAR_SETTLEMENT_ID}"));
            Assert.That(
                rivalsSettlement.Errors.Single().Replace($"{_SETTLEMENT_ID}", "<id>"),
                Is.EqualTo(unbankedSettlement.Errors.Single().Replace($"{_FAR_SETTLEMENT_ID}", "<id>")),
                "Identical but for the Settlement id the caller already supplied.");
        }
    }

    [Test, Description("The all-banks read keeps each bank separate rather than merging totals, since a bank is one Settlement's store and two of them may not reach each other.")]
    public async Task ShouldReturnEachOfThePlayersBanksSeparately()
    {
        int firstBankId = await CreateBankAsync(_playerId, _SETTLEMENT_ID, new Dictionary<string, long> { [_OAK_LOG_CODE] = 4 });
        int secondBankId = await CreateBankAsync(_playerId, _FAR_SETTLEMENT_ID, new Dictionary<string, long> { [_OAK_LOG_CODE] = 6 });

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
        int ownBankId = await CreateBankAsync(_playerId, _SETTLEMENT_ID, new Dictionary<string, long> { [_OAK_LOG_CODE] = 4 });
        await CreateBankAsync(_otherPlayerId, _FAR_SETTLEMENT_ID, new Dictionary<string, long> { [_OAK_LOG_CODE] = 999 });

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

    [Test, Description("Holdings come back in the catalogue's display order rather than insertion order, so a panel and a group drawdown walk the same Assets in the same sequence.")]
    public async Task ShouldReturnHoldingsInDisplayOrder()
    {
        // Credited back to front. Seeded sort orders space by 10 in declaration order, which is Oak, Generic, Cedar.
        await CreateBankAsync(
            _playerId,
            _SETTLEMENT_ID,
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
        await CreateBankAsync(_playerId, _SETTLEMENT_ID, new Dictionary<string, long> { [_OAK_LOG_CODE] = 4 });

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

    /// <summary>The site's own bank out of the network read, for the cases that are about one bank's contents rather than about the network.</summary>
    private async Task<AssetBankHoldings> ReadForSettlementAsync(int settlementId)
    {
        AssetNetworkHoldings network = await ReadNetworkAsync(settlementId);

        return network.Banks.Single(bank => bank.AssetBankId == network.SiteAssetBankId);
    }

    private async Task<AssetNetworkHoldings> ReadNetworkAsync(int settlementId)
    {
        ServiceResult<AssetNetworkHoldings> result = await SendForSettlementAsync(settlementId);

        Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));

        return result.SuccessValue!;
    }

    private async Task<ServiceResult<AssetNetworkHoldings>> SendForSettlementAsync(int settlementId)
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
