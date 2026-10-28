using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Assets.Classes;
using AscendantEmpire.Application.Features.Assets.Services;
using AscendantEmpire.Domain.Entities.Assets;

using Microsoft.EntityFrameworkCore;

namespace AscendantEmpire.Tests.Features.Assets;

/// <summary>
/// Tests the read that puts a real figure behind every ticker: which store each Asset's scope sends it to, and what it reports from there.
/// </summary>
/// <remarks>
/// <para>
/// See <see cref="AssetBankTestBase"/> for the tier and the seeded fixture. The real <see cref="AssetBankReader"/> is used rather than a substitute, because
/// it has its own fixture and what is pinned here is the routing and the arithmetic over it, not the bank query again.
/// </para>
/// <para>
/// Every scope is taken off the seeded catalogue rather than stated in the test, so a seed that moves an Asset to a different store moves these with it.
/// </para>
/// </remarks>
[TestFixture]
internal class PlayerAssetQuantityReaderTests : AssetBankTestBase
{
    private const int _SETTLEMENT_ID = 41;
    private const int _FAR_SETTLEMENT_ID = 77;

    private PlayerAssetQuantityReader _reader = null!;

    [SetUp]
    public void CreateReader()
    {
        _reader = new PlayerAssetQuantityReader(new AssetBankReader());
    }

    [Test, Description("A banked Asset reports the total across every bank the Player owns, because that sum is what an Empire-wide expense is met from.")]
    public async Task ShouldSumABankedAssetAcrossEveryBank()
    {
        await CreateBankAsync(_playerId, [_SETTLEMENT_ID], new Dictionary<string, long> { [_OAK_LOG_CODE] = 4 });
        await CreateBankAsync(_playerId, [_FAR_SETTLEMENT_ID], new Dictionary<string, long> { [_OAK_LOG_CODE] = 6 });

        IReadOnlyDictionary<int, int> amounts = await ResolveAsync(_OAK_LOG_CODE);

        Assert.That(amounts[await GetAssetIdAsync(_OAK_LOG_CODE)], Is.EqualTo(10));
    }

    [Test, Description("Another Player's bank is not counted, so a figure can never include stock its reader has no claim on.")]
    public async Task ShouldNotCountAnotherPlayersBank()
    {
        await CreateBankAsync(_playerId, [_SETTLEMENT_ID], new Dictionary<string, long> { [_OAK_LOG_CODE] = 4 });
        await CreateBankAsync(_otherPlayerId, [_FAR_SETTLEMENT_ID], new Dictionary<string, long> { [_OAK_LOG_CODE] = 50 });

        IReadOnlyDictionary<int, int> amounts = await ResolveAsync(_OAK_LOG_CODE);

        Assert.That(amounts[await GetAssetIdAsync(_OAK_LOG_CODE)], Is.EqualTo(4));
    }

    [Test, Description("An Asset the bank has no row for is left out, because no row and a zero mean the same thing to every reader of a balance.")]
    public async Task ShouldLeaveOutAnAssetWithNoBalanceRow()
    {
        await CreateBankAsync(_playerId, [_SETTLEMENT_ID], new Dictionary<string, long> { [_OAK_LOG_CODE] = 4 });

        IReadOnlyDictionary<int, int> amounts = await ResolveAsync(_OAK_LOG_CODE, _CEDAR_LOG_CODE);

        Assert.That(amounts, Does.Not.ContainKey(await GetAssetIdAsync(_CEDAR_LOG_CODE)));
    }

    [Test, Description("A bank holding less than one whole unit reports none of it, which is also all a drawdown would release of it.")]
    public async Task ShouldReportNoneOfASubUnitBalance()
    {
        await CreateBankWithRawBalanceAsync(_playerId, _OAK_LOG_CODE, rawQuantity: 400);

        IReadOnlyDictionary<int, int> amounts = await ResolveAsync(_OAK_LOG_CODE);

        Assert.That(amounts, Does.Not.ContainKey(await GetAssetIdAsync(_OAK_LOG_CODE)));
    }

    [Test, Description("A fractional balance is rounded down rather than up, so a reported figure is never more than the Player can spend.")]
    public async Task ShouldFloorAFractionalBalance()
    {
        await CreateBankWithRawBalanceAsync(_playerId, _OAK_LOG_CODE, rawQuantity: 1_900);

        IReadOnlyDictionary<int, int> amounts = await ResolveAsync(_OAK_LOG_CODE);

        Assert.That(amounts[await GetAssetIdAsync(_OAK_LOG_CODE)], Is.EqualTo(1));
    }

    [Test, Description("A Player who owns no bank reports no banked figure rather than failing, because owning none is ordinary before the first settlement.")]
    public async Task ShouldReportNothingWhenThePlayerOwnsNoBank()
    {
        IReadOnlyDictionary<int, int> amounts = await ResolveAsync(_OAK_LOG_CODE);

        Assert.That(amounts, Is.Empty);
    }

