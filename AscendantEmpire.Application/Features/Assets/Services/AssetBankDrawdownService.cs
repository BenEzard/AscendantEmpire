using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Assets.Classes;
using AscendantEmpire.Application.Helpers.ScaledNumerics;
using AscendantEmpire.BackendContracts.Assets;
using AscendantEmpire.Domain.Entities.Assets;
using AscendantEmpire.Domain.Entities.Technologies;
using AscendantEmpire.Domain.Enums.Assets;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.Assets.Services;

/// <inheritdoc cref="IAssetBankDrawdownService" />
internal sealed class AssetBankDrawdownService(ILogger<AssetBankDrawdownService> logger) : IAssetBankDrawdownService
{
    /// <summary>
    /// The one Asset held per Player that has a store to debit today: the directed research points.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Matched on the catalogue code rather than the id, because the code is the stable key - the seed's upsert matches on it and never rewrites it, so
    /// renaming the Asset in the catalogue leaves this working.
    /// </para>
    /// <para>
    /// Culture is the other <c>Player</c>-scope Asset in the catalogue and is deliberately absent: nothing stores culture points yet, so a request to spend
    /// them is refused loudly rather than quietly written somewhere that is not the number the panel reads. Adding that store is its own ticket, and when it
    /// lands this becomes a second entry here.
    /// </para>
    /// </remarks>
    private const string _TECHNOLOGY_EXPERIENCE_ASSET_CODE = "Society.Experience.Technology";

    public async Task<ServiceResultBase> DebitAsync(AscendantEmpireContext context, AssetExpense expense, CancellationToken cancelToken = default)
    {
        // A zero expense is a caller that did not mean to call, and a negative one is a credit by another name - production owns those. Neither is something
        // the Player could have done, so neither reads as a refusal.
        if (expense.Amount <= ScaledAmount.Zero)
        {
            return InternalServerError($"An expense must be for a positive amount, and {expense.Amount} was requested.");
        }

        ServiceResult<ExpenseTarget> targetResult = await ResolveTargetAsync(context, expense, cancelToken);

        if (targetResult.HasSucceeded == false)
        {
            return targetResult;
        }

        ExpenseTarget target = targetResult.SuccessValue!;

        return target.StorageScope switch
        {
            AssetStorageScope.SettlementBank => await DebitBankAsync(context, expense, target, cancelToken),
            AssetStorageScope.Player => await DebitPlayerStoreAsync(context, expense, target, cancelToken),

            // Not a shortfall. A Derived Asset has no stored figure anywhere - Civilians are counted off the Settlements, not banked - so there is nothing to
            // debit and never will be. A caller asking to spend one has made a mistake about what the Asset is.
            AssetStorageScope.Derived => InternalServerError(
                $"\"{target.DisplayName}\" is derived from what already records it and has no stored balance, so it cannot be spent."),
            _ => InternalServerError($"\"{target.DisplayName}\" has an unrecognised storage scope ({target.StorageScope}), so it cannot be spent.")
        };
    }

    /// <summary>
    /// Works out what the expense names, in the order it would be spent, and which store routes it.
    /// </summary>
    /// <remarks>
    /// An Asset expense resolves to that Asset alone and routes on its own scope. A group expense resolves to the group's bankable Assets in spend order,
    /// and always routes to a bank - the group ordering is a rule about balances, and a group of Assets that are not banked has no balances to order.
    /// </remarks>
    private static async Task<ServiceResult<ExpenseTarget>> ResolveTargetAsync(AscendantEmpireContext context, AssetExpense expense, CancellationToken cancelToken)
    {
        if (expense.AssetId is not null)
        {
            TargetAsset? asset = await context.Assets
                .AsNoTracking()
                .Where(catalogueAsset => catalogueAsset.Id == expense.AssetId.Value)
                .Select(catalogueAsset => new TargetAsset(
                    catalogueAsset.Id,
                    catalogueAsset.Code,
                    catalogueAsset.DisplayName,
                    catalogueAsset.StorageScope))
                .FirstOrDefaultAsync(cancelToken);

            if (asset is null)
            {
                return ServiceResult<ExpenseTarget>.NotFound($"No Asset with id {expense.AssetId.Value} is in the catalogue.");
            }

            // The Asset's own name is what a refusal is phrased in: the caller asked for Oak Logs, so "Not enough Oak Log" is the honest message.
            return ServiceResult<ExpenseTarget>.Succeed(new ExpenseTarget(asset.DisplayName, asset.StorageScope, [asset]));
        }

        int assetGroupId = expense.AssetGroupId!.Value;

        // The group's own name, because a group expense asked for Logs rather than for any particular log, and that is what the Player should be told it is
        // short of.
        string? groupDisplayName = await context.AssetGroups
            .AsNoTracking()
            .Where(group => group.Id == assetGroupId)
            .Select(group => group.DisplayName)
            .FirstOrDefaultAsync(cancelToken);

        if (groupDisplayName is null)
        {
            return ServiceResult<ExpenseTarget>.NotFound($"No AssetGroup with id {assetGroupId} is in the catalogue.");
        }

        List<TargetAsset> candidates = await ReadGroupSpendOrderAsync(context, assetGroupId, cancelToken);

        if (candidates.Count == 0)
        {
            // Every population group is Derived and every Asset in it unbankable, so this is the same mistake as naming a Derived Asset outright, reported
            // against the group the caller actually passed.
            return ServiceResult<ExpenseTarget>.InternalServerError(
                $"AssetGroup {assetGroupId} (\"{groupDisplayName}\") holds no Asset that is kept in a settlement bank, so it cannot be spent out of one.");
        }

        return ServiceResult<ExpenseTarget>.Succeed(new ExpenseTarget(groupDisplayName, AssetStorageScope.SettlementBank, candidates));
    }

