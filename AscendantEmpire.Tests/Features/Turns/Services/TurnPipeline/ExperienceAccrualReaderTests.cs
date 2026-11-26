using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Turns.Services.TurnPipeline.Campaign.EndTurn;
using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Entities.Maps.CampaignMaps;
using AscendantEmpire.Domain.Entities.Settlements;
using AscendantEmpire.Domain.Entities.Structures;
using AscendantEmpire.Domain.Entities.Units;
using AscendantEmpire.Domain.Enums.Forces;
using AscendantEmpire.Domain.Enums.Maps;
using AscendantEmpire.Domain.Enums.Settlements;
using AscendantEmpire.Domain.Enums.Structures;

using Microsoft.Extensions.Logging.Abstractions;

namespace AscendantEmpire.Tests.Features.Turns.Services.TurnPipeline;

/// <summary>
/// Tests for <see cref="ExperienceAccrualReader"/> — the experience a Turn's movement and construction earned, read back off the history it already wrote.
/// </summary>
/// <remarks>
/// On the in-memory tier: plain LINQ reads over history rows and tiles, with no bulk update, transaction or complex type between them. Nothing here calls the
/// allocator; what the reader returns is a batch for <c>ApplyResearch</c> to hand on.
/// </remarks>
[TestFixture]
internal class ExperienceAccrualReaderTests : InMemoryTestBase
{
    private const int _GAME_ID = 1;
    private const int _PLAYER_ID = 5;
    private const int _PLAYER_TURN_ID = 42;
    private const int _ANOTHER_TURN_ID = 41;

    private const int _JUNGLE_TILE_ID = 1_000;
    private const int _GRASSLAND_TILE_ID = 1_001;

    private const string _JUNGLE_CODE = "Biome.Jungle";
    private const string _GRASSLAND_CODE = "Biome.Grassland";

    private int _nextRowId = 1;

    #region Treading new ground

    [Test, Description("Entering an unimproved tile earns one point of its Biome's experience, which is how a Player learns ground by walking it.")]
    public async Task ShouldEarnOnePointPerBiomeWhenAForceEntersAnUnimprovedTileAsync()
    {
        await AddTileAsync(_JUNGLE_TILE_ID, BiomeType.Jungle);
        await AddTileAsync(_GRASSLAND_TILE_ID, BiomeType.Grassland);
        await AddMoveAsync(_JUNGLE_TILE_ID);
        await AddMoveAsync(_GRASSLAND_TILE_ID);

        IReadOnlyDictionary<string, int> earned = await ReadAsync();

        Assert.Multiple(() =>
        {
            Assert.That(earned[_JUNGLE_CODE], Is.EqualTo(1));
            Assert.That(earned[_GRASSLAND_CODE], Is.EqualTo(1));
        });
    }

    [Test, Description("A tile crossed repeatedly in one Turn pays once, so walking a Force back and forth cannot farm experience.")]
    public async Task ShouldPayOncePerTileWhenTheSameTileIsEnteredSeveralTimesAsync()
    {
        await AddTileAsync(_JUNGLE_TILE_ID, BiomeType.Jungle);
        await AddMoveAsync(_JUNGLE_TILE_ID, unitInstanceId: 1);
        await AddMoveAsync(_JUNGLE_TILE_ID, unitInstanceId: 1);
        await AddMoveAsync(_JUNGLE_TILE_ID, unitInstanceId: 1);

        IReadOnlyDictionary<string, int> earned = await ReadAsync();

        Assert.That(earned[_JUNGLE_CODE], Is.EqualTo(1));
    }

    [Test, Description("Two Forces crossing one tile pay once between them, because the Player learns the ground rather than each Force learning it separately.")]
    public async Task ShouldPayOncePerTileWhenTwoForcesCrossItAsync()
    {
        await AddTileAsync(_JUNGLE_TILE_ID, BiomeType.Jungle);
        await AddMoveAsync(_JUNGLE_TILE_ID, unitInstanceId: 1);
        await AddMoveAsync(_JUNGLE_TILE_ID, unitInstanceId: 2);

        IReadOnlyDictionary<string, int> earned = await ReadAsync();

        Assert.That(earned[_JUNGLE_CODE], Is.EqualTo(1));
    }

    [Test, Description("Only a Moved row earns anything: a Force appearing on a tile or being destroyed on one has not crossed it.")]
    public async Task ShouldEarnNothingWhenTheForceOnlyAppearedOnTheTileAsync()
    {
        await AddTileAsync(_JUNGLE_TILE_ID, BiomeType.Jungle);
        await AddMoveAsync(_JUNGLE_TILE_ID, eventType: ForceHistoryEventType.Created);

        IReadOnlyDictionary<string, int> earned = await ReadAsync();

        Assert.That(earned.ContainsKey(_JUNGLE_CODE), Is.False);
    }