    [Test, Description("Technology experience comes from the unspent directed research points, which is the store the research pipeline already writes.")]
    public async Task ShouldReadTechnologyExperienceFromTheUnspentDirectedPoints()
    {
        await CreateResearchStateAsync(_playerId, unspentDirectedPoints: 7, unspentDiffusionPoints: 30);

        IReadOnlyDictionary<int, int> amounts = await ResolveAsync(_TECHNOLOGY_EXPERIENCE_CODE);

        Assert.That(
            amounts[await GetAssetIdAsync(_TECHNOLOGY_EXPERIENCE_CODE)],
            Is.EqualTo(7),
            "diffusion points are not a budget the Player spends, so they are no part of this figure");
    }

    [Test, Description("A Player with no research state reports no Technology experience rather than failing, since that row is written at game setup.")]
    public async Task ShouldReportNothingWhenThePlayerHasNoResearchState()
    {
        IReadOnlyDictionary<int, int> amounts = await ResolveAsync(_TECHNOLOGY_EXPERIENCE_CODE);

        Assert.That(amounts, Is.Empty);
    }

    [Test, Description("Culture is left out although it is held per Player, because nothing stores culture points and a figure here would be invented.")]
    public async Task ShouldLeaveOutCultureExperience()
    {
        await CreateResearchStateAsync(_playerId, unspentDirectedPoints: 7);

        IReadOnlyDictionary<int, int> amounts = await ResolveAsync(_CULTURE_EXPERIENCE_CODE);

        Assert.That(amounts, Is.Empty);
    }

    [Test, Description("A Derived Asset is left out, because the holdings ledger and roster reads its figure is to come from do not exist yet.")]
    public async Task ShouldLeaveOutADerivedAsset()
    {
        await CreateBankAsync(_playerId, [_SETTLEMENT_ID], new Dictionary<string, long> { [_OAK_LOG_CODE] = 4 });

        IReadOnlyDictionary<int, int> amounts = await ResolveAsync(_CIVILIANS_CODE);

        Assert.That(amounts, Is.Empty);
    }

    [Test, Description("All three scopes are answered in one call, so a read path needs no knowledge of which store answers for which Asset.")]
    public async Task ShouldResolveEveryScopeInOneCall()
    {
        await CreateBankAsync(_playerId, [_SETTLEMENT_ID], new Dictionary<string, long> { [_OAK_LOG_CODE] = 4 });
        await CreateResearchStateAsync(_playerId, unspentDirectedPoints: 7);

        IReadOnlyDictionary<int, int> amounts = await ResolveAsync(_OAK_LOG_CODE, _TECHNOLOGY_EXPERIENCE_CODE, _CIVILIANS_CODE);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(amounts[await GetAssetIdAsync(_OAK_LOG_CODE)], Is.EqualTo(4));
            Assert.That(amounts[await GetAssetIdAsync(_TECHNOLOGY_EXPERIENCE_CODE)], Is.EqualTo(7));
            Assert.That(amounts, Does.Not.ContainKey(await GetAssetIdAsync(_CIVILIANS_CODE)));
        }
    }

    /// <summary>
    /// Resolves the amounts for the named Assets, as the read path does for the Player whose tree it is building.
    /// </summary>
    private async Task<IReadOnlyDictionary<int, int>> ResolveAsync(params string[] assetCodes)
    {
        IReadOnlyCollection<AssetQuantityRequest> requests = await BuildRequestsAsync(assetCodes);

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        return await _reader.GetWholeAmountsByAssetIdAsync(context, _playerId, requests);
    }

    /// <summary>
    /// Builds the requests for the named Assets, taking each one's storage scope off the seeded catalogue.
    /// </summary>
    private async Task<IReadOnlyCollection<AssetQuantityRequest>> BuildRequestsAsync(IReadOnlyCollection<string> assetCodes)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        List<AssetQuantityRequest> requests = await context.Assets
            .AsNoTracking()
            .Where(asset => assetCodes.Contains(asset.Code))
            .Select(asset => new AssetQuantityRequest(asset.Id, asset.Code, asset.StorageScope))
            .ToListAsync();

        Assert.That(
            requests,
            Has.Count.EqualTo(assetCodes.Count),
            () => $"the seeded catalogue has no Asset for one of: {string.Join(", ", assetCodes)}");

        return requests;
    }

    /// <summary>
    /// Creates a bank holding one Asset at an exact scaled quantity, for the cases that turn on sub-unit stock.
    /// </summary>
    /// <remarks>
    /// <see cref="AssetBankTestBase.CreateBankAsync"/> takes whole units, which is what almost every test wants to read; this is the way past it.
    /// </remarks>
    private async Task CreateBankWithRawBalanceAsync(int playerId, string assetCode, long rawQuantity)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        var bank = new AssetBank { PlayerId = playerId };
        context.AssetBanks.Add(bank);
        await context.SaveChangesAsync();

        context.AssetBankBalances.Add(new AssetBankBalance
        {
            AssetBankId = bank.Id,
            AssetId = await GetAssetIdAsync(assetCode),
            Quantity = rawQuantity
        });

        await context.SaveChangesAsync();
    }
}
