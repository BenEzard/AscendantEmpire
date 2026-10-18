using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Maps.CampaignMaps.Classes;
using AscendantEmpire.BackendContracts.CampaignMaps.GetPlayerCampaignMap;

using Microsoft.EntityFrameworkCore;

namespace AscendantEmpire.Application.Features.Maps.CampaignMaps.Services;

/// <inheritdoc cref="ICampaignOwnershipReader"/>
internal class CampaignOwnershipReader : ICampaignOwnershipReader
{
    /// <summary>No claimants, for the overwhelming majority of tiles that have no claim rows at all.</summary>
    private static readonly int[] _NO_CLAIMANTS = [];

    /// <inheritdoc/>
    public async Task<IReadOnlyDictionary<int, TileOwnership>> GetTileOwnershipAsync(
        AscendantEmpireContext context,
        int gameId,
        IReadOnlyCollection<int> campaignMapIds,
        CancellationToken cancelToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(campaignMapIds);

        if (campaignMapIds.Count == 0)
        {
            return new Dictionary<int, TileOwnership>();
        }

        int[] tileIds = [.. campaignMapIds.Distinct()];

        // Projected to the six columns the derivation reads rather than materialising the tiles, which carry their terrain, their river edges and their sight stamps too. A
        // keyed lookup on the primary key, so this is one round-trip however many tiles are in sight.
        List<TileOwnershipColumns> tiles = await context.CampaignMaps
            .AsNoTracking()
            .Where(tile => tile.GameId == gameId && tileIds.Contains(tile.CampaignMapId))
            .Select(tile => new TileOwnershipColumns
            {
                CampaignMapId = tile.CampaignMapId,
                OwnedByPlayerId = tile.OwnedByPlayerId,
                OccupiedByPlayerId = tile.OccupiedByPlayerId,
                LastOwnedByPlayerId = tile.LastOwnedByPlayerId,
                RebellionPercentRemaining = tile.RebellionPercentRemaining,
                HasClaims = tile.HasClaims
            })
            .ToListAsync(cancelToken);

        if (tiles.Count == 0)
        {
            return new Dictionary<int, TileOwnership>();
        }

        IReadOnlyDictionary<int, List<int>> claimantsByTileId = await GetClaimantsAsync(context, gameId, tiles, cancelToken);
        IReadOnlySet<int> disputedTileIds = await GetDisputedTileIdsAsync(context, gameId, tileIds, cancelToken);

        Dictionary<int, TileOwnership> ownershipByTileId = [];

        foreach (TileOwnershipColumns tile in tiles)
        {
            IReadOnlyCollection<int> claimantPlayerIds = claimantsByTileId.TryGetValue(tile.CampaignMapId, out List<int>? claimants) ? claimants : _NO_CLAIMANTS;

            ownershipByTileId[tile.CampaignMapId] = TileOwnershipDerivation.Derive(
                tile,
                claimantPlayerIds,
                hasBattle: disputedTileIds.Contains(tile.CampaignMapId));
        }

        return ownershipByTileId;
    }

    /// <summary>
    /// Every Player claiming each of the given tiles, keyed by tile id, for the tiles that have any claims at all.
    /// </summary>
    /// <remarks>
    /// <b>This is what <c>CampaignMap.HasClaims</c> exists for.</b> Claims are sparse - most of a map has never had a row in the junction - so the flag already loaded with
    /// the tiles decides which ids are worth asking about, and a read over wilderness skips this query entirely rather than sending every tile in sight to a table with
    /// nothing in it for them.
    /// </remarks>
    private static async Task<IReadOnlyDictionary<int, List<int>>> GetClaimantsAsync(
        AscendantEmpireContext context,
        int gameId,
        IReadOnlyCollection<TileOwnershipColumns> tiles,
        CancellationToken cancelToken)
    {
        int[] claimedTileIds = [.. tiles.Where(tile => tile.HasClaims).Select(tile => tile.CampaignMapId)];

        if (claimedTileIds.Length == 0)
        {
            return new Dictionary<int, List<int>>();
        }

        var claims = await context.CampaignMapClaims
            .AsNoTracking()
            .Where(claim => claim.GameId == gameId && claimedTileIds.Contains(claim.CampaignMapId))
            .Select(claim => new { claim.CampaignMapId, claim.PlayerId })
            .ToListAsync(cancelToken);

        return claims
            .GroupBy(claim => claim.CampaignMapId)
            .ToDictionary(tileClaims => tileClaims.Key, tileClaims => tileClaims.Select(claim => claim.PlayerId).ToList());
    }

    /// <summary>
    /// The tiles of the given set that have a battle being fought on them right now.
    /// </summary>
    /// <remarks>
    /// Disputed is the one state with nothing on the tile behind it: it is derived from the <c>Battle</c> on the tile and never stored, so it has to be asked for separately.
    /// A Battle with no <c>ConcludedAtRoundNumber</c> is one still being fought - the same test <c>StepForceHandler</c> uses to refuse a step onto a tile already in battle.
    /// </remarks>
    private static async Task<IReadOnlySet<int>> GetDisputedTileIdsAsync(
        AscendantEmpireContext context,
        int gameId,
        IReadOnlyCollection<int> tileIds,
        CancellationToken cancelToken)
    {
        List<int> disputedTileIds = await context.Battles
            .AsNoTracking()
            .Where(battle => battle.GameId == gameId && battle.ConcludedAtRoundNumber == null && tileIds.Contains(battle.CampaignMapTileId))
            .Select(battle => battle.CampaignMapTileId)
            .Distinct()
            .ToListAsync(cancelToken);

        return disputedTileIds.ToHashSet();
    }
}
