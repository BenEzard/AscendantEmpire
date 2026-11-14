using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Maps.Depletion.Classes;
using AscendantEmpire.Application.Helpers.ScaledNumerics;
using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Enums.Maps;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AscendantEmpire.Application.Features.Maps.Depletion.Services;

/// <inheritdoc cref="IFoodConsumptionService" />
internal sealed class FoodConsumptionService(ILogger<FoodConsumptionService> logger) : IFoodConsumptionService
{
    public async Task ConsumeAsync(
        AscendantEmpireContext context,
        int gameId,
        IReadOnlyCollection<TileFoodConsumption> consumptions,
        CancellationToken cancelToken = default)
    {
        foreach (TileFoodConsumption consumption in consumptions)
        {
            foreach ((FoodSourceKey key, ScaledAmount taken) in consumption.Taken.Where(entry => entry.Key.SourceType == FoodSourceType.Tile))
            {
                DepletionTaken.AddTaken(consumption.Depletion, key.Resource!.Value, taken);
            }
        }

        var patchAmounts = consumptions
            .SelectMany(consumption => consumption.Taken)
            .Where(entry => entry.Key.SourceType == FoodSourceType.MapResource)
            .Where(entry => entry.Value > ScaledAmount.Zero)
            .ToList();

        if (patchAmounts.Count == 0)
        {
            return;
        }

        await LowerPatchQuantitiesAsync(context, gameId, patchAmounts, cancelToken);
    }

    /// <summary>
    /// Takes what was eaten off each patch's own quantity.
    /// </summary>
    /// <remarks>
    /// Tracked rather than projected, since these rows are about to be written. The amount eaten is an amount of an Asset, and a patch's quantity is in its
    /// own units, so each is divided back by the yield rate that produced it.
    /// </remarks>
    private async Task LowerPatchQuantitiesAsync(
        AscendantEmpireContext context,
        int gameId,
        IReadOnlyCollection<KeyValuePair<FoodSourceKey, ScaledAmount>> patchAmounts,
        CancellationToken cancelToken)
    {
        int[] instanceIds = [.. patchAmounts.Select(entry => entry.Key.MapResourceInstanceId!.Value).Distinct()];

        Dictionary<int, MapResourceInstance> instancesById = await context.MapResourceInstances
            .Where(instance => instance.GameId == gameId)
            .Where(instance => instanceIds.Contains(instance.Id))
            .ToDictionaryAsync(instance => instance.Id, cancelToken);

        int[] typeIds = [.. instancesById.Values.Select(instance => instance.MapResourceTypeId).Distinct()];

        Dictionary<(int TypeId, int AssetId), long> ratesByYield = await context.MapResourceTypeYields
            .Where(yield => typeIds.Contains(yield.MapResourceTypeId))
            .ToDictionaryAsync(yield => ValueTuple.Create(yield.MapResourceTypeId, yield.AssetId), yield => yield.AmountPerUnit, cancelToken);

        foreach ((FoodSourceKey key, ScaledAmount taken) in patchAmounts)
        {
            int instanceId = key.MapResourceInstanceId!.Value;

            if (instancesById.TryGetValue(instanceId, out MapResourceInstance? instance) == false
                || instance.Quantity is null
                || ratesByYield.TryGetValue((instance.MapResourceTypeId, key.AssetId), out long amountPerUnit) == false
                || amountPerUnit <= 0)
            {
                // The patch was read a moment ago, so this is a row removed under the feed or a yield unconfigured since. Charging the tile nothing is
                // better than refusing the Turn over it.
                logger.LogWarning(
                    "MapResource instance Id {MapResourceInstanceId} could no longer be charged {Taken} of Asset Id {AssetId} in Game Id {GameId}.",
                    instanceId,
                    taken.ToDecimal(),
                    key.AssetId,
                    gameId);

                continue;
            }

            long quantityTaken = (long)((Int128)taken.RawValue * ScaledAmount.Scale / amountPerUnit);

            instance.Quantity = Math.Max(0, instance.Quantity.Value - quantityTaken);
        }
    }
}
