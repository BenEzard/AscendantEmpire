using AscendantEmpire.Application.Data;
using AscendantEmpire.BackendContracts.Assets.GetAssetBanks;
using AscendantEmpire.Domain.Entities.Assets;
using AscendantEmpire.Domain.Enums.Assets;

using Microsoft.EntityFrameworkCore;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.Assets.Services;

/// <inheritdoc cref="IAssetBankReader" />
internal sealed class AssetBankReader : IAssetBankReader
{
    public async Task<ServiceResult<AssetNetworkHoldings>> GetForSettlementAsync(
        AscendantEmpireContext context,
        int playerId,
        int settlementId,
        CancellationToken cancelToken = default)
    {
        // Both columns in the predicate, so naming another Player's Settlement cannot reach their bank. That is what makes "not yours" and "no bank" one
        // answer rather than two.
        SiteRow? site = await context.AssetBanks
            .AsNoTracking()
            .Where(bank => bank.SettlementId == settlementId && bank.PlayerId == playerId)
            .Select(bank => new SiteRow(bank.Id, bank.NetworkKey))
            .FirstOrDefaultAsync(cancelToken);

        if (site is null)
        {
            return ServiceResult<AssetNetworkHoldings>.NotFound($"Settlement {settlementId} has no asset bank belonging to Player {playerId}.");
        }

        // Guid.Empty is the absence of a key, not a network two banks share, so an unkeyed bank reaches nothing but itself. Grouping on it would hand one
        // Settlement every other bank of the Player's that had not been keyed yet.
        int[] networkBankIds = site.NetworkKey == Guid.Empty
            ? [site.AssetBankId]
            : [.. await context.AssetBanks
                .AsNoTracking()
                .Where(bank => bank.PlayerId == playerId && bank.NetworkKey == site.NetworkKey)
                .Select(bank => bank.Id)
                .ToListAsync(cancelToken)];

        IReadOnlyCollection<AssetBankHoldings> banks = await ReadBanksAsync(context, playerId, networkBankIds, cancelToken);

        // The site read above proved the bank exists and is this Player's, so it is always among them. A miss here would mean the row vanished between the
        // two reads, which is a failure rather than an empty result.
        if (!banks.Any(bank => bank.AssetBankId == site.AssetBankId))
        {
            return ServiceResult<AssetNetworkHoldings>.NotFound($"Asset bank {site.AssetBankId}, which Settlement {settlementId} draws on, could not be read.");
        }

        return ServiceResult<AssetNetworkHoldings>.Succeed(new AssetNetworkHoldings
        {
            NetworkKey = site.NetworkKey,
            PlayerId = playerId,
            SiteAssetBankId = site.AssetBankId,
            Banks = banks
        });
    }

    public async Task<ServiceResult<IReadOnlyCollection<AssetBankHoldings>>> GetForPlayerAsync(
        AscendantEmpireContext context,
        int playerId,
        CancellationToken cancelToken = default)
    {
        IReadOnlyCollection<AssetBankHoldings> banks = await ReadBanksAsync(context, playerId, assetBankIds: null, cancelToken);

        // A Player with no settlement yet owns no banks. That is an empty list, not a missing resource.
        return ServiceResult<IReadOnlyCollection<AssetBankHoldings>>.Succeed(banks);
    }

    /// <summary>
    /// Reads the Player's banks, optionally narrowed to a set, and assembles them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Two flat reads joined in memory rather than one query with a nested collection projection, which is the shape <c>GetPlayerAssetsHandler</c> already
    /// uses for the asset tree. A Player holds a handful of banks and each holds at most a few hundred balances, so the assembly is cheap.
    /// </para>
    /// <para>The Player filter is applied to the banks themselves, so narrowing by id cannot reach another Player's bank even when the id is theirs.</para>
    /// </remarks>
    /// <param name="assetBankIds">The banks to read, or <see langword="null"/> for every bank the Player owns.</param>
    private static async Task<IReadOnlyCollection<AssetBankHoldings>> ReadBanksAsync(
        AscendantEmpireContext context,
        int playerId,
        IReadOnlyCollection<int>? assetBankIds,
        CancellationToken cancelToken)
    {
        IQueryable<AssetBank> banks = context.AssetBanks
            .AsNoTracking()
            .Where(bank => bank.PlayerId == playerId);

        if (assetBankIds is not null)
        {
            banks = banks.Where(bank => assetBankIds.Contains(bank.Id));
        }

        List<BankRow> bankRows = await banks
            .Select(bank => new BankRow(bank.Id, bank.PlayerId, bank.SettlementId, bank.NetworkKey))
            .ToListAsync(cancelToken);

        if (bankRows.Count == 0)
        {
            return [];
        }

        int[] bankIds = [.. bankRows.Select(bank => bank.AssetBankId)];

        // Projected through the Asset navigation, which EF expands into the join. The catalogue columns come back on the balance row rather than from a
        // second read, so a holding can never be reported under a name belonging to a different Asset.
        List<HoldingRow> holdingRows = await context.AssetBankBalances
            .AsNoTracking()
            .Where(balance => bankIds.Contains(balance.AssetBankId))
            .Select(balance => new HoldingRow(
                balance.AssetBankId,
                balance.AssetId,
                balance.Asset.Code,
                balance.Asset.DisplayName,
                balance.Asset.AssetGroupId,
                balance.Asset.SortOrder,
                balance.Asset.AssetGrade,
                balance.Quantity,
                balance.ReservedQuantity))
            .ToListAsync(cancelToken);

        ILookup<int, HoldingRow> holdingsByBankId = holdingRows.ToLookup(holding => holding.AssetBankId);

        return
        [
            .. bankRows
                .OrderBy(bank => bank.AssetBankId)
                .Select(bank => new AssetBankHoldings
                {
                    AssetBankId = bank.AssetBankId,
                    PlayerId = bank.PlayerId,
                    SettlementId = bank.SettlementId,
                    NetworkKey = bank.NetworkKey,
                    Holdings = ToHoldings(holdingsByBankId[bank.AssetBankId])
                })
        ];
    }

    /// <summary>
    /// Orders one bank's holdings the way the Assets panel orders the same Assets.
    /// </summary>
    /// <remarks>
    /// Sort order, then name, then grade, matching <c>GetPlayerAssetsHandler.OrderAssets</c>. Grade breaks the final tie because every grade of a graded
    /// Asset shares one display name, so they would otherwise come back in whatever order the lookup happened to hold them.
    /// </remarks>
    private static IReadOnlyCollection<AssetBankHolding> ToHoldings(IEnumerable<HoldingRow> holdings)
    {
        return
        [
            .. holdings
                .OrderBy(holding => holding.SortOrder)
                .ThenBy(holding => holding.DisplayName)
                .ThenBy(holding => holding.AssetGrade)
                .Select(holding => new AssetBankHolding
                {
                    AssetId = holding.AssetId,
                    AssetCode = holding.AssetCode,
                    DisplayName = holding.DisplayName,
                    AssetGroupId = holding.AssetGroupId,
                    RawQuantity = holding.Quantity,
                    RawReserved = holding.ReservedQuantity
                })
        ];
    }

    private sealed record SiteRow(int AssetBankId, Guid NetworkKey);

    private sealed record BankRow(int AssetBankId, int PlayerId, int SettlementId, Guid NetworkKey);

    private sealed record HoldingRow(
        int AssetBankId,
        int AssetId,
        string AssetCode,
        string DisplayName,
        int? AssetGroupId,
        int SortOrder,
        AssetGrade AssetGrade,
        long Quantity,
        long ReservedQuantity);
}
