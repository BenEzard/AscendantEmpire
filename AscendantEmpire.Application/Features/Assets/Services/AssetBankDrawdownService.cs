using System.Diagnostics;

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
    public async Task<ServiceResultBase> DebitAsync(AscendantEmpireContext context, AssetExpense expense, CancellationToken cancelToken = default)
    {
        // Nothing checks the amount here: AssetExpense cannot be built with a non-positive one, so a zero or negative expense never reaches this method.
        ServiceResult<ExpenseTarget> targetResult = await ResolveTargetAsync(context, expense, cancelToken);

        if (targetResult.HasSucceeded == false)
        {
            return targetResult;
        }

        ExpenseTarget target = targetResult.SuccessValue!;

        return target.StorageScope switch
        {
            // The two arms spend out of different sets of banks and refuse for different reasons, so each resolves its own rather than one read taking a
            // conditional filter.
            AssetStorageScope.SettlementBank => expense.IsEmpireWide
                ? await DebitEveryBankAsync(context, expense, target, cancelToken)
                : await DebitSettlementNetworkAsync(context, expense, target, cancelToken),

            AssetStorageScope.Player => await DebitPlayerStoreAsync(context, expense, target, cancelToken),

            // Not a shortfall. A Derived Asset has no stored figure anywhere - Civilians are counted off the Settlements, not banked - so there is nothing to
            // debit and never will be. A caller asking to spend one has made a mistake about what the Asset is.
            AssetStorageScope.Derived => InternalServerError($"\"{target.DisplayName}\" is derived from what already records it and has no stored balance, so it cannot be spent."),

            // Thrown rather than answered with a status. A scope with no arm here means an AssetStorageScope member was added and this switch was not
            // updated, which is a build-time mistake; returning it as a result would dress it up as something a request could legitimately provoke.
            _ => throw new UnreachableException($"\"{target.DisplayName}\" has storage scope {target.StorageScope}, which the drawdown has no arm for.")
        };
    }

    /// <summary>
    /// Works out what the expense names, in the order it would be spent, and which store routes it.
    /// </summary>
    /// <remarks>
    /// One arm per shape of <see cref="SpendTarget"/>, because the two share nothing but their return type: an Asset expense is a single catalogue row that
    /// routes on its own storage scope, and a group expense is an ordered set of them that always routes to a bank.
    /// </remarks>
    private static async Task<ServiceResult<ExpenseTarget>> ResolveTargetAsync(AscendantEmpireContext context, AssetExpense expense, CancellationToken cancelToken)
    {
        // A SpendTarget names exactly one of the two, its factories being the only way to build one, so the group arm is reached precisely when there is no
        // Asset id and the group id is there to be read.
        return expense.Target.AssetId is int assetId
            ? await ResolveAssetAsync(context, assetId, cancelToken)
            : await ResolveGroupAsync(context, expense.Target.AssetGroupId!.Value, cancelToken);
    }

    /// <summary>
    /// Resolves "10 Oak Logs" to the one catalogue Asset it names.
    /// </summary>
    private static async Task<ServiceResult<ExpenseTarget>> ResolveAssetAsync(AscendantEmpireContext context, int assetId, CancellationToken cancelToken)
    {
        TargetAsset? asset = await context.Assets
            .AsNoTracking()
            .Where(catalogueAsset => catalogueAsset.Id == assetId)
            .Select(catalogueAsset => new TargetAsset(
                catalogueAsset.Id,
                catalogueAsset.Code,
                catalogueAsset.DisplayName,
                catalogueAsset.StorageScope))
            .FirstOrDefaultAsync(cancelToken);

        if (asset is null)
        {
            return ServiceResult<ExpenseTarget>.NotFound($"No Asset with id {assetId} is in the catalogue.");
        }

        // The Asset's own name is what a refusal is phrased in: the caller asked for Oak Logs, so "Not enough Oak Log" is the honest message.
        return ServiceResult<ExpenseTarget>.Succeed(new ExpenseTarget(asset.DisplayName, asset.StorageScope, [asset]));
    }

    /// <summary>
    /// Resolves "10 Logs, any kind" to the group's bankable Assets, in the order they would be spent.
    /// </summary>
    /// <remarks>
    /// Always routes to a bank, whatever the group's Assets are: the group ordering is a rule about balances, and a group of Assets that are not banked has
    /// no balances to order.
    /// </remarks>
    private static async Task<ServiceResult<ExpenseTarget>> ResolveGroupAsync(AscendantEmpireContext context, int assetGroupId, CancellationToken cancelToken)
    {
        // The group's own name, because a group expense asked for Logs rather than for any particular log, and that is what the Player should be told it is short of.
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
            return ServiceResult<ExpenseTarget>.InternalServerError($"AssetGroup {assetGroupId} (\"{groupDisplayName}\") holds no Asset that is kept in a settlement bank, so it cannot be spent out of one.");
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
    /// Spends the expense out of the Settlement's own bank first, and the remainder out of the rest of its network.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Sufficiency is settled over the whole candidate set before anything is written, which is what makes a group expense all-or-nothing: the spend below
    /// only ever runs when the network's total is already known to cover the amount.
    /// </para>
    /// <para>
    /// <b>Local-first: the site's own bank is drained before any other is touched</b>, because goods already standing where the building is going up do not
    /// travel. The remainder comes out of the banks sharing the site's <c>NetworkKey</c>, levelled - see <see cref="LevelDown"/> - so it is taken from the
    /// richest and a poor Settlement keeps the stock its own construction will need.
    /// </para>
    /// <para>
    /// <b>The debit moves nothing and rebalances nothing.</b> No bank is topped up and the depleted site stays depleted. Levelling lives on the credit path,
    /// where next round's production favours the emptiest banks and re-levels the network over time - RS-38.
    /// </para>
    /// <para>
    /// <b>Asset preference outranks locality</b> - see <see cref="Spend"/>, which walks the Assets outermost. The site's Cedar is not taken before the
    /// network's Oak.
    /// </para>
    /// <para>
    /// An unkeyed bank reaches nothing but itself. <see cref="Guid.Empty"/> is the absence of a key rather than a network two banks share, so it is never
    /// grouped on.
    /// </para>
    /// <para>
    /// <b>The site's own bank id is prepended rather than read back out of the network query.</b> Nothing holds a snapshot across the two reads, so a re-key
    /// landing between them would otherwise drop the site out of its own network: the whole expense would come off the peers, which is local-first inverted
    /// and reported as a success.
    /// </para>
    /// <para>
    /// RS-27 adds the transport turn cost the distance implies, and it is the remainder ordering rather than the reaching that it turns into nearest-first,
    /// with the site at distance zero.
    /// </para>
    /// </remarks>
    private async Task<ServiceResultBase> DebitSettlementNetworkAsync(AscendantEmpireContext context, AssetExpense expense, ExpenseTarget target, CancellationToken cancelToken)
    {
        int settlementId = expense.SettlementId!.Value;

        // Both columns in the predicate, so naming another Player's Settlement cannot reach their bank.
        SiteBank? site = await context.AssetBanks
            .AsNoTracking()
            .Where(bank => bank.SettlementId == settlementId && bank.PlayerId == expense.PlayerId)
            .Select(bank => new SiteBank(bank.Id, bank.NetworkKey))
            .FirstOrDefaultAsync(cancelToken);

        if (site is null)
        {
            // Still a not-found however much the rest of the network holds: the expense happens at this Settlement, and one that cannot bank cannot be spent at.
            return NotFound($"Settlement {settlementId} has no asset bank belonging to Player {expense.PlayerId}, so nothing can be spent there.");
        }

        // PlayerId as well as the key, because the index is (PlayerId, NetworkKey) and a key alone would let a conquest caught mid-recompute reach a rival's stock.
        int[] peerBankIds = site.NetworkKey == Guid.Empty
            ? []
            : [.. await context.AssetBanks
                .AsNoTracking()
                .Where(bank => bank.PlayerId == expense.PlayerId && bank.NetworkKey == site.NetworkKey && bank.Id != site.AssetBankId)
                .Select(bank => bank.Id)
                .ToListAsync(cancelToken)];

        int[] networkBankIds = [site.AssetBankId, .. peerBankIds];

        int[] candidateAssetIds = [.. target.Assets.Select(asset => asset.AssetId)];

        // Tracked, unlike every other read here: these are the rows being written, and the caller's SaveChangesAsync is what commits them.
        List<AssetBankBalance> balances = await context.AssetBankBalances
            .Where(balance => networkBankIds.Contains(balance.AssetBankId) && candidateAssetIds.Contains(balance.AssetId))
            .ToListAsync(cancelToken);

        // A candidate with no row holds none of it, which is the same as a zero, so the sum needs no entry for it.
        ScaledAmount available = ScaledAmount.FromRaw(balances.Sum(balance => balance.Quantity));

        if (available < expense.Amount)
        {
            return ToShortfall(expense, target, available);
        }

        List<AssetBankBalance> siteBalances = [.. balances.Where(balance => balance.AssetBankId == site.AssetBankId)];
        List<AssetBankBalance> peerBalances = [.. balances.Where(balance => balance.AssetBankId != site.AssetBankId)];

        Spend(context, target, expense.Amount, [siteBalances, peerBalances]);

        // The peer count is what the remainder could have come from, not what it did: the site covers the whole expense often enough that naming the peers as
        // payers would misreport the common case.
        logger.LogInformation(
            "Debited {Amount} {Asset} for player {PlayerId} at settlement {SettlementId}, from its own asset bank {AssetBankId}, with {PeerBankCount} other bank(s) in its network available for the remainder.",
            expense.Amount,
            target.DisplayName,
            expense.PlayerId,
            settlementId,
            site.AssetBankId,
            peerBankIds.Length);

        return SuccessNoContent();
    }

    /// <summary>
    /// Spends the expense across every bank the Player owns, which is what an Empire-wide cost is paid out of.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A technology or an export belongs to no Settlement, so it is met out of everything the Player holds anywhere. Availability is the sum across every
    /// bank, and two banks that cannot reach each other are deliberately added together here - an Empire-wide expense is not goods travelling to a building
    /// site, so it carries no transport cost and no connectivity question.
    /// </para>
    /// <para>
    /// <b>The spread is levelling rather than proportional</b> - see <see cref="LevelDown"/>. The point of it is that a small bank keeps the stock its own
    /// construction will need, which is the stranding problem the Empire-wide arm would otherwise create every time it spent.
    /// </para>
    /// </remarks>
    private async Task<ServiceResultBase> DebitEveryBankAsync(AscendantEmpireContext context, AssetExpense expense, ExpenseTarget target, CancellationToken cancelToken)
    {
        int[] candidateAssetIds = [.. target.Assets.Select(asset => asset.AssetId)];

        // Filtered through the Bank navigation, so this is an INNER JOIN on a required foreign key and no other Player's balance is reachable. Tracked,
        // unlike every other read here: these are the rows being written, and the caller's SaveChangesAsync is what commits them.
        List<AssetBankBalance> balances = await context.AssetBankBalances
            .Where(balance => balance.Bank.PlayerId == expense.PlayerId && candidateAssetIds.Contains(balance.AssetId))
            .ToListAsync(cancelToken);

        ScaledAmount available = ScaledAmount.FromRaw(balances.Sum(balance => balance.Quantity));

        if (available < expense.Amount)
        {
            // A Player who owns no bank at all arrives here with nothing available, and that is a shortfall rather than a not-found: they are short of the
            // whole amount, and the all-banks read already treats owning no bank as an empty result rather than a missing one.
            return ToShortfall(expense, target, available);
        }

        Spend(context, target, expense.Amount, [balances]);

        logger.LogInformation(
            "Debited {Amount} {Asset} Empire-wide for player {PlayerId}, across the {BankCount} asset bank(s) holding any of it.",
            expense.Amount,
            target.DisplayName,
            expense.PlayerId,
            balances.Select(balance => balance.AssetBankId).Distinct().Count());

        return SuccessNoContent();
    }

    /// <summary>
    /// Takes the amount out of the balances handed over, walking the candidate Assets in spend order.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The walk is over Assets outermost, and each Asset is taken from the tiers in order before the next Asset is reached.</b> So a group expense takes
    /// every bank's Oak to nothing before any Cedar is touched anywhere, and a Settlement's own Cedar is not taken before its network's Oak: the Player's
    /// ordering says which Asset they are willing to lose first, and asset preference outranks locality.
    /// </para>
    /// <para>
    /// Only ever called once the caller has established that the balances cover the amount, which is what makes an expense all-or-nothing: there is no
    /// partial walk to unwind, because the walk cannot run short.
    /// </para>
    /// </remarks>
    /// <param name="tiers">
    /// The balance sets in locality order: each Asset comes out of each tier as far as it goes before the next is touched. A single tier is a set with no
    /// locality in it, which is what an Empire-wide expense spends out of.
    /// </param>
    private static void Spend(
        AscendantEmpireContext context,
        ExpenseTarget target,
        ScaledAmount amount,
        IReadOnlyList<IReadOnlyCollection<AssetBankBalance>> tiers)
    {
        List<Dictionary<int, List<AssetBankBalance>>> heldByAssetIdPerTier =
        [
            .. tiers.Select(tier => tier
                .GroupBy(balance => balance.AssetId)
                .ToDictionary(assetBalances => assetBalances.Key, assetBalances => assetBalances.ToList()))
        ];

        ScaledAmount remaining = amount;

        foreach (TargetAsset candidate in target.Assets)
        {
            foreach (Dictionary<int, List<AssetBankBalance>> heldByAssetId in heldByAssetIdPerTier)
            {
                if (remaining <= ScaledAmount.Zero)
                {
                    return;
                }

                if (heldByAssetId.TryGetValue(candidate.AssetId, out List<AssetBankBalance>? held) == false)
                {
                    continue;
                }

                remaining -= TakeOneAsset(context, held, remaining);
            }
        }
    }

    /// <summary>
    /// Takes as much of one Asset as the amount still calls for, and answers what it took.
    /// </summary>
    /// <remarks>
    /// Taking the Asset out entirely is the common case in a group walk and needs no levelling: a partial take only ever happens on the last Asset the walk
    /// reaches, which is the one the Player is left holding some of.
    /// </remarks>
    private static ScaledAmount TakeOneAsset(AscendantEmpireContext context, IReadOnlyList<AssetBankBalance> held, ScaledAmount remaining)
    {
        ScaledAmount total = ScaledAmount.FromRaw(held.Sum(balance => balance.Quantity));
        ScaledAmount taken = total <= remaining ? total : remaining;

        if (taken <= ScaledAmount.Zero)
        {
            return ScaledAmount.Zero;
        }

        if (taken == total)
        {
            foreach (AssetBankBalance balance in held)
            {
                balance.Quantity = 0;
            }
        }
        else
        {
            LevelDown(held, taken.RawValue);
        }

        // An emptied balance is deleted rather than left as a zero. A missing row and a zero row mean the same thing to every reader, and deleting keeps a
        // bank from accumulating a row per Asset it has ever briefly held.
        foreach (AssetBankBalance balance in held)
        {
            if (balance.Quantity == 0)
            {
                context.AssetBankBalances.Remove(balance);
            }
        }

        return taken;
    }

    /// <summary>
    /// Spreads a partial take across the balances by levelling: the fullest are cut down to a common water-line and the rest are left alone.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Chosen over a proportional spread, where a bank holding a tenth of the stock would pay a tenth of the cost. Levelling takes from the richest first,
    /// so a bank with little keeps what it has and stays able to fund its own construction.
    /// </para>
    /// <para>
    /// With banks holding 50, 30 and 4 and a take of 40, the water-line falls at 20: the first two are cut to 20 each and the third is untouched. A take of
    /// 84 out of the same banks is the whole of it and does not reach here at all.
    /// </para>
    /// <para>
    /// <b>The leftover units go richest-first, then to the lower bank id.</b> A quantity is a scaled integer, so a water-line that divides exactly is the
    /// exception and a few raw units are normally left over. The order is fixed rather than incidental, so that the same expense against the same banks
    /// always spends the same rows.
    /// </para>
    /// </remarks>
    /// <param name="takeRaw">How much to take, in raw scaled units, and strictly less than the balances hold between them.</param>
    private static void LevelDown(IReadOnlyList<AssetBankBalance> held, long takeRaw)
    {
        long waterLine = FindWaterLine(held, takeRaw);

        List<AssetBankBalance> inSpendOrder =
        [
            .. held
                .OrderByDescending(balance => balance.Quantity)
                .ThenBy(balance => balance.AssetBankId)
        ];

        // Everything strictly above the line comes off first. The line is the highest one that still yields enough, so what is left outstanding after this
        // pass is fewer units than there are balances standing on the line - never more.
        long[] deductions = new long[inSpendOrder.Count];
        long outstanding = takeRaw;

        for (int index = 0; index < inSpendOrder.Count; index++)
        {
            long aboveLine = Math.Max(0, inSpendOrder[index].Quantity - (waterLine + 1));

            deductions[index] = aboveLine;
            outstanding -= aboveLine;
        }

        // The remainder is one raw unit each off the balances standing on the line, richest first.
        for (int index = 0; index < inSpendOrder.Count && outstanding > 0; index++)
        {
            if (inSpendOrder[index].Quantity - deductions[index] <= waterLine)
            {
                continue;
            }

            deductions[index]++;
            outstanding--;
        }

        for (int index = 0; index < inSpendOrder.Count; index++)
        {
            inSpendOrder[index].Quantity -= deductions[index];
        }
    }

    /// <summary>
    /// The highest level at which cutting every balance down to it would still yield at least what is being taken.
    /// </summary>
    /// <remarks>
    /// Binary searched rather than walked, because a quantity is a scaled integer and the line can sit anywhere in a range of billions. What sits above a
    /// level only falls as the level rises, which is what makes the search sound: at zero it is everything the balances hold, which is more than is being
    /// taken, and at the fullest balance it is nothing, so the line lies between the two and the search cannot run off either end.
    /// </remarks>
    private static long FindWaterLine(IReadOnlyList<AssetBankBalance> held, long takeRaw)
    {
        long low = 0;
        long high = held.Max(balance => balance.Quantity);

        while (low < high)
        {
            // Rounded up, so a two-wide range tries the upper candidate and the loop cannot sit still on the lower one.
            long candidate = low + ((high - low + 1) / 2);

            if (SumAboveLevel(held, candidate) >= takeRaw)
            {
                low = candidate;
            }
            else
            {
                high = candidate - 1;
            }
        }

        return low;
    }

    /// <summary>
    /// How much the balances hold above the given level, which is what cutting every one of them down to it would yield.
    /// </summary>
    private static long SumAboveLevel(IReadOnlyList<AssetBankBalance> held, long level)
    {
        return held.Sum(balance => Math.Max(0, balance.Quantity - level));
    }

    /// <summary>
    /// Spends the expense out of the player-level store that already holds the Asset's figure.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Only a single-Asset expense reaches here, because a group expense is routed to a bank regardless of what its Assets are. Connectivity is irrelevant
    /// to a figure held once per Player for the whole game, and so is any Settlement.
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

        // A figure held once per Player for the whole game is not spent anywhere in particular, so a Settlement expense naming one is a caller that has the
        // two arms confused. Reported rather than quietly ignored, now that there is an Empire-wide arm which says what it means.
        if (expense.IsEmpireWide == false)
        {
            return InternalServerError($"\"{asset.DisplayName}\" is held per Player, so it is spent Empire-wide rather than at Settlement {expense.SettlementId!.Value}.");
        }

        if (string.Equals(asset.Code, PlayerStoreAssetCodes.TECHNOLOGY_EXPERIENCE, StringComparison.OrdinalIgnoreCase) == false)
        {
            return InternalServerError($"\"{asset.DisplayName}\" ({asset.Code}) is held per Player, but nothing stores it yet, so it cannot be spent.");
        }

        // The store counts whole points, so a fractional expense has nowhere to land. Rounding it either way would silently over- or under-charge, so the
        // caller is told it asked for something the store cannot express.
        if (expense.Amount.RemainderRaw() != 0)
        {
            return InternalServerError($"\"{asset.DisplayName}\" is counted in whole points, so {expense.Amount} cannot be spent of it.");
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
    /// <para>
    /// A conflict rather than a bad request: the expense was well formed and is simply at odds with what the Player currently holds, which is also why
    /// retrying it after a turn of production is a sensible thing for a caller to do.
    /// </para>
    /// <para>
    /// <b>Nothing in it is bank-specific, whichever arm refused.</b> An Empire-wide expense is short against everything the Player holds, and naming the
    /// pool that happened to run out would tell them about a boundary they did not spend across.
    /// </para>
    /// </remarks>
    private static ServiceResultBase ToShortfall(AssetExpense expense, ExpenseTarget target, ScaledAmount available)
    {
        ScaledAmount shortfall = expense.Amount - available;

        var detail = new AssetShortfall
        {
            AssetId = expense.Target.AssetId,
            AssetGroupId = expense.Target.AssetGroupId,
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

    /// <summary>
    /// The bank a Settlement expense is paid at, and the network that bank sits in.
    /// </summary>
    private sealed record SiteBank(int AssetBankId, Guid NetworkKey);
}
