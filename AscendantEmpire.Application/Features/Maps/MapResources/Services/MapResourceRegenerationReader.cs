using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Maps.MapResources.Classes;

using Microsoft.EntityFrameworkCore;

namespace AscendantEmpire.Application.Features.Maps.MapResources.Services;

/// <inheritdoc cref="IMapResourceRegenerationReader" />
internal sealed class MapResourceRegenerationReader : IMapResourceRegenerationReader
{
    /// <inheritdoc />
    public async Task<IReadOnlyCollection<RenewableHabitat>> GetRenewableHabitatConfigAsync(AscendantEmpireContext context, CancellationToken cancelToken = default)
    {
        // A projection, so nothing is tracked and the habitats need no Include.
        return await context.MapResourceTypes
            .Where(type => type.IsRenewable)
            .SelectMany(type => type.Habitats, (type, habitat) => new RenewableHabitat
            {
                MapResourceTypeId = type.Id,
                IsDistribution = type.IsDistribution,
                EstimationFuzzyFactorMin = type.EstimationFuzzyFactorMin,
                EstimationFuzzyFactorMax = type.EstimationFuzzyFactorMax,
                BiomeTypeId = habitat.BiomeTypeId,
                CampaignMapGeographyTypeId = habitat.CampaignMapGeographyTypeId,
                // Required on a renewable habitat, so a null is a broken catalogue and should throw rather than default to a rule.
                MaximumQuantity = habitat.MaximumQuantity!.Value,
                GrowthPercentage = habitat.GrowthPercentage!.Value,
                SpreadLikelihood = habitat.SpreadLikelihood
            })
            .ToListAsync(cancelToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyCollection<MapResourceRegenerationInstance>> GetMapResourceInstancesAsync(AscendantEmpireContext context, int gameId, CancellationToken cancelToken = default)
    {
        // Query syntax for the joins: an instance has no navigation to its tile or its type.
        return await (
            from instance in context.MapResourceInstances
            join tile in context.CampaignMaps on instance.CampaignMapId equals tile.CampaignMapId
            join type in context.MapResourceTypes on instance.MapResourceTypeId equals type.Id
            where instance.GameId == gameId && type.IsRenewable
            select new MapResourceRegenerationInstance
            {
                MapResourceInstanceId = instance.Id,
                MapResourceTypeId = instance.MapResourceTypeId,
                XCoord = tile.XCoord,
                YCoord = tile.YCoord,
                BiomeTypeId = tile.BiomeTypeId,
                CampaignMapGeographyTypeId = tile.CampaignMapGeographyTypeId,
                Quantity = instance.Quantity,
                SpreadLimit = instance.SpreadLimit,
                DormantSinceRound = instance.DormantSinceRound,
                // Required on a renewable type, so a null is a broken catalogue and should throw rather than default to a rule.
                SurvivalRounds = type.SurvivalRounds!.Value,
                ReseedLikelihood = type.ReseedLikelihood!.Value,
                InstanceAmounts = context.MapResourceInstanceAmounts
                    .Where(amount => amount.MapResourceInstanceId == instance.Id)
                    .Select(amount => new DistributedQuantity { AssetId = amount.AssetId, Amount = amount.Amount })
                    .ToList()
            })
            .ToListAsync(cancelToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<int, SpreadTargetTile>> GetSpreadTargetsAsync(AscendantEmpireContext context, int gameId, IReadOnlyCollection<int> campaignMapIds, CancellationToken cancelToken = default)
    {
        if (campaignMapIds.Count == 0)
        {
            return new Dictionary<int, SpreadTargetTile>();
        }

        // An instance has no navigation to its tile, so the filled slots are a correlated subquery.
        return await context.CampaignMaps
            .Where(tile => tile.GameId == gameId && campaignMapIds.Contains(tile.CampaignMapId))
            .Select(tile => new SpreadTargetTile
            {
                CampaignMapId = tile.CampaignMapId,
                XCoord = tile.XCoord,
                YCoord = tile.YCoord,
                BiomeTypeId = tile.BiomeTypeId,
                CampaignMapGeographyTypeId = tile.CampaignMapGeographyTypeId,
                FilledSlots = context.MapResourceInstances
                    .Where(instance => instance.CampaignMapId == tile.CampaignMapId)
                    .Select(instance => new SpreadTargetFilledSlot
                    {
                        MapResourceInstanceId = instance.Id,
                        MapResourceTypeId = instance.MapResourceTypeId,
                        Slot = instance.Slot,
                        Quantity = instance.Quantity
                    })
                    .ToList()
            })
            .ToDictionaryAsync(tile => tile.CampaignMapId, cancelToken);
    }

    /// <inheritdoc />
    public async Task<long?> GetGameSeedAsync(AscendantEmpireContext context, int gameId, CancellationToken cancelToken = default)
    {
        return await context.GameIndex
            .Where(game => game.Id == gameId)
            .Select(game => (long?)game.GenerationSeed)
            .SingleOrDefaultAsync(cancelToken);
    }
}