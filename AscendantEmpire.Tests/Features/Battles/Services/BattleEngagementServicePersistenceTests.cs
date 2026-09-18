using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Battles.Services;
using AscendantEmpire.Application.Features.Game.Handlers;
using AscendantEmpire.Application.Features.Maps.BattleMaps.Services;
using AscendantEmpire.Application.Features.Players.Handlers;
using AscendantEmpire.Application.Features.Turns.Services;
using AscendantEmpire.Application.Features.Units.Services;
using AscendantEmpire.Domain.Entities.Battles;
using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Entities.Maps.BattleMaps;
using AscendantEmpire.Domain.Entities.Maps.CampaignMaps;
using AscendantEmpire.Domain.Entities.Units;
using AscendantEmpire.Domain.Enums.Battle;
using AscendantEmpire.Domain.Enums.Forces;
using AscendantEmpire.Domain.Enums.Maps;
using AscendantEmpire.Domain.Enums.Turns;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Tests.Features.Battles.Services;

/// <summary>
/// Tests for <see cref="BattleEngagementService"/>, which starts a Battle when a Force steps onto another Player's Forces.
/// </summary>
/// <remarks>
/// <para>
/// On the containerised tier, for the reasons <c>testing.md</c> names: the battle map is bulk-copied inside its own transaction, commencing saves twice inside another, sequences are allocated
/// with raw SQL, and the point of the whole service is that the step and the Battle commit together or not at all - which only a real database can show.
/// </para>
/// <para>
/// Each test stages the attacker's step the way <c>StepForceHandler</c> does - through the real <see cref="ForceLifecycleWriter"/>, with the movement paid - and then hands the same context to
/// the service, so what is proven is the handler's real save path rather than a stand-in for it. The handler's decision to start a Battle at all is covered in-memory by
/// <c>StepForceHandlerTests</c>.
/// </para>
/// <para>
/// The attacker stands at (1, 2) and steps East onto the defender at (2, 2), on a 30 x 30 battle map, so the attacker starts centred on the west edge at (0, 14) and the defender on the east
/// edge at (29, 14).
/// </para>
/// <para>
/// The game has a third Player who takes no part, so that it can be shown a bystander gets no view of the battle map.
/// </para>
/// </remarks>
[TestFixture]
internal class BattleEngagementServicePersistenceTests : GameSqlServerTestBase
{
    private const int _MAP_WIDTH = 4;
    private const int _MAP_HEIGHT = 4;

    /// <summary>An attacker, a defender and a bystander.</summary>
    private const int _NUMBER_OF_PLAYERS = 3;

    /// <summary>The smallest the request validator allows, so each generated map is 900 tiles rather than 2,500.</summary>
    private const int _BATTLE_MAP_SIZE = 30;

    /// <summary>The seeded settler tag is unled with a base size of 25, so a Force must be a multiple of it.</summary>
    private const int _SETTLER_SIZE = 50;

    /// <summary>What the attacker holds before the step, and what the step costs: flat grassland at 1.0 point.</summary>
    private const long _STARTING_MOVEMENT = 2_000;
    private const long _ENTRY_COST = 1_000;

    /// <summary>A Player Turn id no row has, for provoking the foreign key on a history row.</summary>
    private const int _MISSING_PLAYER_TURN_ID = 999_999;

    private int _gameId;
    private int _attackingPlayerId;
    private int _defendingPlayerId;
    private int _bystandingPlayerId;
    private int _playerTurnId;
    private int _roundNumber;
    private int _originTileId;
    private int _targetTileId;
    private int _attackerId;
    private int _defenderId;

    private IMediator _mediator = null!;

    /// <summary>Captures what the service logs, so a failed commence can report the exception behind it.</summary>
    /// <remarks>Replaced in every set-up, so an exception logged by one test is never reported against the next.</remarks>
    private CapturingLogger<BattleEngagementService> _engagementLogger = null!;

