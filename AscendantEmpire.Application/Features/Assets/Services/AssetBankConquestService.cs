using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Assets.Classes;
using AscendantEmpire.Application.Helpers.ScaledNumerics;
using AscendantEmpire.Domain.Entities.Assets;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.Assets.Services;

/// <inheritdoc cref="IAssetBankConquestService" />
internal sealed class AssetBankConquestService(
    ILogger<AssetBankConquestService> logger,
    IAssetNetworkConnectivityService connectivityService,
    IAssetBankCreditService creditService
) : IAssetBankConquestService
{
    public async Task<ServiceResultBase> TransferOnConquestAsync(
        AscendantEmpireContext context,
        int settlementId,
        int newOwnerPlayerId,
        CancellationToken cancelToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        try
        {
            // Tracked, because this is the row being written. Found by Settlement rather than by bank id: the column is unique and never changes, so there is
            // at most one bank this can be.
            AssetBank? bank = await context.AssetBanks
                .FirstOrDefaultAsync(bank => bank.SettlementId == settlementId, cancelToken);

            if (bank is null)
            {
                string error = $"Settlement Id {settlementId} has no asset bank, so there is nothing to hand over.";
                logger.LogError("{Error} Every Settlement is born with one, so this is a capture naming a Settlement that does not exist.", error);

                return NotFound(error);
            }

            int losingPlayerId = bank.PlayerId;

            if (losingPlayerId == newOwnerPlayerId)
            {
                string error = $"Settlement Id {settlementId}'s asset bank already belongs to Player Id {newOwnerPlayerId}, so there is no conquest to make.";
                logger.LogWarning("{Error}", error);

                return Fail(error);
            }

            bank.PlayerId = newOwnerPlayerId;

            // Through the connectivity service, which is the only thing allowed to write NetworkKey. A network of one: what the captured bank can now reach
            // is the recompute below, not this.
            connectivityService.AssignKeyToCapturedBank(bank);

            // Two saves have to land together, as in IStructureLifecycleWriter.FoundAsync. A caller already inside a transaction keeps ownership of it;
            // otherwise this opens one, and disposing it uncommitted rolls both saves back.
            await using IDbContextTransaction? ownTransaction = context.Database.CurrentTransaction is null
                ? await context.Database.BeginTransactionAsync(cancelToken)
                : null;

            // The hand-over is committed before either recompute runs, because they read ownership from the database and not from the change tracker. Staging
            // both in one unit of work would assess each Player against banks they no longer hold.
            await context.SaveChangesAsync(cancelToken);

            // Both sides. The loser's empire may have been partitioned by losing the bridge between two halves of it, which is as much a re-key as the
            // conqueror's gain. The order does not matter - a recompute is idempotent and reads only its own Player's banks.
            int[] playersToRecompute = [losingPlayerId, newOwnerPlayerId];

            foreach (int playerId in playersToRecompute)
            {
                ServiceResultBase recomputeResult = await connectivityService.RecomputeForPlayerAsync(context, playerId, cancelToken);

                if (recomputeResult.HasSucceeded == false)
                {
                    string error = $"Settlement Id {settlementId}'s asset bank could not be handed to Player Id {newOwnerPlayerId}, because Player Id {playerId}'s networks could not be recomputed.";
                    logger.LogError("{Error} Errors: {Errors}", error, string.Join("; ", recomputeResult.Errors));

                    return Fail(recomputeResult.Errors.Prepend(error));
                }
            }

            // After the recompute, so a released quantity spreads across whatever network the captured bank now belongs to rather than being trapped in the
            // network of one it was handed over as.
            ServiceResultBase reserveResult = await SettleCapturedReserveAsync(context, bank, newOwnerPlayerId, cancelToken);

            if (reserveResult.HasSucceeded == false)
            {
                string error = $"Settlement Id {settlementId}'s asset bank was handed to Player Id {newOwnerPlayerId}, but its strategic reserve could not be settled against their policy.";
                logger.LogError("{Error} Errors: {Errors}", error, string.Join("; ", reserveResult.Errors));

                return Fail(reserveResult.Errors.Prepend(error));
            }

            await context.SaveChangesAsync(cancelToken);

            if (ownTransaction is not null)
            {
                await ownTransaction.CommitAsync(cancelToken);
            }

            logger.LogInformation(
                "Player Id {NewOwnerPlayerId} took Asset Bank Id {AssetBankId} with Settlement Id {SettlementId} from Player Id {LosingPlayerId}, and both players' networks were recomputed.",
                newOwnerPlayerId, bank.Id, settlementId, losingPlayerId);

            return Succeed();
        }
        catch (Exception ex)
        {
            string error = $"An error occurred while handing Settlement Id {settlementId}'s asset bank to Player Id {newOwnerPlayerId}.";
            logger.LogError(ex, "{Error}", error);

            return Fail(error);
        }
    }

    /// <summary>
    /// Decides how much of a captured bank's strategic reserve the conqueror keeps in reserve, and releases the rest into circulation.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The conqueror's policy decides, not the loser's.</b> A reserve is kept up to the conqueror's own target for that Asset, counting what they already
    /// hold in reserve everywhere else, and the excess is released. Where they have stated no policy for the Asset the whole reserve is released.
    /// </para>
    /// <para>
    /// <b>The release goes through the credit path.</b> A released quantity becomes ordinary stock and so has to fit the Settlement's stockpile, which is a
    /// different budget from the reserve it was sitting in. Crediting it means capacity binds and an overflow is reported rather than silently exceeding the
    /// store; what will not fit is lost, which is the honest outcome of capturing more than you can keep.
    /// </para>
    /// </remarks>
    private async Task<ServiceResultBase> SettleCapturedReserveAsync(
        AscendantEmpireContext context,
        AssetBank capturedBank,
        int newOwnerPlayerId,
        CancellationToken cancelToken)
    {
        // Tracked, because the reserve column on these rows is what gets written down.
        List<AssetBankBalance> reservedBalances = await context.AssetBankBalances
            .Where(balance => balance.AssetBankId == capturedBank.Id && balance.ReservedQuantity > 0)
            .ToListAsync(cancelToken);

        if (reservedBalances.Count == 0)
        {
            return Succeed();
        }

        int[] assetIds = [.. reservedBalances.Select(balance => balance.AssetId)];

        Dictionary<int, long> targetByAssetId = await context.PlayerStrategicReservePolicies
            .AsNoTracking()
            .Where(policy => policy.PlayerId == newOwnerPlayerId && assetIds.Contains(policy.AssetId))
            .ToDictionaryAsync(policy => policy.AssetId, policy => policy.TargetQuantity, cancelToken);

        // What the conqueror already holds in reserve elsewhere, which the target is measured against. The captured bank is excluded because it is theirs
        // only as of a moment ago: counting it would have the reserve crowd out its own headroom.
        Dictionary<int, long> alreadyReservedByAssetId = await context.AssetBankBalances
            .AsNoTracking()
            .Where(balance =>
                balance.Bank.PlayerId == newOwnerPlayerId
                && balance.AssetBankId != capturedBank.Id
                && assetIds.Contains(balance.AssetId))
            .GroupBy(balance => balance.AssetId)
            .Select(group => new { AssetId = group.Key, RawReserved = group.Sum(balance => balance.ReservedQuantity) })
            .ToDictionaryAsync(row => row.AssetId, row => row.RawReserved, cancelToken);

        List<AssetCredit> releases = [];

        foreach (AssetBankBalance balance in reservedBalances)
        {
            long keptRaw = 0;

            if (targetByAssetId.TryGetValue(balance.AssetId, out long targetRaw))
            {
                alreadyReservedByAssetId.TryGetValue(balance.AssetId, out long alreadyReservedRaw);

                long headroomRaw = Math.Max(0, targetRaw - alreadyReservedRaw);
                keptRaw = Math.Min(balance.ReservedQuantity, headroomRaw);
            }

            long releasedRaw = balance.ReservedQuantity - keptRaw;

            if (releasedRaw == 0)
            {
                continue;
            }

            balance.ReservedQuantity = keptRaw;

            releases.Add(AssetCredit.AtSettlement(
                newOwnerPlayerId,
                capturedBank.SettlementId,
                balance.AssetId,
                ScaledAmount.FromRaw(releasedRaw)));
        }

        if (releases.Count == 0)
        {
            return Succeed();
        }

        ServiceResult<IReadOnlyList<AssetCreditPlacement>> creditResult = await creditService.CreditAsync(context, releases, cancelToken);

        if (creditResult.HasSucceeded == false)
        {
            return Fail(creditResult.Errors);
        }

        foreach (AssetCreditPlacement placement in creditResult.SuccessValue ?? [])
        {
            if (placement.RawUnplaced > 0)
            {
                logger.LogWarning(
                    "Releasing a captured reserve of Asset Id {AssetId} at Settlement Id {SettlementId} for Player Id {PlayerId} lost {RawUnplaced} raw for want of storage.",
                    placement.Credit.AssetId, capturedBank.SettlementId, newOwnerPlayerId, placement.RawUnplaced);
            }
        }

        return Succeed();
    }
}
