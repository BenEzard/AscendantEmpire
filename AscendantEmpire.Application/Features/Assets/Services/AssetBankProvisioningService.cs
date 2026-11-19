using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Domain.Entities.Assets;
using AscendantEmpire.Domain.Entities.Settlements;
using AscendantEmpire.Domain.Entities.Units;
using AscendantEmpire.Domain.Enums.Assets;

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
            Kind = AssetBankKind.Settlement,
            Settlement = settlement
        };

        // Through the connectivity service, which is the only thing allowed to write NetworkKey.
        connectivityService.AssignKeyToNewBank(bank);

        context.AssetBanks.Add(bank);

        logger.LogInformation("Staged an empty asset bank for Player Id {PlayerId}'s new {Tier}.", playerId, settlement.Tier);

        return Succeed();
    }

    public async Task<ServiceResult<AssetBank>> ProvisionForUnitAsync(AscendantEmpireContext context, UnitInstance unit, CancellationToken cancelToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(unit);

        // Unlike the Settlement path, which stages a bank for a Settlement still being founded, this one takes a Unit already standing on a tile: cargo is
        // provisioned when something is first put into it, which cannot happen before the Unit exists.
        if (unit.Id == 0)
        {
            string error = "A cargo bank can only be provisioned for a Unit that has been saved, since nothing can be carried by one that is not fielded yet.";
            logger.LogError("{Error} Player Id: {PlayerId}.", error, unit.PlayerId);

            return ServiceResult<AssetBank>.Fail(error);
        }

        // Tracked, because the caller is about to credit into whatever comes back and the caller's save is what commits it.
        AssetBank? existingBank = await context.AssetBanks.FirstOrDefaultAsync(bank => bank.UnitInstanceId == unit.Id, cancelToken);

        // Rows added to the context but not yet saved are invisible to that query, so a second capture inside one unit of work would otherwise stage a second
        // bank for the same Unit and fail the save on the unique index.
        existingBank ??= context.ChangeTracker
            .Entries<AssetBank>()
            .Where(entry => entry.State == EntityState.Added)
            .Select(entry => entry.Entity)
            .FirstOrDefault(bank => bank.UnitInstanceId == unit.Id);

        if (existingBank is not null)
        {
            // Handed back rather than refused. Provisioning cargo is a get-or-create: a capture has no way to know whether this Unit has carried anything
            // before, and asking it to find out first would put the same query in both places.
            return ServiceResult<AssetBank>.Succeed(existingBank);
        }

        var bank = new AssetBank
        {
            PlayerId = unit.PlayerId,
            Kind = AssetBankKind.Unit,
            UnitInstanceId = unit.Id
        };

        // Through the connectivity service, which is the only thing allowed to write NetworkKey.
        connectivityService.AssignKeyToNewBank(bank);

        context.AssetBanks.Add(bank);

        logger.LogInformation("Staged an empty cargo bank for Player Id {PlayerId}'s Unit Instance Id {UnitInstanceId}.", unit.PlayerId, unit.Id);

        return ServiceResult<AssetBank>.Succeed(bank);
    }

    public async Task<ServiceResultBase> RemoveForUnitAsync(AscendantEmpireContext context, int unitInstanceId, CancellationToken cancelToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        AssetBank? bank = await context.AssetBanks
            .Include(x => x.Balances)
            .FirstOrDefaultAsync(x => x.UnitInstanceId == unitInstanceId, cancelToken);

        // Most Units never carry anything, so having no bank is the ordinary case rather than a fault.
        if (bank is null)
        {
            return Succeed();
        }

        context.AssetBankBalances.RemoveRange(bank.Balances);
        context.AssetBanks.Remove(bank);

        logger.LogInformation(
            "Staged the removal of cargo bank Id {AssetBankId} and its {BalanceCount} balance(s), carried by Unit Instance Id {UnitInstanceId}.",
            bank.Id, bank.Balances.Count, unitInstanceId);

        return Succeed();
    }
}
