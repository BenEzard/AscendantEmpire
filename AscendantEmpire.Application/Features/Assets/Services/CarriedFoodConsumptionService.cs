using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Assets.Classes;
using AscendantEmpire.Application.Helpers.ScaledNumerics;
using AscendantEmpire.Domain.Entities.Assets;
using AscendantEmpire.Domain.Enums.Assets;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AscendantEmpire.Application.Features.Assets.Services;

/// <inheritdoc cref="ICarriedFoodConsumptionService" />
internal sealed class CarriedFoodConsumptionService(ILogger<CarriedFoodConsumptionService> logger) : ICarriedFoodConsumptionService
{
    public async Task<IReadOnlyDictionary<int, ScaledAmount>> ConsumeAsync(
        AscendantEmpireContext context,
        int playerId,
        IReadOnlyCollection<CarriedFoodDraw> draws,
        IReadOnlyDictionary<int, IReadOnlyDictionary<int, long>> amountsPerUnitBySetId,
        CancellationToken cancelToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(draws);
        ArgumentNullException.ThrowIfNull(amountsPerUnitBySetId);

        var fedByForceId = new Dictionary<int, ScaledAmount>();

        // Force id order, so where two Forces share a network the same one always eats first.
        List<CarriedFoodDraw> wanting =
        [
            .. draws
                .Where(draw => draw.Shortfall > ScaledAmount.Zero)
                .OrderBy(draw => draw.UnitInstanceId)
        ];

        if (wanting.Count == 0)
        {
            return fedByForceId;
        }

        IReadOnlyCollection<AssetBank> banks = await ReadBanksAsync(context, playerId, [.. wanting.Select(draw => draw.UnitInstanceId)], cancelToken);

        if (banks.Count == 0)
        {
            return fedByForceId;
        }

        // One bank per Unit is a schema rule, so a Force's own load is a single row rather than a set to reconcile.
        Dictionary<int, AssetBank> banksByUnitId = banks
            .Where(bank => bank.UnitInstanceId is not null)
            .ToDictionary(bank => bank.UnitInstanceId!.Value);

        Dictionary<Guid, List<AssetBank>> banksByNetworkKey = banks
            .Where(bank => bank.NetworkKey != Guid.Empty)
            .GroupBy(bank => bank.NetworkKey)
            .ToDictionary(group => group.Key, group => group.OrderBy(bank => bank.Id).ToList());

        foreach (CarriedFoodDraw draw in wanting)
        {
            if (banksByUnitId.TryGetValue(draw.UnitInstanceId, out AssetBank? ownBank) == false)
            {
                // A Unit with no bank is carrying nothing. Most Units never carry anything, so this is the ordinary case rather than a fault.
                continue;
            }

            IReadOnlyDictionary<int, long> amountPerUnitByAssetId = amountsPerUnitBySetId.GetValueOrDefault(
                draw.FoodSupplySetId,
                new Dictionary<int, long>());

            if (amountPerUnitByAssetId.Count == 0)
            {
                // A set holding no Assets feeds nobody, so nothing the Force carries is food to it.
                continue;
            }

            ScaledAmount remaining = draw.Shortfall;
            ScaledAmount fed = ScaledAmount.Zero;

            foreach (IReadOnlyCollection<AssetBank> tier in LocalityTiers(ownBank, banksByNetworkKey))
            {
                if (remaining <= ScaledAmount.Zero)
                {
                    break;
                }

                ScaledAmount fromTier = EatFromTier(context, tier, amountPerUnitByAssetId, remaining);

                fed += fromTier;
                remaining -= fromTier;
            }

            if (fed > ScaledAmount.Zero)
            {
                fedByForceId.Add(draw.UnitInstanceId, fed);
            }
        }

        if (fedByForceId.Count > 0)
        {
            logger.LogInformation(
                "Fed {ForceCount} Force(s) belonging to Player Id {PlayerId} out of what they were carrying: {TotalFed} ration(s).",
                fedByForceId.Count,
                playerId,
                fedByForceId.Values.Aggregate(ScaledAmount.Zero, (sum, fed) => sum + fed).ToDecimal());
        }

        return fedByForceId;
    }

