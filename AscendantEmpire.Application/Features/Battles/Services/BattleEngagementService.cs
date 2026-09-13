using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Battles.Classes;
using AscendantEmpire.Application.Features.Maps.BattleMaps;
using AscendantEmpire.Domain.Entities.Battles;
using AscendantEmpire.Domain.Entities.Units;
using AscendantEmpire.Domain.Enums.Battle;
using AscendantEmpire.Domain.Enums.Maps;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.Battles.Services;

/// <inheritdoc cref="IBattleEngagementService" />
internal sealed class BattleEngagementService(
    ILogger<BattleEngagementService> logger,
    IMediator mediator,
    IBattleLifecycleWriter battleLifecycleWriter) : IBattleEngagementService
{
    /// <inheritdoc />
    public async Task<ServiceResult<int>> CommenceAsync(AscendantEmpireContext context, BattleEngagement engagement, CancellationToken cancelToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(engagement);

        if (engagement.Defenders.Count == 0)
        {
            return ServiceResult<int>.Fail("A Battle cannot begin with nobody to fight.");
        }

        // The step handler only ever hands over other Players' Forces, so this is a caller bug rather than something a player can do. Refused rather than trusted, because a Player on both
        // sides would be a duplicate on the roster, which the lifecycle writer would reject far less helpfully.
        if (engagement.Defenders.Any(defender => defender.PlayerId == engagement.Attacker.PlayerId))
        {
            return ServiceResult<int>.Fail("A Force cannot do battle with its own side.");
        }

        try
        {
            // The battle map first, before anything is placed or saved. The generator returns straight away when the tile already has one, which is what "subsequent battles reuse it" in
            // docs/design/battlefield-maps.md asks for. It commits on its own connection, so a failure further down leaves a finished map behind - harmless, since the next Battle here reuses it.
            ServiceResult<int> battleMapResult = await mediator.Send(new GenerateBattleMapRequest(engagement.GameId, engagement.CampaignMapTileId), cancelToken);

            if (battleMapResult.HasSucceeded == false)
            {
                const string error = "The battlefield could not be prepared, so the battle cannot begin.";

                logger.LogWarning(
                    "{Error} Game Id: {GameId}, Campaign Map Tile Id: {CampaignMapTileId}. Errors: {Errors}",
                    error,
                    engagement.GameId,
                    engagement.CampaignMapTileId,
                    string.Join("; ", battleMapResult.Errors));

                return ServiceResult<int>.Fail(battleMapResult.Errors.Prepend(error));
            }

            int battleMapIndexId = battleMapResult.SuccessValue;

            // Only a finished map has an offset worth placing anyone by: the index row records its offset as 0 until the tiles have been written.
            int? battleMapOffsetId = await context.BattleMapIndex
                .AsNoTracking()
                .Where(index => index.Id == battleMapIndexId && index.GenerationStatus == MapCreationStatus.Complete)
                .Select(index => (int?)index.BattleMapOffsetId)
                .FirstOrDefaultAsync(cancelToken);

            if (battleMapOffsetId is null)
            {
                string error = $"Battle Map Index Id {battleMapIndexId} was reported as generated but is not complete, so nobody can be placed on it.";
                logger.LogError("{Error} Game Id: {GameId}, Campaign Map Tile Id: {CampaignMapTileId}.", error, engagement.GameId, engagement.CampaignMapTileId);

                return ServiceResult<int>.Fail(error);
            }

            // The game's own snapshot of the size, which is what the generator built the map with.
            int battleMapSize = await context.GameIndex
                .AsNoTracking()
                .Where(game => game.Id == engagement.GameId)
                .Select(game => game.BattleMapSize)
                .FirstOrDefaultAsync(cancelToken);

            // Who held the ground when the fighting started. The generator has already confirmed the tile exists, so a null here means nobody controls it rather than that it is missing.
            int? tileControllerPlayerId = await context.CampaignMaps
                .AsNoTracking()
                .Where(tile => tile.GameId == engagement.GameId && tile.CampaignMapId == engagement.CampaignMapTileId)
                .Select(tile => tile.ControlledByPlayerId)
                .FirstOrDefaultAsync(cancelToken);

            // Ordered by id, so the same Forces on the same tile always produce the same placement and the same roster, whatever order the caller found them in.
            List<UnitInstance> defenders = [.. engagement.Defenders.OrderBy(defender => defender.Id)];

            // One side per Player. The attacker's Player goes first on the roster, then the defending Players in id order, so the history reads "attacker joined" before the defenders.
            int[] sidePlayerIds = [engagement.Attacker.PlayerId, .. defenders.Select(defender => defender.PlayerId).Distinct().Order()];

            Dictionary<int, string> factionNamesByPlayerId = await context.Players
                .AsNoTracking()
                .Where(player => sidePlayerIds.Contains(player.Id))
                .ToDictionaryAsync(player => player.Id, player => player.FactionName, cancelToken);

            // Worked out in full before anything is written onto the Forces, so a placement that cannot be made (it throws) leaves them untouched.
            IReadOnlyDictionary<int, int> battleTileIdsByUnitId = BattleDeploymentPlanner.Plan(
                battleMapSize,
                battleMapOffsetId.Value,
                engagement.StepDirection,
                attackerUnitIds: [engagement.Attacker.Id],
                defenderUnitIds: [.. defenders.Select(defender => defender.Id)]);

            // A Force standing on a battle map tile is a Force in battle: it is what refuses it a campaign step, and what lights up its Battle button. These are staged on the caller's
            // tracked Forces and saved by the lifecycle writer below, in the same transaction as the Battle itself.
            engagement.Attacker.BattleMapTileId = battleTileIdsByUnitId[engagement.Attacker.Id];

            foreach (UnitInstance defender in defenders)
            {
                defender.BattleMapTileId = battleTileIdsByUnitId[defender.Id];
            }

            Battle battle = BuildBattle(engagement, sidePlayerIds, factionNamesByPlayerId, tileControllerPlayerId);

            // The writer's first save flushes everything the caller has staged on this context - the attacker's move and its Moved history row, the movement points it paid, and the starting
            // tiles above - inside the transaction it opens. That documented behaviour is what makes the whole step one commit: the move is never saved without the Battle, nor the Battle
            // without the move.
            ServiceResultBase commenceResult = await battleLifecycleWriter.CommenceAsync(context, battle, engagement.PlayerTurnId, cancelToken);

            if (commenceResult.HasSucceeded == false)
            {
                const string error = "The battle could not be recorded, so it has not begun.";

                logger.LogWarning(
                    "{Error} Game Id: {GameId}, Campaign Map Tile Id: {CampaignMapTileId}. Errors: {Errors}",
                    error,
                    engagement.GameId,
                    engagement.CampaignMapTileId,
                    string.Join("; ", commenceResult.Errors));

                return ServiceResult<int>.Fail(commenceResult.Errors.Prepend(error));
            }

            logger.LogInformation(
                "Battle Id {BattleId} commenced on Campaign Map Tile Id {CampaignMapTileId} in Game Id {GameId}: Unit Instance Id {AttackerId} attacked {DefenderCount} Force(s) of Player Id(s) {DefendingPlayerIds}.",
                battle.Id,
                engagement.CampaignMapTileId,
                engagement.GameId,
                engagement.Attacker.Id,
                defenders.Count,
                string.Join(", ", sidePlayerIds.Skip(1)));

            return ServiceResult<int>.Succeed(battle.Id);
        }
        catch (Exception ex)
        {
            const string error = "An error occurred while starting a battle.";

            logger.LogError(
                ex,
                "{Error} Game Id: {GameId}, Campaign Map Tile Id: {CampaignMapTileId}, Attacker Unit Instance Id: {AttackerId}.",
                error,
                engagement.GameId,
                engagement.CampaignMapTileId,
                engagement.Attacker.Id);

            return ServiceResult<int>.Fail(error);
        }
    }

    /// <summary>
    /// Builds the new, unsaved Battle and its founding roster.
    /// </summary>
    /// <param name="sidePlayerIds">One entry per side, attacker first.</param>
    /// <param name="tileControllerPlayerId">The Player controlling the tile when the fighting started, or <see langword="null"/> when nobody did.</param>
    private static Battle BuildBattle(BattleEngagement engagement, int[] sidePlayerIds, Dictionary<int, string> factionNamesByPlayerId, int? tileControllerPlayerId)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;

        return new Battle
        {
            GameId = engagement.GameId,
            CampaignMapTileId = engagement.CampaignMapTileId,
            GlobalBattleName = BuildBattleName(engagement.RoundNumber, sidePlayerIds, factionNamesByPlayerId),
            CommencedAtRoundNumber = engagement.RoundNumber,

            // The campaign map records only who controls a tile, not how firmly, so a controlled tile is taken as owned. Claimed, Occupied and Disputed need a per-tile ownership status
            // the map does not have yet.
            OriginalTileStatus = tileControllerPlayerId is null ? TileOwnershipStatus.Unclaimed : TileOwnershipStatus.Owned,
            OriginalTileOwnerId = tileControllerPlayerId,

            ConcludedAtRoundNumber = null,
            VictorId = null,
            CreatedAt = now,
            UpdatedAt = now,
            RowVersion = [],
            BattleParticipants =
            [
                .. sidePlayerIds.Select(playerId => new BattleParticipant
                {
                    PlayerId = playerId,
                    JoinedAtRoundNumber = engagement.RoundNumber,
                    IsStillInBattle = true,
                    LeftAtRoundNumber = null,
                    ExitReason = BattleExitReason.NotApplicable,
                    PlayerBattleName = null,
                    RowVersion = []
                })
            ]
        };
    }

    /// <summary>
    /// A default name for the Battle, such as "Battle 4 between Rome and Carthage".
    /// </summary>
    /// <remarks>
    /// The same placeholder wording <c>CreateBattleTurnQueueHandler</c> uses. Cut to the column's length rather than refused, since a long list of faction names is no reason not to fight.
    /// </remarks>
    private static string BuildBattleName(int roundNumber, int[] sidePlayerIds, Dictionary<int, string> factionNamesByPlayerId)
    {
        IEnumerable<string> sideNames = sidePlayerIds.Select(playerId => factionNamesByPlayerId.TryGetValue(playerId, out string? factionName) ? factionName : $"Player #{playerId}");

        string name = $"Battle {roundNumber} between {string.Join(" and ", sideNames)}";

        return name.Length <= Battle.BATTLE_NAME_LENGTH ? name : name[..Battle.BATTLE_NAME_LENGTH];
    }
}