    [Test, Description("Only this Turn's history counts, so last Turn's moves are not paid for a second time.")]
    public async Task ShouldIgnoreHistoryFromAnotherTurnAsync()
    {
        await AddTileAsync(_JUNGLE_TILE_ID, BiomeType.Jungle);
        await AddMoveAsync(_JUNGLE_TILE_ID, playerTurnId: _ANOTHER_TURN_ID);

        IReadOnlyDictionary<string, int> earned = await ReadAsync();

        Assert.That(earned.ContainsKey(_JUNGLE_CODE), Is.False);
    }

    #endregion

    #region What improved ground means

    [Test, Description("A tile carrying a standing Structure teaches nothing, whoever owns it, which is what makes unimproved ground the thing worth crossing.")]
    public async Task ShouldEarnNothingWhenTheTileCarriesAStandingStructureAsync()
    {
        await AddTileAsync(_JUNGLE_TILE_ID, BiomeType.Jungle);
        await AddStructureAsync(_JUNGLE_TILE_ID, completedOnTurnId: _ANOTHER_TURN_ID);
        await AddMoveAsync(_JUNGLE_TILE_ID);

        IReadOnlyDictionary<string, int> earned = await ReadAsync();

        Assert.That(earned.ContainsKey(_JUNGLE_CODE), Is.False);
    }

    [Test, Description("A Structure improves its tile from the moment it is founded rather than from completion, because half-built ground is occupied either way.")]
    public async Task ShouldEarnNothingWhenTheStructureOnTheTileIsStillUnderConstructionAsync()
    {
        await AddTileAsync(_JUNGLE_TILE_ID, BiomeType.Jungle);
        await AddStructureAsync(_JUNGLE_TILE_ID, completedOnTurnId: null);
        await AddMoveAsync(_JUNGLE_TILE_ID);

        IReadOnlyDictionary<string, int> earned = await ReadAsync();

        Assert.That(earned.ContainsKey(_JUNGLE_CODE), Is.False);
    }

    [Test, Description("A battle-map Structure still carries a campaign tile id, so it must not disqualify a campaign tile that is otherwise empty ground.")]
    public async Task ShouldStillEarnWhenTheOnlyStructureOnTheTileIsOnABattleMapAsync()
    {
        await AddTileAsync(_JUNGLE_TILE_ID, BiomeType.Jungle);
        await AddStructureAsync(_JUNGLE_TILE_ID, completedOnTurnId: _ANOTHER_TURN_ID, battleMapTileId: 77);
        await AddMoveAsync(_JUNGLE_TILE_ID);

        IReadOnlyDictionary<string, int> earned = await ReadAsync();

        Assert.That(earned[_JUNGLE_CODE], Is.EqualTo(1));
    }

    [Test, Description("A razed Structure leaves the ground unimproved again, so its tile teaches once more.")]
    public async Task ShouldEarnWhenTheOnlyStructureOnTheTileHasBeenRazedAsync()
    {
        await AddTileAsync(_JUNGLE_TILE_ID, BiomeType.Jungle);
        await AddStructureAsync(_JUNGLE_TILE_ID, completedOnTurnId: _ANOTHER_TURN_ID, removedOnTurnId: _ANOTHER_TURN_ID);
        await AddMoveAsync(_JUNGLE_TILE_ID);

        IReadOnlyDictionary<string, int> earned = await ReadAsync();

        Assert.That(earned[_JUNGLE_CODE], Is.EqualTo(1));
    }

    #endregion

    #region Settling

    [Test, Description("Completing a Settlement's Structure is worth 10 points of its tile's Biome, the one grant that rewards staying rather than passing through.")]
    public async Task ShouldEarnTenPointsWhenASettlementStructureIsCompletedAsync()
    {
        await AddTileAsync(_JUNGLE_TILE_ID, BiomeType.Jungle);
        await AddCompletedStructureHistoryAsync(_JUNGLE_TILE_ID, structureInstanceId: 900, withSettlement: true);

        IReadOnlyDictionary<string, int> earned = await ReadAsync();

        Assert.That(earned[_JUNGLE_CODE], Is.EqualTo(10));
    }

    [Test, Description("A completed Structure with no Settlement earns nothing, because the grant is for settling rather than for building.")]
    public async Task ShouldEarnNothingWhenTheCompletedStructureIsNotASettlementAsync()
    {
        await AddTileAsync(_JUNGLE_TILE_ID, BiomeType.Jungle);
        await AddCompletedStructureHistoryAsync(_JUNGLE_TILE_ID, structureInstanceId: 900, withSettlement: false);

        IReadOnlyDictionary<string, int> earned = await ReadAsync();

        Assert.That(earned.ContainsKey(_JUNGLE_CODE), Is.False);
    }

    [Test, Description("Crossing a tile and finishing a Settlement on it in one Turn pays only the Settlement, because the finished Structure improved the ground.")]
    public async Task ShouldPayOnlyTheSettlementWhenBothHappenOnOneTileAsync()
    {
        await AddTileAsync(_JUNGLE_TILE_ID, BiomeType.Jungle);
        await AddStructureAsync(_JUNGLE_TILE_ID, completedOnTurnId: _PLAYER_TURN_ID, structureInstanceId: 900);
        await AddCompletedStructureHistoryAsync(_JUNGLE_TILE_ID, structureInstanceId: 900, withSettlement: true, addStructureInstance: false);
        await AddMoveAsync(_JUNGLE_TILE_ID);

        IReadOnlyDictionary<string, int> earned = await ReadAsync();

        Assert.That(earned[_JUNGLE_CODE], Is.EqualTo(10));
    }

