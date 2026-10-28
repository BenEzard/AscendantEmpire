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
    public async Task<ServiceResult<AssetBankHoldings>> GetForSettlementAsync(
        AscendantEmpireContext context,
        int playerId,
        int settlementId,
        CancellationToken cancelToken = default)
    {
        // Projected through the Bank navigation rather than joined, and so an INNER JOIN on a required foreign key: a membership whose bank is another
        // Player's drops out here, which is what makes "not yours" and "no bank" one answer rather than two.
        int? assetBankId = await context.AssetBankSettlementMemberships
            .AsNoTracking()
            .Where(membership => membership.SettlementId == settlementId && membership.Bank.PlayerId == playerId)
            .Select(membership => (int?)membership.AssetBankId)
            .FirstOrDefaultAsync(cancelToken);

        if (assetBankId is null)
        {
            return ServiceResult<AssetBankHoldings>.NotFound($"Settlement {settlementId} has no asset bank belonging to Player {playerId}.");
        }

        IReadOnlyCollection<AssetBankHoldings> banks = await ReadBanksAsync(context, playerId, assetBankId.Value, cancelToken);

        // The membership above proved the bank exists and is this Player's, so exactly one bank comes back. A miss here would mean the row vanished between
        // the two reads, which is a failure rather than an empty result.
        if (banks.Count != 1)
        {
            return ServiceResult<AssetBankHoldings>.NotFound($"Asset bank {assetBankId.Value}, which Settlement {settlementId} draws on, could not be read.");
        }

        return ServiceResult<AssetBankHoldings>.Succeed(banks.Single());
    }

    public async Task<ServiceResult<IReadOnlyCollection<AssetBankHoldings>>> GetForPlayerAsync(
        AscendantEmpireContext context,
        int playerId,
        CancellationToken cancelToken = default)
    {
        IReadOnlyCollection<AssetBankHoldings> banks = await ReadBanksAsync(context, playerId, assetBankId: null, cancelToken);

        // A Player with no settlement yet owns no banks. That is an empty list, not a missing resource.
        return ServiceResult<IReadOnlyCollection<AssetBankHoldings>>.Succeed(banks);
    }

    /// <summary>
    /// Reads the Player's banks, optionally narrowed to one, and assembles them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Three flat reads joined in memory rather than one query with nested collection projections, which is the shape <c>GetPlayerAssetsHandler</c> already
    /// uses for the asset tree. A Player holds a handful of banks and each holds at most a few hundred balances, so the assembly is cheap, and keeping the
    /// membership read separate means a bank with no memberships still comes back - which it has to, because an orphaned bank still holds stock.
    /// </para>
    /// <para>The Player filter is applied to the banks themselves, so narrowing by id cannot reach another Player's bank even when the id is theirs.</para>
    /// </remarks>
    /// <param name="assetBankId">The single bank to read, or <see langword="null"/> for every bank the Player owns.</param>
    private static async Task<IReadOnlyCollection<AssetBankHoldings>> ReadBanksAsync(AscendantEmpireContext context, int playerId, int? assetBankId, CancellationToken cancelToken)
    {
        IQueryable<AssetBank> banks = context.AssetBanks
            .AsNoTracking()
            .Where(bank => bank.PlayerId == playerId);

        if (assetBankId is not null)
        {
            banks = banks.Where(bank => bank.Id == assetBankId.Value);
        }

        List<BankRow> bankRows = await banks
            .Select(bank => new BankRow(bank.Id, bank.PlayerId))
            .ToListAsync(cancelToken);

        if (bankRows.Count == 0)
        {
            return [];
        }

        int[] bankIds = [.. bankRows.Select(bank => bank.AssetBankId)];

        List<MembershipRow> membershipRows = await context.AssetBankSettlementMemberships
            .AsNoTracking()
            .Where(membership => bankIds.Contains(membership.AssetBankId))
            .Select(membership => new MembershipRow(membership.AssetBankId, membership.SettlementId))
            .ToListAsync(cancelToken);

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
                balance.Quantity))
            .ToListAsync(cancelToken);

        ILookup<int, MembershipRow> membershipsByBankId = membershipRows.ToLookup(membership => membership.AssetBankId);
        ILookup<int, HoldingRow> holdingsByBankId = holdingRows.ToLookup(holding => holding.AssetBankId);

        return
        [
            .. bankRows
                .OrderBy(bank => bank.AssetBankId)
                .Select(bank => new AssetBankHoldings
                {
                    AssetBankId = bank.AssetBankId,
                    PlayerId = bank.PlayerId,
                    SettlementIds =
                    [
                        .. membershipsByBankId[bank.AssetBankId]
                            .Select(membership => membership.SettlementId)
                            .OrderBy(settlementId => settlementId)
                    ],
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
                    RawQuantity = holding.Quantity
                })
        ];
    }

    private sealed record BankRow(int AssetBankId, int PlayerId);

    private sealed record MembershipRow(int AssetBankId, int SettlementId);

    private sealed record HoldingRow(
        int AssetBankId,
        int AssetId,
        string AssetCode,
        string DisplayName,
        int? AssetGroupId,
        int SortOrder,
        AssetGrade AssetGrade,
        long Quantity);
}