    [SetUp]
    public async Task SetUpGameAsync()
    {
        _engagementLogger = new CapturingLogger<BattleEngagementService>();

        await SeedGameAsync(mapWidth: _MAP_WIDTH, mapHeight: _MAP_HEIGHT, numberOfPlayers: _NUMBER_OF_PLAYERS);

        // The shared fixture seeds the gallery with no master seed, which the campaign map is fine without but the battle map generator is not.
        await using (AscendantEmpireContext seed = await DbFactory.CreateDbContextAsync())
        {
            CampaignMapGalleryIndex galleryIndex = await seed.CampaignMapGalleryIndex.SingleAsync(cmi => cmi.Id == _galleryIndexId);
            galleryIndex.PropertiesJson = """{"seed":"123456789"}""";

            // The shared fixture seeds only the Unknown and Grassland biomes, but the generator scatters variants (Woodland, Arid, ...) across a battle map. Its bulk copy skips constraint
            // checks, so those tiles are written regardless; copying their biomes into a player's view is a checked update, and would break the foreign key without the catalogue rows a real
            // database seeds for every BiomeType.
            HashSet<BiomeType> seededBiomes = [.. await seed.CampaignBiomes.Select(b => b.BiomeTypeId).ToListAsync()];

            foreach (BiomeType biomeType in Enum.GetValues<BiomeType>().Where(biomeType => seededBiomes.Contains(biomeType) == false))
            {
                seed.CampaignBiomes.Add(new CampaignBiome
                {
                    BiomeTypeId = biomeType,
                    Name = biomeType.ToString(),
                    MapEditorHtmlColor = "#808080",
                    MovementCost = 1.0m,
                    DoesBlockSight = false,
                    IsWater = false,
                    IsTraversable = true,
                    RegenerationPercent = 0.1m,
                    IsPaintableOnCampaignMap = false,
                    CanBeElevated = true
                });
            }

            await seed.SaveChangesAsync();
        }

        ServiceResult<int> setupResult = await _service.SetupGameAsync(
            new SetupGameRequest(GalleryMapId: _galleryIndexId, GameName: "Engagement Game", NumberOfPlayers: _NUMBER_OF_PLAYERS, BattleMapSize: _BATTLE_MAP_SIZE, GenerationSeed: 42),
            CancellationToken.None);

        Assert.That(setupResult.HasSucceeded, Is.True, () => string.Join("; ", setupResult.Errors));

        _gameId = setupResult.SuccessValue;

        // Opens the first Player's Turn, so they are the attacker.
        _playerTurnId = await OpenFirstPlayerTurnAsync(_gameId);

        await using (AscendantEmpireContext context = await DbFactory.CreateDbContextAsync())
        {
            int[] playerIds = await context.Players.Where(p => p.GameId == _gameId).OrderBy(p => p.Id).Select(p => p.Id).ToArrayAsync();

            _attackingPlayerId = playerIds[0];
            _defendingPlayerId = playerIds[1];
            _bystandingPlayerId = playerIds[2];

            _roundNumber = await context.TurnQueues
                .Where(q => q.GameId == _gameId && q.QueueType == TurnQueueType.Campaign && q.BattleId == null)
                .Select(q => q.CurrentRoundNumber)
                .SingleAsync();

            _originTileId = await TileIdAsync(context, x: 1, y: 2);
            _targetTileId = await TileIdAsync(context, x: 2, y: 2);

            int settlerTemplateId = await context.UnitTemplates.Where(t => t.Code == "SETTLER").Select(t => t.Id).SingleAsync();

            // Placed directly rather than spawned, away from the starting armies on row 0, so the only Forces on either tile are these two.
            var attacker = NewSettler(_attackingPlayerId, _originTileId, settlerTemplateId);
            var defender = NewSettler(_defendingPlayerId, _targetTileId, settlerTemplateId);

            context.UnitInstances.AddRange(attacker, defender);

            await context.SaveChangesAsync();

            _attackerId = attacker.Id;
            _defenderId = defender.Id;
        }

        // GenerateBattleMapHandler takes a context directly rather than a factory.
        ServiceProvider provider = new ServiceCollection()
            .AddSingleton(DbFactory)
            .AddScoped(_ => DbFactory.CreateDbContext())
            .AddMediator(typeof(GetPlayersHandler).Assembly)
            .AddLogging()
            .BuildServiceProvider();

        _mediator = provider.GetRequiredService<IMediator>();
    }

