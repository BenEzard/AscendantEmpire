using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Assets.Classes;
using AscendantEmpire.Application.Features.Settlements.Classes;
using AscendantEmpire.Domain.Enums.Settlements;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.Assets.Services;

/// <inheritdoc cref="IAssetBankCapacityService" />
/// <remarks>
/// <para>
/// Two reads: the Settlements' tiers, and what their banks hold grouped by storage class. The second projects through the mapped <c>Asset</c> navigation, so EF
/// expands the join rather than this service naming it - the class an Asset is kept under is catalogue data, and reading it any other way would be a second
/// source for it.
/// </para>
/// </remarks>
internal sealed class AssetBankCapacityService(ILogger<AssetBankCapacityService> logger) : IAssetBankCapacityService
{
    public async Task<ServiceResult<IReadOnlyDictionary<int, SettlementStorageCapacity>>> GetSettlementCapacitiesAsync(
        AscendantEmpireContext context,
        IReadOnlyCollection<int> settlementIds,
        CancellationToken cancelToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(settlementIds);

        if (settlementIds.Count == 0)
        {
            return ServiceResult<IReadOnlyDictionary<int, SettlementStorageCapacity>>.Succeed(new Dictionary<int, SettlementStorageCapacity>());
        }

        int[] distinctSettlementIds = [.. settlementIds.Distinct()];

        Dictionary<int, SettlementTier> tierBySettlementId = await context.Settlements
            .AsNoTracking()
            .Where(settlement => distinctSettlementIds.Contains(settlement.Id))
            .Select(settlement => new { settlement.Id, settlement.Tier })
            .ToDictionaryAsync(settlement => settlement.Id, settlement => settlement.Tier, cancelToken);

        int[] missingSettlementIds = [.. distinctSettlementIds.Except(tierBySettlementId.Keys)];

        if (missingSettlementIds.Length > 0)
        {
            // A caller asking about a Settlement that is not there has read something stale, and answering for the ones that do exist would hide which.
            string error = $"No settlement row was found for Id(s) {string.Join(", ", missingSettlementIds)}, so their storage capacity could not be read.";
            logger.LogError("{Error}", error);

            return ServiceResult<IReadOnlyDictionary<int, SettlementStorageCapacity>>.NotFound(error);
        }

        List<Stock> stock = await context.AssetBankBalances
            .AsNoTracking()
            .Where(balance => distinctSettlementIds.Contains(balance.Bank.SettlementId))
            .GroupBy(balance => new { balance.Bank.SettlementId, balance.Asset.StorageClassId })
            .Select(group => new Stock(group.Key.SettlementId, group.Key.StorageClassId, group.Sum(balance => balance.Quantity)))
            .ToListAsync(cancelToken);

        Dictionary<int, Dictionary<int, long>> heldBySettlementId = stock
            .GroupBy(row => row.SettlementId)
            .ToDictionary(group => group.Key, group => group.ToDictionary(row => row.StorageClassId, row => row.RawHeld));

        var capacityBySettlementId = new Dictionary<int, SettlementStorageCapacity>(distinctSettlementIds.Length);

        foreach (int settlementId in distinctSettlementIds)
        {
            SettlementTier tier = tierBySettlementId[settlementId];

            if (SettlementStorageRules.TryGetGenericPoolCapacity(tier, out long rawPoolCapacity) == false)
            {
                // Every real tier is authored, so this is a tier cast from a value that is not one - a data defect rather than a game state.
                string error = $"Settlement Id {settlementId} is of tier {tier}, which has no authored generic storage pool.";
                logger.LogError("{Error}", error);

                return ServiceResult<IReadOnlyDictionary<int, SettlementStorageCapacity>>.Fail(error);
            }

            Dictionary<int, long> rawHeldByClassId = heldBySettlementId.GetValueOrDefault(settlementId) ?? [];

            capacityBySettlementId[settlementId] = new SettlementStorageCapacity(
                rawPoolCapacity,
                rawDedicatedCapacityByClassId: new Dictionary<int, long>(),
                rawHeldByClassId);
        }

        return ServiceResult<IReadOnlyDictionary<int, SettlementStorageCapacity>>.Succeed(capacityBySettlementId);
    }

    /// <summary>What one Settlement holds of one storage class, summed across every Asset kept that way.</summary>
    private sealed record Stock(int SettlementId, int StorageClassId, long RawHeld);
}
