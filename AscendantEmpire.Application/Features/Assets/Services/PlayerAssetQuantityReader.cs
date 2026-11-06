using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Assets.Classes;
using AscendantEmpire.Application.Helpers.ScaledNumerics;
using AscendantEmpire.BackendContracts.Assets.GetAssetBanks;
using AscendantEmpire.Domain.Enums.Assets;

using Microsoft.EntityFrameworkCore;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.Assets.Services;

/// <inheritdoc cref="IPlayerAssetQuantityReader" />
internal sealed class PlayerAssetQuantityReader(IAssetBankReader bankReader) : IPlayerAssetQuantityReader
{
    public async Task<IReadOnlyDictionary<int, int>> GetWholeAmountsByAssetIdAsync(
        AscendantEmpireContext context,
        int playerId,
        IReadOnlyCollection<AssetQuantityRequest> assets,
        CancellationToken cancelToken = default)
    {
        Dictionary<int, int> amountsByAssetId = [];

        if (assets.Count == 0)
        {
            return amountsByAssetId;
        }

        await AddBankedAmountsAsync(context, playerId, assets, amountsByAssetId, cancelToken);
        await AddPlayerStoreAmountsAsync(context, playerId, assets, amountsByAssetId, cancelToken);

        // Derived Assets are resolved by nobody yet, so they are absent and read as none. Civilians are to come from Settlement.Population, the three
        // services from the force rosters, and Slaves / Interned / P.o.W. from the holdings ledger RS-20 specified and did not build.

        return amountsByAssetId;
    }

    public async Task<IReadOnlySet<int>> GetAssetIdsHeldInReserveAsync(
        AscendantEmpireContext context,
        int playerId,
        CancellationToken cancelToken = default)
    {
        ServiceResult<IReadOnlyCollection<AssetBankHoldings>> banksResult = await bankReader.GetForPlayerAsync(context, playerId, cancelToken);

        // Read through the bank reader for the same reason the amounts are, so what a Settlement's panel shows and what the ticker flags are the same rows.
        return new HashSet<int>(
            (banksResult.SuccessValue ?? [])
                .SelectMany(bank => bank.Holdings)
                .Where(holding => holding.RawReserved > 0)
                .Select(holding => holding.AssetId));
    }

    /// <summary>
    /// Adds the figure for every requested Asset held in the Player's banks, summed across all of them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Summed deliberately, and the sum is spendable.</b> An Empire-wide expense is met from everything the Player holds anywhere - two banks that cannot
    /// reach each other are added together, because nothing is travelling to a building site - so the total is the figure a purchase can actually be made
    /// against. See the Empire-wide section of <c>docs/design/asset-banks.md</c>, and <c>AssetBankDrawdownService</c>, which settles availability the same way.
    /// </para>
    /// <para>
    /// Read through <see cref="IAssetBankReader"/> rather than off <c>AssetBankBalances</c> directly, because that is the one place a bank's balances are
    /// read: what a settlement's panel shows and what this ticker adds up are then the same rows assembled the same way. The cost is that it also hands back
    /// memberships this caller discards, and balances for Assets nobody asked about.
    /// </para>
    /// </remarks>
    private async Task AddBankedAmountsAsync(
        AscendantEmpireContext context,
        int playerId,
        IReadOnlyCollection<AssetQuantityRequest> assets,
        Dictionary<int, int> amountsByAssetId,
        CancellationToken cancelToken)
    {
        HashSet<int> bankedAssetIds =
        [
            .. assets
                .Where(asset => asset.StorageScope == AssetStorageScope.SettlementBank)
                .Select(asset => asset.AssetId)
        ];

        if (bankedAssetIds.Count == 0)
        {
            return;
        }

        ServiceResult<IReadOnlyCollection<AssetBankHoldings>> banksResult = await bankReader.GetForPlayerAsync(context, playerId, cancelToken);

        // A Player with no settlement owns no banks, which the reader answers as an empty list rather than as a failure, so there is no refusal to pass on here.
        IEnumerable<AssetBankHolding> holdings = (banksResult.SuccessValue ?? [])
            .SelectMany(bank => bank.Holdings)
            .Where(holding => bankedAssetIds.Contains(holding.AssetId));

        foreach (IGrouping<int, AssetBankHolding> assetHoldings in holdings.GroupBy(holding => holding.AssetId))
        {
            long rawTotal = assetHoldings.Sum(holding => holding.RawQuantity);
            int wholeTotal = ToWholeAmount(rawTotal);

            // A whole zero is left out rather than written in: an Asset the Player effectively holds none of should read the same whether its balance rows
            // are missing or hold less than one unit between them.
            if (wholeTotal > 0)
            {
                amountsByAssetId[assetHoldings.Key] = wholeTotal;
            }
        }
    }

    /// <summary>
    /// Adds the figure for every requested Asset held once per Player, outside any bank.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Only the codes in <see cref="PlayerStoreAssetCodes"/> have a store. Culture is the other <c>Player</c>-scope Asset in the catalogue and is left
    /// absent, so its ticker reads zero rather than a figure invented here - the same gap the drawdown refuses a culture expense over.
    /// </para>
    /// <para>
    /// <c>UnspentDirectedPoints</c> is a whole unscaled count, so nothing is converted. Diffusion points are deliberately not added in: they arrive attached
    /// to a trade partner rather than being a budget the Player spends, which is why the drawdown will not spend them either.
    /// </para>
    /// </remarks>
    private static async Task AddPlayerStoreAmountsAsync(
        AscendantEmpireContext context,
        int playerId,
        IReadOnlyCollection<AssetQuantityRequest> assets,
        Dictionary<int, int> amountsByAssetId,
        CancellationToken cancelToken)
    {
        AssetQuantityRequest? technologyExperience = assets
            .Where(asset => asset.StorageScope == AssetStorageScope.Player)
            .FirstOrDefault(asset => string.Equals(asset.Code, PlayerStoreAssetCodes.TECHNOLOGY_EXPERIENCE, StringComparison.OrdinalIgnoreCase));

        if (technologyExperience is null)
        {
            return;
        }

        // One row per Player, written at game setup, so its absence means the Player was never set up rather than that they have earned nothing.
        int unspentDirectedPoints = await context.PlayerResearchStates
            .AsNoTracking()
            .Where(state => state.PlayerId == playerId)
            .Select(state => state.UnspentDirectedPoints)
            .FirstOrDefaultAsync(cancelToken);

        if (unspentDirectedPoints > 0)
        {
            amountsByAssetId[technologyExperience.AssetId] = unspentDirectedPoints;
        }
    }

    /// <summary>
    /// Turns a scaled total into the whole number the UI and the API speak.
    /// </summary>
    /// <remarks>
    /// Rounded down, so a Player holding nine tenths of a log reads as none of it - which is also all a drawdown would let them spend of it. Clamped rather
    /// than cast, because a sum across every bank is a <c>long</c> and the contract field is an <c>int</c>.
    /// </remarks>
    private static int ToWholeAmount(long rawTotal)
    {
        long wholeTotal = ScaledAmount.FromRaw(rawTotal).ToWholeRoundedDown();

        return (int)Math.Clamp(wholeTotal, int.MinValue, int.MaxValue);
    }
}