    /// <summary>
    /// The group's bankable Assets, in the order a group expense consumes them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The ordering is the Player's, and RS-16 owns persisting it.</b> Until that lands this is the catalogue's global <c>Asset.SortOrder</c>, which the
    /// seed spaces by 10 for exactly this kind of use. That fallback is permanent in any case: a Player who has never rearranged a ticker has no ordering of
    /// their own, and an arbitrary one would make the same expense spend different stock for different Players.
    /// </para>
    /// <para>
    /// When the per-Player ordering arrives it is keyed <c>(PlayerId, AssetGroupId, AssetId)</c>. Not on the category: every Asset reachable by a group
    /// expense is on the grouped arm of the catalogue, where <c>AssetCategoryId</c> is null, so a category-keyed priority would match nothing here.
    /// </para>
    /// <para>
    /// Only <c>SettlementBank</c> Assets are candidates. A Derived or player-level Asset sitting in a group has no balance in the bank, so including it would
    /// put a step in the walk that can never yield anything.
    /// </para>
    /// </remarks>
    private static async Task<List<TargetAsset>> ReadGroupSpendOrderAsync(AscendantEmpireContext context, int assetGroupId, CancellationToken cancelToken)
    {
        return await context.Assets
            .AsNoTracking()
            .Where(asset => asset.AssetGroupId == assetGroupId)
            .Where(asset => asset.StorageScope == AssetStorageScope.SettlementBank)
            .OrderBy(asset => asset.SortOrder)
            .ThenBy(asset => asset.DisplayName)
            .ThenBy(asset => asset.AssetGrade)
            .Select(asset => new TargetAsset(asset.Id, asset.Code, asset.DisplayName, asset.StorageScope))
            .ToListAsync(cancelToken);
    }

    /// <summary>
    /// Spends the expense out of the bank the Settlement draws on.
    /// </summary>
    /// <remarks>
    /// Sufficiency is settled over the whole candidate set before anything is written, which is what makes a group expense all-or-nothing: the walk below
    /// only ever runs when the total is already known to cover the amount.
    /// </remarks>
    private async Task<ServiceResultBase> DebitBankAsync(AscendantEmpireContext context, AssetExpense expense, ExpenseTarget target, CancellationToken cancelToken)
    {
        // Through the Bank navigation, so this is an INNER JOIN on a required foreign key and another Player's bank cannot be reached by naming their Settlement.
        int? assetBankId = await context.AssetBankSettlementMemberships
            .AsNoTracking()
            .Where(membership => membership.SettlementId == expense.SettlementId && membership.Bank.PlayerId == expense.PlayerId)
            .Select(membership => (int?)membership.AssetBankId)
            .FirstOrDefaultAsync(cancelToken);

        if (assetBankId is null)
        {
            return NotFound($"Settlement {expense.SettlementId} has no asset bank belonging to Player {expense.PlayerId}, so nothing can be spent there.");
        }

        int[] candidateAssetIds = [.. target.Assets.Select(asset => asset.AssetId)];

        // Tracked, unlike every other read here: these are the rows being written, and the caller's SaveChangesAsync is what commits them.
        Dictionary<int, AssetBankBalance> balancesByAssetId = await context.AssetBankBalances
            .Where(balance => balance.AssetBankId == assetBankId.Value && candidateAssetIds.Contains(balance.AssetId))
            .ToDictionaryAsync(balance => balance.AssetId, cancelToken);

        // A candidate with no row holds none of it, which is the same as a zero, so the sum needs no entry for it.
        ScaledAmount available = ScaledAmount.FromRaw(balancesByAssetId.Values.Sum(balance => balance.Quantity));

        if (available < expense.Amount)
        {
            return ToShortfall(expense, target, available);
        }

        ScaledAmount remaining = expense.Amount;

        foreach (TargetAsset candidate in target.Assets)
        {
            if (remaining <= ScaledAmount.Zero)
            {
                break;
            }

            if (balancesByAssetId.TryGetValue(candidate.AssetId, out AssetBankBalance? balance) == false)
            {
                continue;
            }

            // Each Asset is taken to nothing before the next is touched, which is what the design doc's Oak-then-Cedar walk means.
            ScaledAmount held = ScaledAmount.FromRaw(balance.Quantity);
            ScaledAmount taken = held <= remaining ? held : remaining;

            balance.Quantity -= taken.RawValue;
            remaining -= taken;

            // An emptied balance is deleted rather than left as a zero. A missing row and a zero row mean the same thing to every reader, and deleting keeps
            // a bank from accumulating a row per Asset it has ever briefly held.
            if (balance.Quantity == 0)
            {
                context.AssetBankBalances.Remove(balance);
            }
        }

        logger.LogInformation(
            "Debited {Amount} {Asset} from asset bank {AssetBankId} for player {PlayerId}, at settlement {SettlementId}.",
            expense.Amount,
            target.DisplayName,
            assetBankId.Value,
            expense.PlayerId,
            expense.SettlementId);

        return SuccessNoContent();
    }

