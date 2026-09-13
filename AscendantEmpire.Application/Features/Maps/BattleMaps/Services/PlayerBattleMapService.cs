using AscendantEmpire.Application.Data;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.Maps.BattleMaps.Services;

/// <inheritdoc cref="IPlayerBattleMapService" />
internal sealed class PlayerBattleMapService(ILogger<PlayerBattleMapService> logger) : IPlayerBattleMapService
{
    /// <inheritdoc />
    public async Task<ServiceResult<int>> InitialiseAsync(AscendantEmpireContext context, int gameId, int battleMapIndexId, IReadOnlyCollection<int> playerIds, CancellationToken cancelToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(playerIds);

        if (playerIds.Count == 0)
        {
            return ServiceResult<int>.Fail($"No Players were given to initialise Battle Map Index Id {battleMapIndexId} for.");
        }

        int inserted = 0;

        // One statement per Player. Battles have a handful of sides at most, and a single interpolated statement cannot carry a list of ids as parameters.
        foreach (int playerId in playerIds.Distinct().Order())
        {
            // Copies the tile's coordinates and leaves everything a Player learns by looking - biome, altitude, when they saw it - unknown. BiomeTypeId 0 is BiomeType.Unknown, which has a
            // catalogue row, exactly as game setup relies on for PlayerCampaignMap.
            //
            // NOT EXISTS keeps a Player's rows from an earlier Battle on the same persistent map, along with whatever they explored during it, and also means the primary key is never hit.
            inserted += await context.Database.ExecuteSqlInterpolatedAsync($@"
                INSERT INTO [game].[PlayerBattleMap]
                (
                    [PlayerId],
                    [BattleMapId],
                    [GameId],
                    [BattleMapIndexId],
                    [XCoord],
                    [YCoord],
                    [BiomeTypeId],
                    [AltitudeLevel],
                    [FirstSeenOnTurnId],
                    [LastSeenOnTurnId]
                )
                SELECT
                    {playerId},
                    bm.[Id],
                    {gameId},
                    bm.[BattleMapIndexId],
                    bm.[XCoord],
                    bm.[YCoord],
                    CAST(0 AS INT),
                    NULL,
                    NULL,
                    NULL
                FROM [battle].[BattleMap] bm
                WHERE bm.[BattleMapIndexId] = {battleMapIndexId}
                  AND NOT EXISTS
                  (
                      SELECT 1
                      FROM [game].[PlayerBattleMap] pbm
                      WHERE pbm.[PlayerId] = {playerId}
                        AND pbm.[BattleMapId] = bm.[Id]
                  );
                ", cancelToken);
        }

        logger.LogDebug("Initialised {RowCount} Player Battle Map row(s) on Battle Map Index Id {BattleMapIndexId} for Player Id(s) {PlayerIds}.", inserted, battleMapIndexId, string.Join(", ", playerIds));

        return ServiceResult<int>.Succeed(inserted);
    }

    /// <inheritdoc />
    public async Task<ServiceResult<int>> GrantFullVisionAsync(AscendantEmpireContext context, int battleMapIndexId, int playerId, int playerTurnId, CancellationToken cancelToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Exploring is copying the ground from the real tile. FirstSeenOnTurnId is only ever filled, never moved, so a tile explored in an earlier Battle keeps the Turn it was first seen in;
        // LastSeenOnTurnId always moves on to this sighting.
        int updated = await context.Database.ExecuteSqlInterpolatedAsync($@"
            UPDATE pbm
            SET
                pbm.[BiomeTypeId] = bm.[BiomeTypeId],
                pbm.[AltitudeLevel] = bm.[AltitudeLevel],
                pbm.[FirstSeenOnTurnId] = COALESCE(pbm.[FirstSeenOnTurnId], {playerTurnId}),
                pbm.[LastSeenOnTurnId] = {playerTurnId}
            FROM [game].[PlayerBattleMap] pbm
            INNER JOIN [battle].[BattleMap] bm ON bm.[Id] = pbm.[BattleMapId]
            WHERE pbm.[PlayerId] = {playerId}
              AND pbm.[BattleMapIndexId] = {battleMapIndexId};
            ", cancelToken);

        // Nothing to update means the Player was never initialised on this map - a caller bug, and one that would otherwise pass silently as a Player who simply sees nothing.
        if (updated == 0)
        {
            string error = $"Player Id {playerId} has no rows on Battle Map Index Id {battleMapIndexId}, so there is nothing for them to see.";
            logger.LogError("{Error} Player Turn Id: {PlayerTurnId}.", error, playerTurnId);

            return ServiceResult<int>.Fail(error);
        }

        return ServiceResult<int>.Succeed(updated);
    }
}
