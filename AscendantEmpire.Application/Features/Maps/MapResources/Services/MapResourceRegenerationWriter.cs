using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Maps.MapResources.Classes;
using AscendantEmpire.Domain.Entities.Games;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace AscendantEmpire.Application.Features.Maps.MapResources.Services;

/// <inheritdoc cref="IMapResourceRegenerationWriter" />
internal sealed class MapResourceRegenerationWriter : IMapResourceRegenerationWriter
{
    /// <inheritdoc />
    public async Task WriteAsync(
        AscendantEmpireContext context, 
        int gameId, 
        int currentRoundNumber, 
        MapResourceRegenerationPlan plan, 
        CancellationToken cancelToken = default)
    {
        if (plan is { InstanceUpdates.Count: 0, AmountUpdates.Count: 0, DeletedMapResourceInstanceIds.Count: 0, Creations.Count: 0 })
        {
            return;
        }

        Dictionary<int, List<int>> discoverersBySpreader = await ReadDiscoverersAsync(context, [.. plan.Creations.Select(creation => creation.SourceMapResourceInstanceId).Distinct()], cancelToken);

        // Two saves have to land together: the created instances, to learn their ids, then the discoveries carrying them. A caller's transaction stays theirs to commit.
        await using IDbContextTransaction? ownTransaction = context.Database.CurrentTransaction is null
            ? await context.Database.BeginTransactionAsync(cancelToken)
            : null;

        await DeleteInstancesAsync(context, plan.DeletedMapResourceInstanceIds, cancelToken);

        AttachInstanceUpdates(context, plan.InstanceUpdates);
        AttachAmountUpdates(context, plan.AmountUpdates);

        List<(SpreadCreation Creation, MapResourceInstance Row)> created = [.. plan.Creations.Select(creation => (creation, ToInstance(gameId, currentRoundNumber, creation)))];
        context.MapResourceInstances.AddRange(created.Select(pair => pair.Row));

        await context.SaveChangesAsync(cancelToken);

        context.PlayerKnownMapResources.AddRange(created.SelectMany(pair => discoverersBySpreader
            .GetValueOrDefault(pair.Creation.SourceMapResourceInstanceId, [])
            .Select(playerId => new PlayerKnownMapResource
            {
                GameId = gameId,
                PlayerId = playerId,
                MapResourceInstanceId = pair.Row.Id,
                DiscoveredOnRound = currentRoundNumber
            })));

        await context.SaveChangesAsync(cancelToken);

        if (ownTransaction is not null)
        {
            await ownTransaction.CommitAsync(cancelToken);
        }
    }

    /// <summary>
    /// The Players who have discovered each spreading instance, keyed by the spreader's <c>MapResourceInstanceId</c>.
    /// </summary>
    private static async Task<Dictionary<int, List<int>>> ReadDiscoverersAsync(AscendantEmpireContext context, IReadOnlyCollection<int> spreaderIds, CancellationToken cancelToken)
    {
        if (spreaderIds.Count == 0)
        {
            return [];
        }

        var discoveries = await context.PlayerKnownMapResources
            .AsNoTracking()
            .Where(known => spreaderIds.Contains(known.MapResourceInstanceId))
            .Select(known => new { known.MapResourceInstanceId, known.PlayerId })
            .ToListAsync(cancelToken);

        return discoveries
            .GroupBy(known => known.MapResourceInstanceId)
            .ToDictionary(group => group.Key, group => group.Select(known => known.PlayerId).ToList());
    }

    /// <summary>
    /// Deletes the instances gone for good, children first since the foreign keys do not cascade.
    /// </summary>
    /// <remarks>
    /// No layers are deleted: a renewable type is never stratified.
    /// </remarks>
    private static async Task DeleteInstancesAsync(AscendantEmpireContext context, IReadOnlyCollection<int> instanceIds, CancellationToken cancelToken)
    {
        if (instanceIds.Count == 0)
        {
            return;
        }

        await context.PlayerKnownMapResources.Where(known => instanceIds.Contains(known.MapResourceInstanceId)).ExecuteDeleteAsync(cancelToken);
        await context.MapResourceInstanceAmounts.Where(amount => instanceIds.Contains(amount.MapResourceInstanceId)).ExecuteDeleteAsync(cancelToken);
        await context.MapResourceInstances.Where(instance => instanceIds.Contains(instance.Id)).ExecuteDeleteAsync(cancelToken);
    }

    /// <summary>
    /// Attaches a stub per updated instance with its regenerated columns marked modified, so only those columns are written and nothing is read back first.
    /// </summary>
    /// <remarks>
    /// Marked explicitly rather than detected, because a column going back to <see langword="null"/> matches the stub's default and would otherwise be missed.
    /// </remarks>
    private static void AttachInstanceUpdates(AscendantEmpireContext context, IReadOnlyCollection<RegenerationInstanceUpdate> updates)
    {
        foreach (RegenerationInstanceUpdate update in updates)
        {
            MapResourceInstance stub = new()
            {
                Id = update.MapResourceInstanceId,
                Quantity = update.Quantity,
                SpreadLimit = update.SpreadLimit,
                DormantSinceRound = update.DormantSinceRound
            };

            context.MapResourceInstances.Attach(stub);
            context.Entry(stub).Property(instance => instance.Quantity).IsModified = true;
            context.Entry(stub).Property(instance => instance.SpreadLimit).IsModified = true;
            context.Entry(stub).Property(instance => instance.DormantSinceRound).IsModified = true;
        }
    }

    private static void AttachAmountUpdates(AscendantEmpireContext context, IReadOnlyCollection<RegenerationAmountUpdate> updates)
    {
        foreach (RegenerationAmountUpdate update in updates)
        {
            MapResourceInstanceAmount stub = new()
            {
                MapResourceInstanceId = update.MapResourceInstanceId,
                AssetId = update.AssetId,
                Amount = update.Amount
            };

            context.MapResourceInstanceAmounts.Attach(stub);
            context.Entry(stub).Property(amount => amount.Amount).IsModified = true;
        }
    }

    private static MapResourceInstance ToInstance(int gameId, int currentRoundNumber, SpreadCreation creation)
    {
        return new MapResourceInstance
        {
            GameId = gameId,
            CampaignMapId = creation.CampaignMapId,
            MapResourceTypeId = creation.MapResourceTypeId,
            Slot = creation.Slot,
            Quantity = creation.Quantity,
            SpreadLimit = creation.SpreadLimit,
            EstimationFuzzyFactor = creation.EstimationFuzzyFactor,
            CreatedOnRound = currentRoundNumber
        };
    }
}
