using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.ForceOrders.Movement;
using AscendantEmpire.Application.Features.Maps.CampaignMaps.Classes;
using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Enums.Turns;

using Microsoft.EntityFrameworkCore;

using System.Linq.Expressions;

namespace AscendantEmpire.Application.Features.Maps.CampaignMaps.Services;

/// <inheritdoc cref="ICampaignTerritoryReader" />
internal class CampaignTerritoryReader : ICampaignTerritoryReader
{
    /// <summary>No claimants, shared by the overwhelming majority of tiles that have no claim rows at all.</summary>
    private static readonly int[] _NO_CLAIMANTS = [];

    /// <summary>
    /// A world-map tile as the territory rules see it, without its claimants, which come from the junction.
    /// </summary>
    /// <remarks>
    /// Scalars only, in the <see cref="SightDataReader"/> style. Materialising <see cref="CampaignMap"/> would drag the Biome and Geography entities along with every one
    /// of a Game's tiles; the rules need four values off them and nothing else.
    /// </remarks>
    private static readonly Expression<Func<CampaignMap, TerritoryTileColumns>> _TERRITORY_PROJECTION = tile => new TerritoryTileColumns
    {
        CampaignMapId = tile.CampaignMapId,
        X = tile.XCoord,
        Y = tile.YCoord,
        OwnedByPlayerId = tile.OwnedByPlayerId,
        OwnedBySettlementId = tile.OwnedBySettlementId,
        OccupiedByPlayerId = tile.OccupiedByPlayerId,
        LastOwnedByPlayerId = tile.LastOwnedByPlayerId,
        HasClaims = tile.HasClaims,
        BiomeTypeId = tile.BiomeTypeId,
        BiomeIsTraversable = tile.Biome.IsTraversable,
        GeographyMovementCost = tile.Geography.MovementCost,
        SouthRiver = tile.SouthRiver,
        EastRiver = tile.EastRiver
    };

    /// <inheritdoc />
    public async Task<IReadOnlyCollection<TerritoryTile>> GetTerritoryAsync(AscendantEmpireContext context, int gameId, CancellationToken cancelToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        List<TerritoryTileColumns> tiles = await context.CampaignMaps
            .AsNoTracking()
            .Where(tile => tile.GameId == gameId)
            .Select(_TERRITORY_PROJECTION)
            .ToListAsync(cancelToken);

        if (tiles.Count == 0)
        {
            return [];
        }

        return Combine(tiles, await GetClaimantsForGameAsync(context, gameId, cancelToken));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyCollection<TerritoryTile>> GetTilesAsync(
        AscendantEmpireContext context,
        int gameId,
        IReadOnlyCollection<int> campaignMapIds,
        CancellationToken cancelToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(campaignMapIds);

        if (campaignMapIds.Count == 0)
        {
            return [];
        }

        int[] tileIds = [.. campaignMapIds.Distinct()];

        List<TerritoryTileColumns> tiles = await context.CampaignMaps
            .AsNoTracking()
            .Where(tile => tile.GameId == gameId && tileIds.Contains(tile.CampaignMapId))
            .Select(_TERRITORY_PROJECTION)
            .ToListAsync(cancelToken);

        if (tiles.Count == 0)
        {
            return [];
        }

        // Narrowed to the tiles asked about, since a caller writing nine tiles has no use for the whole Game's claims.
        var claims = await context.CampaignMapClaims
            .AsNoTracking()
            .Where(claim => claim.GameId == gameId && tileIds.Contains(claim.CampaignMapId))
            .Select(claim => new { claim.CampaignMapId, claim.PlayerId })
            .ToListAsync(cancelToken);

        return Combine(tiles, GroupClaimants(claims.Select(claim => (claim.CampaignMapId, claim.PlayerId))));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyCollection<int>> GetPlayerIdsInTurnOrderAsync(AscendantEmpireContext context, int gameId, CancellationToken cancelToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        // The campaign queue, not a Battle's: a Game has exactly one, and it is the queue whose order decides who takes ground first.
        return await context.TurnQueueParticipants
            .AsNoTracking()
            .Where(participant => participant.TurnQueue.GameId == gameId
                               && participant.TurnQueue.QueueType == TurnQueueType.Campaign
                               && participant.IsActiveInLane)
            .OrderBy(participant => participant.TurnOrder)
            .Select(participant => participant.PlayerId)
            .ToListAsync(cancelToken);
    }

    /// <summary>
    /// Every claim row in the Game, grouped by tile.
    /// </summary>
    /// <remarks>
    /// <b>Filtered by Game alone, and deliberately not by <c>HasClaims</c>.</b> <see cref="CampaignOwnershipReader"/> uses the flag because it asks about a handful of
    /// tiles in sight and most of a map has never had a claim. The pass asks about the whole Game, so the flag would buy nothing and would cost something real: a row on
    /// a tile whose flag had gone stale would be invisible to the pass and survive for ever. The rows are the truth and the flag is the cache the pass repairs.
    /// <c>IX_CampaignMapClaim_Game_Player</c> serves this as a prefix, so there is no new index to add.
    /// </remarks>
    private static async Task<IReadOnlyDictionary<int, List<int>>> GetClaimantsForGameAsync(AscendantEmpireContext context, int gameId, CancellationToken cancelToken)
    {
        var claims = await context.CampaignMapClaims
            .AsNoTracking()
            .Where(claim => claim.GameId == gameId)
            .Select(claim => new { claim.CampaignMapId, claim.PlayerId })
            .ToListAsync(cancelToken);

        return GroupClaimants(claims.Select(claim => (claim.CampaignMapId, claim.PlayerId)));
    }

    private static IReadOnlyDictionary<int, List<int>> GroupClaimants(IEnumerable<(int CampaignMapId, int PlayerId)> claims)
    {
        return claims
            .GroupBy(claim => claim.CampaignMapId)
            .ToDictionary(tileClaims => tileClaims.Key, tileClaims => tileClaims.Select(claim => claim.PlayerId).Order().ToList());
    }

    /// <summary>
    /// Joins the projected columns to their claimants, in memory.
    /// </summary>
    private static IReadOnlyCollection<TerritoryTile> Combine(IReadOnlyCollection<TerritoryTileColumns> tiles, IReadOnlyDictionary<int, List<int>> claimantsByTileId)
    {
        List<TerritoryTile> territory = new(tiles.Count);

        foreach (TerritoryTileColumns tile in tiles)
        {
            territory.Add(new TerritoryTile
            {
                CampaignMapId = tile.CampaignMapId,
                X = tile.X,
                Y = tile.Y,
                OwnedByPlayerId = tile.OwnedByPlayerId,
                OwnedBySettlementId = tile.OwnedBySettlementId,
                OccupiedByPlayerId = tile.OccupiedByPlayerId,
                LastOwnedByPlayerId = tile.LastOwnedByPlayerId,
                ClaimantPlayerIds = claimantsByTileId.TryGetValue(tile.CampaignMapId, out List<int>? claimants) ? claimants : _NO_CLAIMANTS,
                HasClaims = tile.HasClaims,
                BiomeTypeId = tile.BiomeTypeId,
                BiomeIsTraversable = tile.BiomeIsTraversable,
                GeographyMovementCost = tile.GeographyMovementCost,
                Rivers = new TileRiverEdges(tile.SouthRiver, tile.EastRiver)
            });
        }

        return territory;
    }
}
