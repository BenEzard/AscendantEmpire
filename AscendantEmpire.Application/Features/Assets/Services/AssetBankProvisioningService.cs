using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Domain.Entities.Assets;
using AscendantEmpire.Domain.Entities.Settlements;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.Assets.Services;

/// <inheritdoc cref="IAssetBankProvisioningService" />
internal sealed class AssetBankProvisioningService(
    ILogger<AssetBankProvisioningService> logger,
    IAssetNetworkConnectivityService connectivityService
) : IAssetBankProvisioningService
{
    public async Task<ServiceResultBase> ProvisionForNewSettlementAsync(AscendantEmpireContext context, int playerId, Settlement settlement, CancellationToken cancelToken = default)
    {
        // Only worth asking of a Settlement that has been saved; an unsaved one has no id a bank could already carry. Guards a later caller rather than this
        // one - the settle path provisions a Settlement it has just created - and turns what would be a unique index violation at save time into a refusal
        // that says what the caller actually got wrong.
        if (settlement.Id != 0)
        {
            int? existingBankId = await context.AssetBanks
                .AsNoTracking()
                .Where(bank => bank.SettlementId == settlement.Id)
                .Select(bank => (int?)bank.Id)
                .FirstOrDefaultAsync(cancelToken);

            if (existingBankId is not null)
            {
                string error = $"Settlement Id {settlement.Id} already has Asset Bank Id {existingBankId}, so it cannot be given another.";
                logger.LogError("Asked to provision an asset bank for Settlement Id {SettlementId}, which already has Asset Bank Id {AssetBankId}.",
                    settlement.Id, existingBankId);

                return InternalServerError(error);
            }
        }

        // The navigation rather than the id, because the Settlement is routinely still unsaved here.
        var bank = new AssetBank
        {
            PlayerId = playerId,
            Settlement = settlement
        };

        // Through the connectivity service, which is the only thing allowed to write NetworkKey.
        connectivityService.AssignKeyToNewBank(bank);

        context.AssetBanks.Add(bank);

        logger.LogInformation("Staged an empty asset bank for Player Id {PlayerId}'s new {Tier}.", playerId, settlement.Tier);

        return Succeed();
    }
}
