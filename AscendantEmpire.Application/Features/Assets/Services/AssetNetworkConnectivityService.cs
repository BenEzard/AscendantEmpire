using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Assets.Classes;
using AscendantEmpire.Domain.Entities.Assets;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.Assets.Services;

/// <inheritdoc cref="IAssetNetworkConnectivityService" />
/// <remarks>
/// <b>Connectivity is isolation.</b> Nothing in the codebase joins two Settlements - there is no road at all, and the river and coastline walks are CM-64,
/// RS-48 and RS-49 - so the only correct arrangement today is one network per bank. Both methods below are written against that single rule, and both read it
/// from <see cref="FindFault"/> so that the three transport tasks have one place to replace.
/// </remarks>
internal sealed class AssetNetworkConnectivityService(ILogger<AssetNetworkConnectivityService> logger) : IAssetNetworkConnectivityService
{
    public void AssignKeyToNewBank(AssetBank bank)
    {
        bank.NetworkKey = Guid.NewGuid();
    }

    public async Task<ServiceResultBase> RecomputeForPlayerAsync(AscendantEmpireContext context, int playerId, CancellationToken cancelToken = default)
    {
        // Tracked, because these are the rows being written and the caller's SaveChangesAsync is what commits them.
        // Ordered by id so that the bank keeping a shared key is always the same one, and a second run has nothing left to change.
        List<AssetBank> banks = await context.AssetBanks
            .Where(bank => bank.PlayerId == playerId)
            .OrderBy(bank => bank.Id)
            .ToListAsync(cancelToken);

        var keysInUse = new HashSet<Guid>();
        int reKeyedCount = 0;

        foreach (AssetBank bank in banks)
        {
            if (FindFault(bank.NetworkKey, keysInUse) is not null)
            {
                bank.NetworkKey = Guid.NewGuid();
                reKeyedCount++;
            }

            keysInUse.Add(bank.NetworkKey);
        }

        if (reKeyedCount > 0)
        {
            logger.LogInformation("Re-keyed {ReKeyedCount} of Player Id {PlayerId}'s {BankCount} asset banks.", reKeyedCount, playerId, banks.Count);
        }

        return Succeed();
    }

    public async Task<ServiceResult<IReadOnlyCollection<AssetNetworkFault>>> FindFaultsForPlayerAsync(
        AscendantEmpireContext context,
        int playerId,
        CancellationToken cancelToken = default)
    {
        List<BankRow> banks = await context.AssetBanks
            .AsNoTracking()
            .Where(bank => bank.PlayerId == playerId)
            .OrderBy(bank => bank.Id)
            .Select(bank => new BankRow(bank.Id, bank.SettlementId, bank.NetworkKey))
            .ToListAsync(cancelToken);

        var keysInUse = new HashSet<Guid>();
        var faults = new List<AssetNetworkFault>();

        foreach (BankRow bank in banks)
        {
            if (FindFault(bank.NetworkKey, keysInUse) is AssetNetworkFaultKind kind)
            {
                faults.Add(new AssetNetworkFault(bank.AssetBankId, bank.SettlementId, kind));
            }

            keysInUse.Add(bank.NetworkKey);
        }

        return ServiceResult<IReadOnlyCollection<AssetNetworkFault>>.Succeed(faults);
    }

    /// <summary>
    /// How a bank's key disagrees with the one network-per-bank rule, or <c>null</c> where it does not, given the keys already claimed by lower bank ids.
    /// </summary>
    /// <remarks>
    /// The single place the rule lives, read by the re-key and by the consistency check alike, so the two cannot disagree about what a fault is. CM-64, RS-48
    /// and RS-49 each widen it, at which point the keys a bank is allowed to share stop being none and become the keys of the banks it can actually reach.
    /// </remarks>
    private static AssetNetworkFaultKind? FindFault(Guid networkKey, HashSet<Guid> keysInUse)
    {
        if (networkKey == Guid.Empty)
        {
            return AssetNetworkFaultKind.Unkeyed;
        }

        if (keysInUse.Contains(networkKey))
        {
            return AssetNetworkFaultKind.SharedKey;
        }

        return null;
    }

    private sealed record BankRow(int AssetBankId, int SettlementId, Guid NetworkKey);
}
