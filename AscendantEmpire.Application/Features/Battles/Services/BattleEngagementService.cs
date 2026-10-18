using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Battles.Classes;
using AscendantEmpire.Application.Features.Maps.BattleMaps;
using AscendantEmpire.Application.Features.Maps.BattleMaps.Services;
using AscendantEmpire.Application.Features.Maps.CampaignMaps.Services;
using AscendantEmpire.BackendContracts.CampaignMaps.GetPlayerCampaignMap;
using AscendantEmpire.Domain.Entities.Battles;
using AscendantEmpire.Domain.Entities.Units;
using AscendantEmpire.Domain.Enums.Battle;
using AscendantEmpire.Domain.Enums.Maps;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.Battles.Services;

/// <inheritdoc cref="IBattleEngagementService" />
internal sealed class BattleEngagementService(
    ILogger<BattleEngagementService> logger,
    IMediator mediator,
    IBattleLifecycleWriter battleLifecycleWriter,
    IPlayerBattleMapService playerBattleMapService,
    ICampaignOwnershipReader campaignOwnershipReader) : IBattleEngagementService
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

            // What the tile was when the fighting started, resolved off its ownership columns and its claims rather than read as a single column: there is none to read, because
            // claims are many-to-one and nothing is cached on the tile. See docs/design/campaign-map/tile-ownership.md.
            //
            // This Battle is not written yet, so it contributes nothing to the Disputed test: the status recorded is the one the tile had before this fighting began. A tile
            // already in battle cannot be stepped onto at all - StepForceHandler refuses it - so in practice that test only ever comes back false here.
            IReadOnlyDictionary<int, TileOwnership> ownershipByTileId = await campaignOwnershipReader.GetTileOwnershipAsync(
                context,
                engagement.GameId,
                [engagement.CampaignMapTileId],
                cancelToken);

            // The generator has already confirmed the tile exists, so a miss here would mean the map is incomplete rather than that the tile is unowned. Wilderness is still
            // the right reading of it: nobody holds a tile there is no row for.
            TileOwnership tileOwnership = ownershipByTileId.GetValueOrDefault(engagement.CampaignMapTileId) ?? TileOwnership.Unclaimed;

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

            Battle battle = BuildBattle(engagement, sidePlayerIds, factionNamesByPlayerId, tileOwnership);

            // One transaction for every write from here on. It is opened only now, after the battle map exists, because the generator holds an exclusive lock on battle.BattleMap inside its own
            // transaction on another connection, and the Player Battle Map writes below read from that table. A caller already inside a transaction keeps ownership of it; otherwise this one is
            // ours, and disposing it uncommitted - on any refusal or exception below - rolls every write back.
            await using IDbContextTransaction? ownTransaction = context.Database.CurrentTransaction is null ? await context.Database.BeginTransactionAsync(cancelToken) : null;

            // Every side's view of the battle map, unexplored. These rows are written straight to the database rather than staged, so they go in before the Battle is recorded, and roll back
            // with it if it cannot be.
            ServiceResult<int> initialiseResult = await playerBattleMapService.InitialiseAsync(context, engagement.GameId, battleMapIndexId, sidePlayerIds, cancelToken);

            if (initialiseResult.HasSucceeded == false)
            {
                const string error = "The players' views of the battlefield could not be prepared, so the battle cannot begin.";

                logger.LogWarning(
                    "{Error} Game Id: {GameId}, Campaign Map Tile Id: {CampaignMapTileId}. Errors: {Errors}",
                    error,
                    engagement.GameId,
                    engagement.CampaignMapTileId,
                    string.Join("; ", initialiseResult.Errors));

                return ServiceResult<int>.Fail(initialiseResult.Errors.Prepend(error));
            }

            // The writer's first save flushes everything the caller has staged on this context - the attacker's move and its Moved history row, the movement points it paid, and the starting
            // tiles above - and it joins the transaction above rather than opening its own. That documented behaviour is what makes the whole step one commit: the move is never saved without
            // the Battle, nor the Battle without the move.
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

            // TODO: Full exploration vision of the battle map belongs to a Player whose settled territory - or an ally's - holds the tile, or who has an ambushing Force on it. Territory,
            //  alliances and ambushes are not modelled yet, so for now the Player whose Turn it is - the attacker - sees the whole battlefield and the defenders see none of it.
            //
            // Granted after the Battle is recorded, still inside the one transaction: a Battle that could not be recorded reveals nothing, and a reveal that fails takes the Battle with it.
            ServiceResult<int> visionResult = await playerBattleMapService.GrantFullVisionAsync(context, battleMapIndexId, engagement.Attacker.PlayerId, engagement.PlayerTurnId, cancelToken);

            if (visionResult.HasSucceeded == false)
            {
                const string error = "The battlefield could not be revealed to the attacker, so the battle has not begun.";

                logger.LogWarning(
                    "{Error} Game Id: {GameId}, Campaign Map Tile Id: {CampaignMapTileId}. Errors: {Errors}",
                    error,
                    engagement.GameId,
                    engagement.CampaignMapTileId,
                    string.Join("; ", visionResult.Errors));

                return ServiceResult<int>.Fail(visionResult.Errors.Prepend(error));
            }

            if (ownTransaction is not null)
            {
                await ownTransaction.CommitAsync(cancelToken);
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
    /// <param name="tileOwnership">What the tile was when the fighting started.</param>
    private static Battle BuildBattle(BattleEngagement engagement, int[] sidePlayerIds, Dictionary<int, string> factionNamesByPlayerId, TileOwnership tileOwnership)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;

        return new Battle
        {
            GameId = engagement.GameId,
            CampaignMapTileId = engagement.CampaignMapTileId,
            GlobalBattleName = BuildBattleName(engagement.RoundNumber, sidePlayerIds, factionNamesByPlayerId),
            CommencedAtRoundNumber = engagement.RoundNumber,

            OriginalTileStatus = tileOwnership.Status,

            // The effective holder rather than the owner: an occupier is drawing from the tile without owning it, and it is the side actually holding the ground that a
            // battle over it is being fought against. Nothing writes the occupier column yet, so today this resolves to the owner either way.
            OriginalTileOwnerId = tileOwnership.EffectiveHolderPlayerId,

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