    #endregion

    #region Helpers

    private async Task<IReadOnlyDictionary<string, int>> ReadAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        ExperienceAccrualReader reader = new(NullLogger<ExperienceAccrualReader>.Instance);

        return await reader.ReadForTurnAsync(context, _PLAYER_ID, _PLAYER_TURN_ID, CancellationToken.None);
    }

    /// <summary>
    /// Adds one tile carrying a Biome.
    /// </summary>
    /// <remarks>
    /// No <c>CampaignBiome</c> or <c>CampaignGeography</c> row, and no navigation: the reader projects <c>BiomeTypeId</c> and never materialises either, so
    /// seeding them would only add rows for the test to collide on.
    /// </remarks>
    private async Task AddTileAsync(int tileId, BiomeType biomeType)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        context.CampaignMaps.Add(new CampaignMap
        {
            CampaignMapId = tileId,
            GameId = _GAME_ID,
            XCoord = tileId,
            YCoord = 0,
            BiomeTypeId = biomeType,
            CampaignMapGeographyTypeId = CampaignMapGeographyType.Flat,
            Biome = null!,
            Geography = null!,
            Game = null!,
            FirstVisitor = null,
            LastVisitor = null,
            OwnedBy = null
        });

        await context.SaveChangesAsync();
    }

    private async Task AddMoveAsync(
        int tileId,
        int unitInstanceId = 1,
        int playerTurnId = _PLAYER_TURN_ID,
        ForceHistoryEventType eventType = ForceHistoryEventType.Moved)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        context.MovementHistory.Add(new MovementHistory
        {
            Id = _nextRowId++,
            GameId = _GAME_ID,
            UnitInstanceId = unitInstanceId,
            PlayerId = _PLAYER_ID,
            UnitTemplateId = 1,
            EventType = eventType,
            FromCampaignMapTileId = eventType == ForceHistoryEventType.Moved ? tileId - 1 : null,
            CampaignMapTileId = tileId,
            PlayerTurnId = playerTurnId,
            TurnSequence = _nextRowId
        });

        await context.SaveChangesAsync();
    }

    private async Task AddStructureAsync(
        int tileId,
        int? completedOnTurnId,
        int structureInstanceId = 500,
        int? battleMapTileId = null,
        int? removedOnTurnId = null)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        context.StructureInstances.Add(NewStructureInstance(structureInstanceId, tileId, completedOnTurnId, battleMapTileId, removedOnTurnId));

        await context.SaveChangesAsync();
    }

    private async Task AddCompletedStructureHistoryAsync(
        int tileId,
        int structureInstanceId,
        bool withSettlement,
        bool addStructureInstance = true)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        if (addStructureInstance)
        {
            context.StructureInstances.Add(NewStructureInstance(structureInstanceId, tileId, _PLAYER_TURN_ID, battleMapTileId: null, removedOnTurnId: null));
        }

        if (withSettlement)
        {
            context.Settlements.Add(new Settlement
            {
                Id = structureInstanceId,
                GameId = _GAME_ID,
                StructureInstanceId = structureInstanceId,
                FoundingPlayerId = _PLAYER_ID,
                Name = "Test Village",
                Tier = SettlementTier.Village,
                Population = 100,
                FoundedOnRoundNumber = 1,
                FoundedOnTurnId = _PLAYER_TURN_ID
            });
        }

        context.StructureHistory.Add(new StructureHistory
        {
            Id = _nextRowId++,
            GameId = _GAME_ID,
            StructureInstanceId = structureInstanceId,
            StructureId = 1,
            PlayerId = _PLAYER_ID,
            EventType = StructureHistoryEventType.Completed,
            CompletionPercent = 100d,
            CampaignMapTileId = tileId,
            PlayerTurnId = _PLAYER_TURN_ID,
            TurnSequence = _nextRowId
        });

        await context.SaveChangesAsync();
    }

    private static StructureInstance NewStructureInstance(
        int structureInstanceId,
        int tileId,
        int? completedOnTurnId,
        int? battleMapTileId,
        int? removedOnTurnId)
    {
        return new StructureInstance
        {
            Id = structureInstanceId,
            GameId = _GAME_ID,
            CampaignMapTileId = tileId,
            BattleMapTileId = battleMapTileId,
            StructureId = 1,
            SlotType = TileSlotType.Primary,
            Position = PositionOnTile.WholeTile,
            OwnedByPlayerId = _PLAYER_ID,
            CreatedOnTurnId = _ANOTHER_TURN_ID,
            BuildPointsProgress = 0,
            BuildPointsPerTurn = 1,
            CompletedOnTurnId = completedOnTurnId,
            RemovedOnTurnId = removedOnTurnId
        };
    }

    #endregion
}
