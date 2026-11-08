using AscendantEmpire.Application.Data;
using AscendantEmpire.Domain.Entities.Games;

using Microsoft.EntityFrameworkCore;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.Maps.MapResources.Services;

/// <inheritdoc cref="IPlayerMapResourceDiscoveryService"/>
internal sealed class PlayerMapResourceDiscoveryService : IPlayerMapResourceDiscoveryService
{
    /// <summary>
    /// The Round anything known before the Game begins is stamped with, matching the Round its instances are created on.
    /// </summary>
    private const int _SETUP_ROUND = 0;

    /// <summary>
    /// The layer of a stratified instance a Player sees without surveying.
    /// </summary>
    /// <remarks>
    /// The switch rules make layer 1 on by default and every deeper layer off, so a Player who has surveyed nothing has still seen the surface.
    /// </remarks>
    private const int _SURFACE_LAYER = 1;

    public async Task<ServiceResult<int>> SeedDefaultDiscoveriesAsync(
        AscendantEmpireContext context,
        int gameId,
        IReadOnlyCollection<int> playerIds,
        CancellationToken cancellationToken)
    {
        if (playerIds.Count == 0)
        {
            return ServiceResult<int>.Succeed(0);
        }

        List<InstanceToSeed> instances = await ReadDefaultDiscoverableAsync(context, gameId, cancellationToken);

        // A Game whose map carries no MapResources, or only hidden ones, is a success with nothing to record.
        if (instances.Count == 0)
        {
            return ServiceResult<int>.Succeed(0);
        }

        List<PlayerKnownMapResource> rows = [];

        foreach (InstanceToSeed instance in instances)
        {
            foreach (int playerId in playerIds)
            {
                rows.Add(new PlayerKnownMapResource
                {
                    GameId = gameId,
                    PlayerId = playerId,
                    MapResourceInstanceId = instance.InstanceId,
                    DiscoveredOnRound = _SETUP_ROUND,
                    DeepestLayerSurveyed = instance.IsStratified ? _SURFACE_LAYER : null
                });
            }
        }

        context.PlayerKnownMapResources.AddRange(rows);
        await context.SaveChangesAsync(cancellationToken);

        return ServiceResult<int>.Succeed(rows.Count);
    }

    /// <summary>
    /// The Game's instances whose type needs no finding, each saying whether it has layers.
    /// </summary>
    /// <remarks>
    /// Query syntax for the join: an instance carries no navigation to its type, so this is a genuine join between two tables rather than a projection
    /// through a mapped relationship. The same join is written the same way in <c>MapResourceInstantiationService.ReadPlacementsAsync</c>.
    /// </remarks>
    private static async Task<List<InstanceToSeed>> ReadDefaultDiscoverableAsync(AscendantEmpireContext context, int gameId, CancellationToken cancellationToken)
    {
        return await (
            from instance in context.MapResourceInstances.AsNoTracking()
            join type in context.MapResourceTypes on instance.MapResourceTypeId equals type.Id
            where instance.GameId == gameId && type.IsDiscoverableByDefault
            select new InstanceToSeed(instance.Id, type.IsStratified))
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// One instance every Player starts out knowing, and whether its type holds its quantity in layers.
    /// </summary>
    private sealed record InstanceToSeed(int InstanceId, bool IsStratified);
}