    /// <summary>
    /// Spends the expense out of the player-level store that already holds the Asset's figure.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Only a single-Asset expense reaches here, because a group expense is routed to a bank regardless of what its Assets are. Connectivity and the
    /// expense's Settlement are both irrelevant to a figure held once per Player for the whole game.
    /// </para>
    /// <para>
    /// <b>Diffusion points are deliberately not spendable.</b> <c>UnspentDirectedPoints</c> is the fungible pool - a budget the Player generates and spends -
    /// while diffusion points arrive attached to a trade partner or a neighbour, so folding them into the same purse would let a purchase consume something
    /// the Player was never given to spend.
    /// </para>
    /// </remarks>
    private async Task<ServiceResultBase> DebitPlayerStoreAsync(
        AscendantEmpireContext context,
        AssetExpense expense,
        ExpenseTarget target,
        CancellationToken cancelToken)
    {
        TargetAsset asset = target.Assets.Single();

        if (string.Equals(asset.Code, _TECHNOLOGY_EXPERIENCE_ASSET_CODE, StringComparison.OrdinalIgnoreCase) == false)
        {
            return InternalServerError(
                $"\"{asset.DisplayName}\" ({asset.Code}) is held per Player, but nothing stores it yet, so it cannot be spent.");
        }

        // The store counts whole points, so a fractional expense has nowhere to land. Rounding it either way would silently over- or under-charge, so the
        // caller is told it asked for something the store cannot express.
        if (expense.Amount.RemainderRaw() != 0)
        {
            return InternalServerError(
                $"\"{asset.DisplayName}\" is counted in whole points, so {expense.Amount} cannot be spent of it.");
        }

        // Tracked: this is the row being written.
        PlayerResearchState? researchState = await context.PlayerResearchStates
            .FirstOrDefaultAsync(state => state.PlayerId == expense.PlayerId, cancelToken);

        if (researchState is null)
        {
            return NotFound($"Player {expense.PlayerId} has no research state, so their \"{asset.DisplayName}\" points cannot be spent.");
        }

        ScaledAmount available = ScaledAmount.FromWhole(researchState.UnspentDirectedPoints);

        if (available < expense.Amount)
        {
            return ToShortfall(expense, target, available);
        }

        // Exact rather than rounded: the remainder was checked above, so the whole value is the amount asked for.
        researchState.UnspentDirectedPoints -= (int)expense.Amount.ToWholeRoundedDown();

        logger.LogInformation(
            "Debited {Amount} {Asset} from the player-level store for player {PlayerId}.",
            expense.Amount,
            target.DisplayName,
            expense.PlayerId);

        return SuccessNoContent();
    }

    /// <summary>
    /// The refusal a Player sees when they cannot afford the expense, carrying the numbers as well as the sentence.
    /// </summary>
    /// <remarks>
    /// A conflict rather than a bad request: the expense was well formed and is simply at odds with what the Player currently holds, which is also why
    /// retrying it after a turn of production is a sensible thing for a caller to do.
    /// </remarks>
    private static ServiceResultBase ToShortfall(AssetExpense expense, ExpenseTarget target, ScaledAmount available)
    {
        ScaledAmount shortfall = expense.Amount - available;

        var detail = new AssetShortfall
        {
            AssetId = expense.AssetId,
            AssetGroupId = expense.AssetGroupId,
            DisplayName = target.DisplayName,
            RawRequired = expense.Amount.RawValue,
            RawAvailable = available.RawValue,
            RawShortfall = shortfall.RawValue
        };

        return ServiceResult<Empty>.Conflict($"Not enough {target.DisplayName}: {expense.Amount} needed, {available} available.", detail);
    }

    /// <summary>
    /// What the expense resolved to: the name to refuse it under, the store that answers for it, and the Assets to walk in spend order.
    /// </summary>
    /// <param name="DisplayName">The group's name for a group expense, and the Asset's own for an Asset one.</param>
    /// <param name="Assets">Exactly one entry for an Asset expense; the group's bankable Assets, in spend order, for a group one.</param>
    private sealed record ExpenseTarget(string DisplayName, AssetStorageScope StorageScope, IReadOnlyList<TargetAsset> Assets);

    private sealed record TargetAsset(int AssetId, string Code, string DisplayName, AssetStorageScope StorageScope);
}
