using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Assets.Classes;
using AscendantEmpire.Domain.Entities.Assets;
using AscendantEmpire.Domain.Enums.Assets;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.Assets.Services;

/// <inheritdoc cref="IAssetBankCargoService" />
internal sealed class AssetBankCargoService(ILogger<AssetBankCargoService> logger) : IAssetBankCargoService
{
    public async Task<ServiceResultBase> CreditAsync(
        AscendantEmpireContext context,
        AssetBank bank,
        IReadOnlyCollection<CargoCredit> credits,
        CancellationToken cancelToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(bank);
        ArgumentNullException.ThrowIfNull(credits);

        if (bank.Kind != AssetBankKind.Unit)
        {
            string error = $"Asset Bank Id {bank.Id} is a {bank.Kind} bank, so it cannot be loaded as cargo.";
            logger.LogError("Asked to credit cargo into Asset Bank Id {AssetBankId}, which is a {Kind} bank.", bank.Id, bank.Kind);

            return InternalServerError(error);
        }

        // Several credits of one Asset are one arrival, so they are pooled before any row is touched. Non-positive amounts drop out here: a yield whose
        // share rounded away to nothing should leave no balance row behind.
        Dictionary<int, long> arrivingByAssetId = credits
            .Where(credit => credit.RawQuantity > 0)
            .GroupBy(credit => credit.AssetId)
            .ToDictionary(group => group.Key, group => group.Sum(credit => credit.RawQuantity));

        if (arrivingByAssetId.Count == 0)
        {
            return Succeed();
        }

        Dictionary<int, AssetBankBalance> balanceByAssetId = await ReadBalancesAsync(context, bank, arrivingByAssetId.Keys, cancelToken);

        // Ascending Asset id, so the same credits against the same bank always stage the same rows in the same order.
        foreach ((int assetId, long raw) in arrivingByAssetId.OrderBy(entry => entry.Key))
        {
            if (balanceByAssetId.TryGetValue(assetId, out AssetBankBalance? balance))
            {
                balance.Quantity += raw;

                continue;
            }

            var created = new AssetBankBalance
            {
                AssetId = assetId,
                Quantity = raw
            };

            // The navigation rather than the id, because a bank provisioned in this same unit of work has not been given one yet.
            if (bank.Id == 0)
            {
                created.Bank = bank;
            }
            else
            {
                created.AssetBankId = bank.Id;
            }

            context.AssetBankBalances.Add(created);
            balanceByAssetId[assetId] = created;
        }

        logger.LogInformation(
            "Staged {CreditCount} cargo credit(s) into Asset Bank Id {AssetBankId}, carried by Unit Instance Id {UnitInstanceId}.",
            arrivingByAssetId.Count, bank.Id, bank.UnitInstanceId);

        return Succeed();
    }

    /// <summary>
    /// The bank's existing balances for the arriving Assets, tracked and ready to be added to.
    /// </summary>
    /// <remarks>
    /// Rows already staged in the change tracker are folded in alongside the saved ones, since a query cannot see them and adding a second row for a pair
    /// the bank is already carrying would fail the composite key. An unsaved bank has no saved rows at all, so only the tracker is worth consulting.
    /// </remarks>
    private static async Task<Dictionary<int, AssetBankBalance>> ReadBalancesAsync(
        AscendantEmpireContext context,
        AssetBank bank,
        IEnumerable<int> assetIds,
        CancellationToken cancelToken)
    {
        int[] arrivingAssetIds = [.. assetIds];

        Dictionary<int, AssetBankBalance> balanceByAssetId = bank.Id == 0
            ? []
            : (await context.AssetBankBalances
                .Where(balance => balance.AssetBankId == bank.Id && arrivingAssetIds.Contains(balance.AssetId))
                .ToListAsync(cancelToken))
                .ToDictionary(balance => balance.AssetId);

        foreach (AssetBankBalance added in context.ChangeTracker
            .Entries<AssetBankBalance>()
            .Where(entry => entry.State == EntityState.Added)
            .Select(entry => entry.Entity))
        {
            bool belongsToThisBank = bank.Id != 0
                ? added.AssetBankId == bank.Id
                : ReferenceEquals(added.Bank, bank);

            if (belongsToThisBank && arrivingAssetIds.Contains(added.AssetId))
            {
                balanceByAssetId[added.AssetId] = added;
            }
        }

        return balanceByAssetId;
    }
}
