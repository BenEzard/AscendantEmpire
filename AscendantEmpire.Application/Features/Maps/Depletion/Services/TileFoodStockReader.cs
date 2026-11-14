using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Maps.Depletion.Classes;
using AscendantEmpire.Application.Helpers.ScaledNumerics;
using AscendantEmpire.Domain.Enums.MapResources;

using Microsoft.EntityFrameworkCore;

namespace AscendantEmpire.Application.Features.Maps.Depletion.Services;

/// <inheritdoc cref="ITileFoodStockReader" />
internal sealed class TileFoodStockReader : ITileFoodStockReader
{
    public async Task<IReadOnlyDictionary<int, IReadOnlyCollection<TileMapResourceStock>>> GetStocksAsync(
        AscendantEmpireContext context,
        int gameId,
        int playerId,
        IReadOnlyCollection<int> tileIds,
        CancellationToken cancelToken = default)
    {
        int[] distinctTileIds = [.. tileIds.Distinct()];

        if (distinctTileIds.Length == 0)
        {
            return new Dictionary<int, IReadOnlyCollection<TileMapResourceStock>>();
        }

        // An instance has no navigation to its type, so the yields are joined rather than projected through one. An instance whose type yields nothing drops
        // out, which is what an inner join should do here: there is no Asset to eat.
        var rows = await context.MapResourceInstances
            .Where(instance => instance.GameId == gameId)
            .Where(instance => distinctTileIds.Contains(instance.CampaignMapId))
            .Join(
                context.MapResourceTypeYields,
                instance => instance.MapResourceTypeId,
                yield => yield.MapResourceTypeId,
                (instance, yield) => new
                {
                    instance.CampaignMapId,
                    InstanceId = instance.Id,
                    instance.MapResourceTypeId,
                    instance.Quantity,
                    yield.AssetId,
                    yield.AmountPerUnit,
                    yield.IsEnabledByDefault
                })
            .ToListAsync(cancelToken);

        if (rows.Count == 0)
        {
            return new Dictionary<int, IReadOnlyCollection<TileMapResourceStock>>();
        }

        IReadOnlyDictionary<(int TypeId, int AssetId), bool> switchesByYield = await ReadYieldSwitchesAsync(context, gameId, playerId, cancelToken);

        return rows
            .GroupBy(row => row.CampaignMapId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyCollection<TileMapResourceStock>)
                [
                    .. group.Select(row => new TileMapResourceStock(
                        row.InstanceId,
                        row.AssetId,
                        ToAssetAmount(row.Quantity, row.AmountPerUnit),
                        switchesByYield.TryGetValue((row.MapResourceTypeId, row.AssetId), out bool isEnabled) ? isEnabled : row.IsEnabledByDefault))
                ]);
    }

    /// <summary>
    /// How much of the Asset an instance's quantity amounts to, or <see langword="null"/> when it holds its amount in child rows instead.
    /// </summary>
    /// <remarks>
    /// Both figures are raw fixed-point, so the scale has to come back out of their product.
    /// </remarks>
    private static ScaledAmount? ToAssetAmount(long? quantity, long amountPerUnit)
    {
        if (quantity is null)
        {
            return null;
        }

        return ScaledAmount.FromRaw((long)((Int128)quantity.Value * amountPerUnit / ScaledAmount.Scale));
    }

    /// <summary>
    /// What the Player's switches say about each yield they hold a row for, an off beating an on as <c>PlayerMapResourceSwitch</c> resolves them.
    /// </summary>
    private static async Task<IReadOnlyDictionary<(int TypeId, int AssetId), bool>> ReadYieldSwitchesAsync(
        AscendantEmpireContext context,
        int gameId,
        int playerId,
        CancellationToken cancelToken)
    {
        var switches = await context.PlayerMapResourceSwitches
            .Where(playerSwitch => playerSwitch.GameId == gameId)
            .Where(playerSwitch => playerSwitch.PlayerId == playerId)
            .Where(playerSwitch => playerSwitch.Kind == MapResourceSwitchKind.YieldAsset)
            .Where(playerSwitch => playerSwitch.AssetId != null)
            .Select(playerSwitch => new { playerSwitch.MapResourceTypeId, playerSwitch.AssetId, playerSwitch.IsEnabled })
            .ToListAsync(cancelToken);

        return switches
            .GroupBy(playerSwitch => (playerSwitch.MapResourceTypeId, AssetId: playerSwitch.AssetId!.Value))
            .ToDictionary(group => group.Key, group => group.All(playerSwitch => playerSwitch.IsEnabled));
    }
}