    [Test, Description("One commit carries the whole attack - the Battle and its roster and history, the attacker's move and payment, and both sides' starting tiles on the generated map.")]
    public async Task ShouldCommitTheMoveTheBattleAndEveryStartingTileTogetherAsync()
    {
        // The defender's Player controls the ground, so the Battle records whose tile was fought over.
        await SetTileControllerAsync(_targetTileId, _defendingPlayerId);

        ServiceResult<int> result = await StepAndCommenceAsync(RealBattleWriter(), _playerTurnId, MoveDirection.East);

        Assert.That(result.HasSucceeded, Is.True, () => FailureOf(result));

        await using AscendantEmpireContext verify = await DbFactory.CreateDbContextAsync();

        Battle battle = await verify.Battles.Include(b => b.BattleParticipants).SingleAsync(b => b.GameId == _gameId);
        List<BattleHistory> history = await verify.BattleHistory.Where(h => h.BattleId == battle.Id).OrderBy(h => h.TurnSequence).ToListAsync();

        UnitInstance attacker = await verify.UnitInstances.SingleAsync(u => u.Id == _attackerId);
        UnitInstance defender = await verify.UnitInstances.SingleAsync(u => u.Id == _defenderId);
        MovementHistory moved = await verify.MovementHistory.SingleAsync(h => h.UnitInstanceId == _attackerId);

        BattleMapIndex battleMap = await verify.BattleMapIndex.SingleAsync(index => index.CampaignMapId == _targetTileId);
        BattleMap attackerTile = await verify.BattleMap.SingleAsync(tile => tile.Id == attacker.BattleMapTileId);
        BattleMap defenderTile = await verify.BattleMap.SingleAsync(tile => tile.Id == defender.BattleMapTileId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.SuccessValue, Is.EqualTo(battle.Id), "The service hands back the Battle it started.");

            Assert.That(battle.CampaignMapTileId, Is.EqualTo(_targetTileId));
            Assert.That(battle.CommencedAtRoundNumber, Is.EqualTo(_roundNumber));
            Assert.That(battle.ConcludedAtRoundNumber, Is.Null);
            Assert.That(battle.OriginalTileStatus, Is.EqualTo(TileOwnershipStatus.Owned));
            Assert.That(battle.OriginalTileOwnerId, Is.EqualTo(_defendingPlayerId));
            Assert.That(battle.GlobalBattleName, Does.StartWith($"Battle {_roundNumber} between "));

            Assert.That(battle.BattleParticipants.Select(p => p.PlayerId), Is.EquivalentTo(new[] { _attackingPlayerId, _defendingPlayerId }));
            Assert.That(battle.BattleParticipants, Has.All.Property(nameof(BattleParticipant.IsStillInBattle)).True);

            Assert.That(history.Select(h => h.EventType), Is.EqualTo(new[] { BattleHistoryEventType.Commenced, BattleHistoryEventType.ParticipantJoined, BattleHistoryEventType.ParticipantJoined }));
            Assert.That(history.Select(h => h.ParticipantPlayerId), Is.EqualTo(new int?[] { null, _attackingPlayerId, _defendingPlayerId }), "The attacker's side joins first.");

            Assert.That(attacker.CampaignMapTileId, Is.EqualTo(_targetTileId), "The attacker moved onto the tile it attacked.");
            Assert.That(attacker.CampaignMovementRemaining, Is.EqualTo(_STARTING_MOVEMENT - _ENTRY_COST), "And paid for the step.");
            Assert.That(moved.EventType, Is.EqualTo(ForceHistoryEventType.Moved));

            Assert.That(battleMap.GenerationStatus, Is.EqualTo(MapCreationStatus.Complete));
            Assert.That(attackerTile.BattleMapIndexId, Is.EqualTo(battleMap.Id), "Both sides stand on the map generated for the tile.");
            Assert.That(defenderTile.BattleMapIndexId, Is.EqualTo(battleMap.Id));
            Assert.That((attackerTile.XCoord, attackerTile.YCoord), Is.EqualTo((0, 14)), "Stepping East entered from the west, centred on that edge.");
            Assert.That((defenderTile.XCoord, defenderTile.YCoord), Is.EqualTo((29, 14)), "The defender faces it from the east edge.");
        }
    }

    [Test, Description("Starting a Battle gives each side a view of the whole battle map - fully explored for the attacker whose Turn it is, unexplored for the defender - and a bystander none.")]
    public async Task ShouldGiveEachSideAViewOfTheBattleMapWithFullVisionForTheAttackerAsync()
    {
        ServiceResult<int> result = await StepAndCommenceAsync(RealBattleWriter(), _playerTurnId, MoveDirection.East);

        Assert.That(result.HasSucceeded, Is.True, () => FailureOf(result));

        await using AscendantEmpireContext verify = await DbFactory.CreateDbContextAsync();

        BattleMapIndex battleMap = await verify.BattleMapIndex.SingleAsync(index => index.CampaignMapId == _targetTileId);
        Dictionary<int, BattleMap> tilesById = await verify.BattleMap.Where(tile => tile.BattleMapIndexId == battleMap.Id).ToDictionaryAsync(tile => tile.Id);

        List<PlayerBattleMap> attackerView = await verify.PlayerBattleMaps.Where(view => view.PlayerId == _attackingPlayerId).ToListAsync();
        List<PlayerBattleMap> defenderView = await verify.PlayerBattleMaps.Where(view => view.PlayerId == _defendingPlayerId).ToListAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(attackerView, Has.Count.EqualTo(_BATTLE_MAP_SIZE * _BATTLE_MAP_SIZE), "One row per battle tile for the attacker.");
            Assert.That(defenderView, Has.Count.EqualTo(_BATTLE_MAP_SIZE * _BATTLE_MAP_SIZE), "And for the defender.");
            Assert.That(await verify.PlayerBattleMaps.AnyAsync(view => view.PlayerId == _bystandingPlayerId), Is.False, "A Player with no part in the Battle has no view of its map.");

            Assert.That(attackerView.Concat(defenderView), Has.All.Matches<PlayerBattleMap>(view => view.GameId == _gameId && view.BattleMapIndexId == battleMap.Id && tilesById.ContainsKey(view.BattleMapId)),
                "Every row describes a tile of the map the Battle is fought on.");

            // The attacker's rows are the ground exactly as it is, seen during the Turn the attack was made in.
            Assert.That(attackerView, Has.All.Matches<PlayerBattleMap>(view =>
                view.XCoord == tilesById[view.BattleMapId].XCoord
                && view.YCoord == tilesById[view.BattleMapId].YCoord
                && view.BiomeTypeId == tilesById[view.BattleMapId].BiomeTypeId
                && view.AltitudeLevel == tilesById[view.BattleMapId].AltitudeLevel
                && view.FirstSeenOnTurnId == _playerTurnId
                && view.LastSeenOnTurnId == _playerTurnId),
                "The attacker has explored the whole battle map.");

            // The defender's rows know where each tile is and nothing else about it.
            Assert.That(defenderView, Has.All.Matches<PlayerBattleMap>(view =>
                view.XCoord == tilesById[view.BattleMapId].XCoord
                && view.YCoord == tilesById[view.BattleMapId].YCoord
                && view.BiomeTypeId == BiomeType.Unknown
                && view.AltitudeLevel == null
                && view.FirstSeenOnTurnId == null
                && view.LastSeenOnTurnId == null),
                "The defender has explored none of it.");
        }
    }

    [Test, Description("When the Battle's history cannot be written, the move, the starting tiles and every side's view of the map roll back with it, so a Force is never left on the tile unrecorded.")]
    public async Task ShouldRollBackTheMoveWhenTheBattleCannotBeRecordedAsync()
    {
        // An allocator that answers for a Turn that does not exist. The first save - the Battle, and with it the staged move and starting tiles - succeeds, and then the history rows break
        // the Player Turn foreign key: the second of the two saves failing, inside the one transaction.
        IPlayerTurnService lyingAllocator = Substitute.For<IPlayerTurnService>();

        lyingAllocator.AllocateSequenceAsync(Arg.Any<AscendantEmpireContext>(), _MISSING_PLAYER_TURN_ID, Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(ServiceResult<TurnSequenceBlock>.Succeed(new TurnSequenceBlock(1, 3)));

        var writer = new BattleLifecycleWriter(NullLogger<BattleLifecycleWriter>.Instance, lyingAllocator);

        ServiceResult<int> result = await StepAndCommenceAsync(writer, _MISSING_PLAYER_TURN_ID, MoveDirection.East);

        await using AscendantEmpireContext verify = await DbFactory.CreateDbContextAsync();

        UnitInstance attacker = await verify.UnitInstances.SingleAsync(u => u.Id == _attackerId);
        UnitInstance defender = await verify.UnitInstances.SingleAsync(u => u.Id == _defenderId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(await verify.Battles.AnyAsync(b => b.GameId == _gameId), Is.False, "The Battle's own insert was rolled back.");
            Assert.That(attacker.CampaignMapTileId, Is.EqualTo(_originTileId), "The move flushed with it was rolled back too.");
            Assert.That(attacker.CampaignMovementRemaining, Is.EqualTo(_STARTING_MOVEMENT), "And so was the payment.");
            Assert.That(await verify.MovementHistory.AnyAsync(h => h.UnitInstanceId == _attackerId), Is.False);
            Assert.That(attacker.BattleMapTileId, Is.Null);
            Assert.That(defender.BattleMapTileId, Is.Null);
            Assert.That(await verify.PlayerBattleMaps.AnyAsync(), Is.False, "The views written before the Battle was recorded rolled back with it.");

            // The map commits on its own before any of this, and is left for the next Battle here to reuse.
            Assert.That(await verify.BattleMapIndex.AnyAsync(index => index.CampaignMapId == _targetTileId && index.GenerationStatus == MapCreationStatus.Complete), Is.True);
        }
    }

    [Test, Description("A battle map that cannot be generated refuses the attack before anything is written, so the Force does not move onto a tile it cannot fight on.")]
    public async Task ShouldRefuseTheAttackWhenTheBattleMapCannotBeGeneratedAsync()
    {
        // Take the master seed away again, so the generator has nothing to build terrain from.
        await using (AscendantEmpireContext edit = await DbFactory.CreateDbContextAsync())
        {
            CampaignMapGalleryIndex galleryIndex = await edit.CampaignMapGalleryIndex.SingleAsync(cmi => cmi.Id == _galleryIndexId);
            galleryIndex.PropertiesJson = "{}";

            await edit.SaveChangesAsync();
        }

        ServiceResult<int> result = await StepAndCommenceAsync(RealBattleWriter(), _playerTurnId, MoveDirection.East);

        await using AscendantEmpireContext verify = await DbFactory.CreateDbContextAsync();

        UnitInstance attacker = await verify.UnitInstances.SingleAsync(u => u.Id == _attackerId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(result.Errors, Has.Some.Contains("battlefield could not be prepared"));
            Assert.That(await verify.Battles.AnyAsync(b => b.GameId == _gameId), Is.False);
            Assert.That(await verify.BattleMapIndex.AnyAsync(index => index.CampaignMapId == _targetTileId), Is.False);
            Assert.That(attacker.CampaignMapTileId, Is.EqualTo(_originTileId));
            Assert.That(attacker.BattleMapTileId, Is.Null);
        }
    }

    [Test, Description("A later Battle on the same tile fights on the map the first one generated, rather than generating a second copy, and each side keeps the view of it they already had.")]
    public async Task ShouldReuseTheTilesBattleMapForALaterBattleAsync()
    {
        ServiceResult<int> first = await StepAndCommenceAsync(RealBattleWriter(), _playerTurnId, MoveDirection.East);

        Assert.That(first.HasSucceeded, Is.True, () => FailureOf(first));

        // Stand in for the first Battle ending: concluded, and both Forces back off the battle map. Nothing in play concludes a Battle yet.
        await using (AscendantEmpireContext edit = await DbFactory.CreateDbContextAsync())
        {
            await edit.Battles.Where(b => b.Id == first.SuccessValue).ExecuteUpdateAsync(set => set.SetProperty(b => b.ConcludedAtRoundNumber, _roundNumber));
            await edit.UnitInstances.Where(u => u.GameId == _gameId).ExecuteUpdateAsync(set => set.SetProperty(u => u.BattleMapTileId, (int?)null));

            // The defender comes away from the first Battle remembering one corner of the field, which the second Battle must neither forget nor duplicate.
            await edit.PlayerBattleMaps
                .Where(view => view.PlayerId == _defendingPlayerId && view.XCoord == 0 && view.YCoord == 0)
                .ExecuteUpdateAsync(set => set.SetProperty(view => view.FirstSeenOnTurnId, (int?)_playerTurnId));
        }

        // Both Forces are on the target tile now, so the second fight needs no step - only the engagement itself.
        ServiceResult<int> second;

        await using (AscendantEmpireContext context = await DbFactory.CreateDbContextAsync())
        {
            second = await BuildService(RealBattleWriter()).CommenceAsync(context, await EngagementAsync(context, _playerTurnId, MoveDirection.West));
        }

        Assert.That(second.HasSucceeded, Is.True, () => FailureOf(second));

        await using AscendantEmpireContext verify = await DbFactory.CreateDbContextAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(second.SuccessValue, Is.Not.EqualTo(first.SuccessValue), "A second Battle was started.");
            Assert.That(await verify.BattleMapIndex.CountAsync(index => index.CampaignMapId == _targetTileId), Is.EqualTo(1), "On the one map.");
            Assert.That(await verify.BattleMap.CountAsync(), Is.EqualTo(_BATTLE_MAP_SIZE * _BATTLE_MAP_SIZE), "No second set of tiles was written.");

            Assert.That(await verify.PlayerBattleMaps.CountAsync(view => view.PlayerId == _attackingPlayerId), Is.EqualTo(_BATTLE_MAP_SIZE * _BATTLE_MAP_SIZE), "No second set of views for the attacker.");
            Assert.That(await verify.PlayerBattleMaps.CountAsync(view => view.PlayerId == _defendingPlayerId), Is.EqualTo(_BATTLE_MAP_SIZE * _BATTLE_MAP_SIZE), "Nor for the defender.");
            Assert.That(await verify.PlayerBattleMaps.CountAsync(view => view.PlayerId == _defendingPlayerId && view.FirstSeenOnTurnId != null), Is.EqualTo(1),
                "The defender still remembers the corner they explored, and has been shown nothing more.");
        }
    }

    // ---------------------------------------------------------------------

    /// <summary>
    /// Stages the attacker's step exactly as the step handler does - moved through the real lifecycle writer and paid for - and then commences the Battle on the same context.
    /// </summary>
    /// <param name="battlePlayerTurnId">The Turn the Battle is recorded against, which a test may point at a missing one to make the second save fail.</param>
    private async Task<ServiceResult<int>> StepAndCommenceAsync(IBattleLifecycleWriter battleWriter, int battlePlayerTurnId, MoveDirection stepDirection)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        BattleEngagement engagement = await EngagementAsync(context, battlePlayerTurnId, stepDirection);

        ServiceResultBase moveResult = await RealForceWriter().MoveAsync(context, engagement.Attacker, _targetTileId, _playerTurnId);

        Assert.That(moveResult.HasSucceeded, Is.True, () => string.Join("; ", moveResult.Errors));

        engagement.Attacker.CampaignMovementRemaining -= _ENTRY_COST;

        return await BuildService(battleWriter).CommenceAsync(context, engagement);
    }

    /// <summary>Loads both Forces tracked on the given context and describes the attack between them.</summary>
    private async Task<BattleEngagement> EngagementAsync(AscendantEmpireContext context, int playerTurnId, MoveDirection stepDirection)
    {
        UnitInstance attacker = await context.UnitInstances.SingleAsync(u => u.Id == _attackerId);
        List<UnitInstance> defenders = await context.UnitInstances.Where(u => u.Id == _defenderId).ToListAsync();

        return new BattleEngagement
        {
            GameId = _gameId,
            Attacker = attacker,
            Defenders = defenders,
            CampaignMapTileId = _targetTileId,
            StepDirection = stepDirection,
            RoundNumber = _roundNumber,
            PlayerTurnId = playerTurnId
        };
    }

    private BattleEngagementService BuildService(IBattleLifecycleWriter battleWriter)
    {
        return new BattleEngagementService(_engagementLogger, _mediator, battleWriter, new PlayerBattleMapService(NullLogger<PlayerBattleMapService>.Instance));
    }

    /// <summary>
    /// A failed result's errors, together with whatever exception the service logged, since its catch-all reports only a generic error and would otherwise hide the cause.
    /// </summary>
    private string FailureOf(ServiceResultBase result)
    {
        return $"{string.Join("; ", result.Errors)}\nLogged exception: {_engagementLogger.LastException}";
    }

    /// <summary>The Battle writer over the real allocator, on the per-test database.</summary>
    private BattleLifecycleWriter RealBattleWriter()
    {
        return new BattleLifecycleWriter(NullLogger<BattleLifecycleWriter>.Instance, new PlayerTurnService(NullLogger<PlayerTurnService>.Instance, DbFactory));
    }

    /// <summary>The Force writer over the real allocator, on the per-test database.</summary>
    private ForceLifecycleWriter RealForceWriter()
    {
        return new ForceLifecycleWriter(NullLogger<ForceLifecycleWriter>.Instance, new PlayerTurnService(NullLogger<PlayerTurnService>.Instance, DbFactory));
    }

    /// <summary>A new, unsaved settler with a full turn and a bit of movement.</summary>
    private UnitInstance NewSettler(int playerId, int tileId, int settlerTemplateId)
    {
        return new UnitInstance
        {
            GameId = _gameId,
            PlayerId = playerId,
            CampaignMapTileId = tileId,
            UnitTemplateId = settlerTemplateId,
            InitialStrength = _SETTLER_SIZE,
            HealthyCount = _SETTLER_SIZE,
            InjuredCount = 0,
            CampaignMovementRemaining = _STARTING_MOVEMENT
        };
    }

    private async Task<int> TileIdAsync(AscendantEmpireContext context, int x, int y)
    {
        return await context.CampaignMaps.Where(t => t.GameId == _gameId && t.XCoord == x && t.YCoord == y).Select(t => t.CampaignMapId).SingleAsync();
    }

    private async Task SetTileControllerAsync(int tileId, int playerId)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        await context.CampaignMaps.Where(t => t.CampaignMapId == tileId).ExecuteUpdateAsync(set => set.SetProperty(t => t.ControlledByPlayerId, (int?)playerId));
    }
}
