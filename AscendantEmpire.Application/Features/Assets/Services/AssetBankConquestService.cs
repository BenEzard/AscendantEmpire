using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Domain.Entities.Assets;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.Assets.Services;

/// <inheritdoc cref="IAssetBankConquestService" />
internal sealed class AssetBankConquestService(
    ILogger<AssetBankConquestService> logger,
    IAssetNetworkConnectivityService connectivityService
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
}
