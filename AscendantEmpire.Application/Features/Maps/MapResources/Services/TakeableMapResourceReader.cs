using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Maps.Depletion.Services;
using AscendantEmpire.Application.Features.Maps.MapResources.Classes;
using AscendantEmpire.Domain.Enums.Assets;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AscendantEmpire.Application.Features.Maps.MapResources.Services;

/// <inheritdoc cref="ITakeableMapResourceReader" />
internal sealed class TakeableMapResourceReader(ILogger<TakeableMapResourceReader> logger) : ITakeableMapResourceReader
{
    public async Task<IReadOnlyDictionary<int, IReadOnlyCollection<TakeableMapResource>>> GetForTilesAsync(
        AscendantEmpireContext context,
        int gameId,
        int playerId,
        IReadOnlyCollection<int> tileIds,
        CancellationToken cancelToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(tileIds);

        int[] distinctTileIds = [.. tileIds.Distinct()];

        if (distinctTileIds.Length == 0)
        {
            return new Dictionary<int, IReadOnlyCollection<TakeableMapResource>>();
        }

        // An instance has no navigation to its type, so the type and its yields are joined rather than projected through one - the same shape
        // TileFoodStockReader reads a patch in. An instance whose type yields nothing drops out of the inner join, which is right: there is no Asset to
        // take. A Distribution and a stratified instance are excluded by the null-quantity filter, since both hold their amounts in child rows.
        var rows = await context.MapResourceInstances
            .AsNoTracking()
            .Where(instance => instance.GameId == gameId)
            .Where(instance => distinctTileIds.Contains(instance.CampaignMapId))
            .Where(instance => instance.Quantity != null && instance.Quantity > 0)
            // Discovered by this Player. A join rather than an Any(), so the whole read stays one query.
            .Join(
                context.PlayerKnownMapResources.Where(known => known.GameId == gameId && known.PlayerId == playerId),
                instance => instance.Id,
                known => known.MapResourceInstanceId,
                (instance, known) => instance)
            .Join(
                context.MapResourceTypes,
                instance => instance.MapResourceTypeId,
                type => type.Id,
                (instance, type) => new { Instance = instance, Type = type })
            .Join(
                context.MapResourceTypeYields,
                row => row.Type.Id,
                yield => yield.MapResourceTypeId,
                (row, yield) => new { row.Instance, row.Type, Yield = yield })
            .Join(
                context.Assets,
                row => row.Yield.AssetId,
                asset => asset.Id,
                (row, asset) => new
                {
                    row.Instance.CampaignMapId,
                    InstanceId = row.Instance.Id,
                    row.Instance.MapResourceTypeId,
                    row.Instance.Slot,
                    row.Instance.Quantity,
                    row.Type.DisplayName,
                    row.Type.IconPath,
                    row.Type.RequiresResearchToExtract,
                    row.Yield.AssetId,
                    row.Yield.AmountPerUnit,
                    row.Yield.IsEnabledByDefault,
                    AssetDisplayName = asset.DisplayName,
                    asset.StorageScope
                })
            .ToListAsync(cancelToken);

        if (rows.Count == 0)
        {
            return new Dictionary<int, IReadOnlyCollection<TakeableMapResource>>();
        }

        IReadOnlyDictionary<(int TypeId, int AssetId), bool> switchesByYield =
            await MapResourceYieldSwitches.ReadAsync(context, gameId, playerId, cancelToken);

        // Only needed for the types that actually say they need it, so a catalogue of freely gathered resources costs no second query.
        int[] researchGatedTypeIds = [.. rows.Where(row => row.RequiresResearchToExtract).Select(row => row.MapResourceTypeId).Distinct()];

        HashSet<int> researchedTypeIds = researchGatedTypeIds.Length == 0
            ? []
            : [.. await context.PlayerMapResourceTypeKnowledge
                .Where(knowledge => knowledge.GameId == gameId)
                .Where(knowledge => knowledge.PlayerId == playerId)
                .Where(knowledge => researchGatedTypeIds.Contains(knowledge.MapResourceTypeId))
                .Where(knowledge => knowledge.ResearchedOnRound != null)
                .Select(knowledge => knowledge.MapResourceTypeId)
                .ToListAsync(cancelToken)];

        var takeable = new Dictionary<int, List<TakeableMapResource>>();

        foreach (var instanceGroup in rows.GroupBy(row => new { row.CampaignMapId, row.InstanceId }))
        {
            var first = instanceGroup.First();

            if (first.RequiresResearchToExtract && !researchedTypeIds.Contains(first.MapResourceTypeId))
            {
                continue;
            }

            List<TakeableMapResourceYield> yields = [];

            foreach (var row in instanceGroup.OrderBy(row => row.AssetId))
            {
                bool isEnabled = switchesByYield.TryGetValue((row.MapResourceTypeId, row.AssetId), out bool switched)
                    ? switched
                    : row.IsEnabledByDefault;

                if (!isEnabled || row.AmountPerUnit <= 0)
                {
                    continue;
                }

                // A balance row can only hold an Asset kept in a bank. A player-level or derived figure has its number elsewhere entirely, so crediting one
                // into cargo would be writing a second source of truth - see AssetBankBalance. Authoring one onto a yield is a catalogue fault.
                if (row.StorageScope != AssetStorageScope.SettlementBank)
                {
                    logger.LogWarning(
                        "MapResource Type Id {MapResourceTypeId} yields Asset Id {AssetId}, whose storage scope is {StorageScope}, so it cannot be carried as cargo and was left on the tile.",
                        row.MapResourceTypeId, row.AssetId, row.StorageScope);

                    continue;
                }

                yields.Add(new TakeableMapResourceYield(row.AssetId, row.AssetDisplayName, row.AmountPerUnit));
            }

            // Nothing survived the switches, so taking this instance would strip the tile and hand the Force nothing. Not takeable.
            if (yields.Count == 0)
            {
                continue;
            }

            if (!takeable.TryGetValue(first.CampaignMapId, out List<TakeableMapResource>? onTile))
            {
                onTile = [];
                takeable[first.CampaignMapId] = onTile;
            }

            onTile.Add(new TakeableMapResource(
                first.InstanceId,
                first.MapResourceTypeId,
                first.Slot,
                first.DisplayName,
                first.IconPath,
                // Non-null: the query keeps only instances holding a quantity of their own.
                first.Quantity!.Value,
                yields));
        }

        // By slot, so the dialog lists a tile's two resources the way the tile itself is drawn rather than in whatever order the join returned them.
        return takeable.ToDictionary(
            tile => tile.Key,
            tile => (IReadOnlyCollection<TakeableMapResource>)[.. tile.Value.OrderBy(resource => resource.Slot)]);
    }
}