    /// <summary>
    /// Every cargo bank the Forces can reach: their own, and any other of the Player's sharing a network with one of them.
    /// </summary>
    /// <remarks>
    /// Tracked and with the balances, because these rows are about to be written. The keys are read first rather than joined, so that an unkeyed bank - a
    /// <see cref="Guid.Empty"/> key is the absence of a network - cannot gather up every other unkeyed load the Player owns.
    /// </remarks>
    private static async Task<IReadOnlyCollection<AssetBank>> ReadBanksAsync(
        AscendantEmpireContext context,
        int playerId,
        IReadOnlyCollection<int> unitInstanceIds,
        CancellationToken cancelToken)
    {
        int?[] feedingUnitIds = [.. unitInstanceIds.Distinct().Select(id => (int?)id)];

        Guid[] networkKeys = await context.AssetBanks
            .Where(bank => bank.PlayerId == playerId)
            .Where(bank => bank.Kind == AssetBankKind.Unit)
            .Where(bank => feedingUnitIds.Contains(bank.UnitInstanceId))
            .Where(bank => bank.NetworkKey != Guid.Empty)
            .Select(bank => bank.NetworkKey)
            .Distinct()
            .ToArrayAsync(cancelToken);

        return await context.AssetBanks
            .Include(bank => bank.Balances)
            .Where(bank => bank.PlayerId == playerId)
            .Where(bank => bank.Kind == AssetBankKind.Unit)
            .Where(bank => feedingUnitIds.Contains(bank.UnitInstanceId) || networkKeys.Contains(bank.NetworkKey))
            .ToListAsync(cancelToken);
    }

    /// <summary>
    /// The banks a Force eats from, nearest first: its own load, and then the loads of the Units sharing its network.
    /// </summary>
    /// <remarks>
    /// The same locality ordering a Settlement's expense uses. A Force eats what it is carrying itself before a sibling is asked for anything, so a Unit
    /// cannot be stripped while the Force beside it still has rations of its own.
    /// </remarks>
    private static IEnumerable<IReadOnlyCollection<AssetBank>> LocalityTiers(AssetBank ownBank, IReadOnlyDictionary<Guid, List<AssetBank>> banksByNetworkKey)
    {
        yield return [ownBank];

        if (ownBank.NetworkKey == Guid.Empty || banksByNetworkKey.TryGetValue(ownBank.NetworkKey, out List<AssetBank>? network) == false)
        {
            yield break;
        }

        List<AssetBank> siblings = [.. network.Where(bank => bank.Id != ownBank.Id)];

        if (siblings.Count > 0)
        {
            yield return siblings;
        }
    }

    /// <summary>
    /// Eats as much of the need as one tier of banks can cover, and answers the rations it yielded.
    /// </summary>
    /// <remarks>
    /// The two axes of levelling meet here: <see cref="RationLeveller"/> decides how much of each Asset is eaten by levelling them against each other in
    /// rations, and <see cref="BalanceLeveller"/> then spreads each Asset's share across the banks of the tier holding it.
    /// </remarks>
    private static ScaledAmount EatFromTier(
        AscendantEmpireContext context,
        IReadOnlyCollection<AssetBank> tier,
        IReadOnlyDictionary<int, long> amountPerUnitByAssetId,
        ScaledAmount need)
    {
        Dictionary<int, List<AssetBankBalance>> heldByAssetId = tier
            .SelectMany(bank => bank.Balances)
            .Where(balance => balance.Quantity > 0)
            .Where(balance => amountPerUnitByAssetId.ContainsKey(balance.AssetId))
            .GroupBy(balance => balance.AssetId)
            .ToDictionary(group => group.Key, group => group.ToList());

        if (heldByAssetId.Count == 0)
        {
            return ScaledAmount.Zero;
        }

        CarriedRation[] carried =
        [
            .. heldByAssetId.Select(entry => new CarriedRation(
                entry.Key,
                entry.Value.Sum(balance => balance.Quantity),
                amountPerUnitByAssetId[entry.Key]))
        ];

        RationTake take = RationLeveller.Level(carried, need);

        foreach ((int assetId, long takeRaw) in take.TakeRawByAssetId)
        {
            List<AssetBankBalance> held = heldByAssetId[assetId];

            BalanceLeveller.Take(held, takeRaw);

            // An emptied balance is deleted rather than left as a zero, as every other debit does: a missing row and a zero row mean the same thing to every
            // reader. ReservedQuantity is checked as well, because a reserve is the one thing a row can hold that eating never reaches.
            foreach (AssetBankBalance balance in held.Where(balance => balance.Quantity == 0 && balance.ReservedQuantity == 0))
            {
                context.AssetBankBalances.Remove(balance);
            }
        }

        return take.Fed;
    }
}
