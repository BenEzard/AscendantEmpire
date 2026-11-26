using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Technologies.Classes;
using AscendantEmpire.Domain.Enums.Forces;
using AscendantEmpire.Domain.Enums.Maps;
using AscendantEmpire.Domain.Enums.Structures;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AscendantEmpire.Application.Features.Turns.Services.TurnPipeline.Campaign.EndTurn;

/// <inheritdoc cref="IExperienceAccrualReader"/>
internal sealed class ExperienceAccrualReader(ILogger<ExperienceAccrualReader> logger) : IExperienceAccrualReader
{
    /// <summary>What crossing one tile of unimproved ground teaches.</summary>
    private const int _TREADING_EXPERIENCE = 1;

    /// <summary>What finishing a Settlement's Structure teaches about the ground it stands on.</summary>
    private const int _SETTLING_EXPERIENCE = 10;

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<string, int>> ReadForTurnAsync(
        AscendantEmpireContext context,
        int playerId,
        int playerTurnId,
        CancellationToken cancelToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        int[] unimprovedTileIds = await ReadTilesTroddenAsync(context, playerId, playerTurnId, cancelToken);
        int[] settledTileIds = await ReadTilesSettledAsync(context, playerId, playerTurnId, cancelToken);

        int[] tileIds = [.. unimprovedTileIds.Concat(settledTileIds).Distinct()];

        if (tileIds.Length == 0)
        {
            return new Dictionary<string, int>();
        }

        Dictionary<int, BiomeType> biomesByTileId = await context.CampaignMaps
            .AsNoTracking()
            .Where(tile => tileIds.Contains(tile.CampaignMapId))
            .ToDictionaryAsync(tile => tile.CampaignMapId, tile => tile.BiomeTypeId, cancelToken);

        Dictionary<string, int> earned = [];

        AddBiomePoints(earned, biomesByTileId, unimprovedTileIds, _TREADING_EXPERIENCE);
        AddBiomePoints(earned, biomesByTileId, settledTileIds, _SETTLING_EXPERIENCE);

        return earned;
    }

    /// <summary>
    /// The distinct tiles the Player's Forces entered this Turn that carry no standing Structure.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="ForceHistoryEventType.Moved"/> only: a Force created on a tile or destroyed on one has not crossed it. The <c>Distinct</c> is the whole of
    /// the once-per-tile-per-Turn rule - <c>MovementHistory</c> is deliberately not deduplicated by tile, because the track layer needs the repeats.
    /// </para>
    /// <para>
    /// Improvement is judged <b>now, not at the moment of entry</b>, and from founding rather than completion: a half-built Structure is occupied ground.
    /// <c>BattleMapTileId</c> has to be excluded because a battle-map Structure still carries a campaign tile id, and counting it would disqualify a campaign
    /// tile holding nothing but battle scenery.
    /// </para>
    /// </remarks>
    private static async Task<int[]> ReadTilesTroddenAsync(
        AscendantEmpireContext context,
        int playerId,
        int playerTurnId,
        CancellationToken cancelToken)
    {
        int[] troddenTileIds = await context.MovementHistory
            .AsNoTracking()
            .Where(row => row.PlayerTurnId == playerTurnId
                       && row.PlayerId == playerId
                       && row.EventType == ForceHistoryEventType.Moved)
            .Select(row => row.CampaignMapTileId)
            .Distinct()
            .ToArrayAsync(cancelToken);

        if (troddenTileIds.Length == 0)
        {
            return [];
        }

        int[] improvedTileIds = await context.StructureInstances
            .AsNoTracking()
            .Where(structure => troddenTileIds.Contains(structure.CampaignMapTileId)
                             && structure.BattleMapTileId == null
                             && structure.RemovedOnTurnId == null)
            .Select(structure => structure.CampaignMapTileId)
            .Distinct()
            .ToArrayAsync(cancelToken);

        return [.. troddenTileIds.Except(improvedTileIds)];
    }

    /// <summary>
    /// The tiles where a Structure a Settlement hangs off reached 100% this Turn.
    /// </summary>
    /// <remarks>
    /// <c>StructureHistory</c> rather than <c>StructureInstance.CompletedOnTurnId</c>, so this reads the same append-only feed the movement rule does. A
    /// Structure founded already complete still gets a <see cref="StructureHistoryEventType.Completed"/> row, and one created outside any Turn - which is how
    /// Game setup places starting Structures - gets no rows at all and so earns nothing.
    /// </remarks>
    private static async Task<int[]> ReadTilesSettledAsync(
        AscendantEmpireContext context,
        int playerId,
        int playerTurnId,
        CancellationToken cancelToken)
    {
        var completions = await context.StructureHistory
            .AsNoTracking()
            .Where(row => row.PlayerTurnId == playerTurnId
                       && row.PlayerId == playerId
                       && row.EventType == StructureHistoryEventType.Completed)
            .Select(row => new { row.StructureInstanceId, row.CampaignMapTileId })
            .ToArrayAsync(cancelToken);

        if (completions.Length == 0)
        {
            return [];
        }

        int[] completedInstanceIds = [.. completions.Select(completion => completion.StructureInstanceId)];

        HashSet<int> settlementInstanceIds = [.. await context.Settlements
            .AsNoTracking()
            .Where(settlement => completedInstanceIds.Contains(settlement.StructureInstanceId))
            .Select(settlement => settlement.StructureInstanceId)
            .ToArrayAsync(cancelToken)];

        return
        [
            .. completions
                .Where(completion => settlementInstanceIds.Contains(completion.StructureInstanceId))
                .Select(completion => completion.CampaignMapTileId)
        ];
    }

    /// <summary>
    /// Adds a flat amount per tile against each tile's Biome code.
    /// </summary>
    private void AddBiomePoints(
        Dictionary<string, int> earned,
        Dictionary<int, BiomeType> biomesByTileId,
        IEnumerable<int> tileIds,
        int pointsPerTile)
    {
        foreach (int tileId in tileIds)
        {
            if (biomesByTileId.TryGetValue(tileId, out BiomeType biomeType) == false)
            {
                logger.LogWarning("Campaign Map Tile Id {TileId} earned experience but has no tile row, so nothing was accrued for it.", tileId);
                continue;
            }

            if (ExperienceTypeCodes.ForBiome(biomeType) is not string code)
            {
                continue;
            }

            earned[code] = earned.TryGetValue(code, out int existing) ? existing + pointsPerTile : pointsPerTile;
        }
    }
}
