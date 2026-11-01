using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Maps.CampaignMaps.Classes;
using AscendantEmpire.Application.Features.Maps.CampaignMaps.Services;
using AscendantEmpire.BackendContracts.CampaignMaps;
using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Entities.Maps.CampaignMaps;
using AscendantEmpire.Domain.Entities.Structures;
using AscendantEmpire.Domain.Entities.UnitDesigns;
using AscendantEmpire.Domain.Entities.Units;
using AscendantEmpire.Domain.Enums;
using AscendantEmpire.Domain.Enums.Forces;
using AscendantEmpire.Domain.Enums.Games;
using AscendantEmpire.Domain.Enums.Maps;
using AscendantEmpire.Domain.Enums.Structures;

namespace AscendantEmpire.Tests.Features.Maps.CampaignMaps;

/// <summary>
/// Tests for <see cref="SightDataReader"/>: that every query the sight rules depend on picks out the right rows and describes them correctly.
/// </summary>
/// <remarks>
/// <para>
/// On the in-memory tier. The reader deliberately uses no raw SQL, no bulk updates and no transactions, and reads only scalars off
/// <c>CompiledUnitTemplate</c>, so nothing here needs a container. If a change makes one of these tests start failing in the in-memory query shaper,
/// that change has made the reader read a damage profile and it belongs a tier up.
/// </para>
/// <para>
/// The Game is a 5 x 4 world map starting at id 100, Flat Grassland with a Jungle at (1, 1). Player 2 is the Player being read about; Player 3 is anyone
/// else. Turn 11 is the Turn in question and Turn 12 some other.
/// </para>
/// </remarks>
[TestFixture]
internal class SightDataReaderTests : InMemoryTestBase
{
    private const int _GAME_ID = 1;
    private const int _MAP_OFFSET = 100;
    private const int _MAP_WIDTH = 5;
    private const int _MAP_HEIGHT = 4;

    private const int _PLAYER_ID = 2;
    private const int _OTHER_PLAYER_ID = 3;

    private const int _PLAYER_TURN_ID = 11;
    private const int _OTHER_PLAYER_TURN_ID = 12;

    /// <summary>A template that sees three tiles, over water, with a Detection and Visibility no default could match by accident.</summary>
    private const int _TEMPLATE_ID = 7;

    /// <summary>The watchtower, which sees two tiles.</summary>
    private const int _WATCHTOWER_STRUCTURE_ID = 1;

    /// <summary>A fortress, which sees five tiles - further than anything else the Players have, so a reach that counts it can only have counted it.</summary>
    private const int _FORTRESS_STRUCTURE_ID = 2;

    private const int _STANDING_STRUCTURE_ID = 20;
    private const int _REMOVED_STRUCTURE_ID = 21;
    private const int _BATTLE_MAP_STRUCTURE_ID = 22;
    private const int _OTHER_PLAYERS_STRUCTURE_ID = 23;

    private const int _RAZED_THIS_TURN_STRUCTURE_ID = 24;
    private const int _CAPTURED_THIS_TURN_STRUCTURE_ID = 25;
    private const int _RAZED_IN_OTHER_TURN_STRUCTURE_ID = 26;
    private const int _CAPTURED_THEN_RAZED_STRUCTURE_ID = 27;

    private const int _UNIT_IN_JUNGLE_ID = 30;
    private const int _OTHER_PLAYERS_UNIT_ID = 31;
    private const int _FAR_UNIT_ID = 32;

    private const int _DESTROYED_UNIT_ID = 50;
    private const int _DESTROYED_IN_OTHER_TURN_UNIT_ID = 51;
    private const int _OTHER_PLAYERS_DESTROYED_UNIT_ID = 52;

    private const int _DESTROYED_AT_SEQUENCE = 9;
    private const int _RAZED_AT_SEQUENCE = 7;
    private const int _CAPTURED_AT_SEQUENCE = 5;

    private readonly SightDataReader _reader = new();

    [SetUp]
    public async Task SeedGameAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        context.GameIndex.Add(new GameIndex
        {
            Id = _GAME_ID,
            GameName = "Sight Game",
            NumberOfPlayers = 2,
            CreationStatus = GameCreationStatus.SetupComplete,
            PlayStatus = GamePlayStatus.InProgress,
            CreatedAt = DateTimeOffset.UtcNow,
            GenerationSeed = 1,
            BattleMapSize = TestGameSettings.MINIMUM_BATTLE_MAP_SIZE,
            CampaignMapOffsetId = _MAP_OFFSET,
            MapWidth = _MAP_WIDTH,
            MapHeight = _MAP_HEIGHT,
            HorizontalWrap = false,
            VerticalWrap = true
        });

        var grassland = new CampaignBiome 
        { 
            BiomeTypeId = BiomeType.Grassland, 
            Name = "Grassland", 
            MovementCost = 1m, 
            IsTraversable = true, 
            MapEditorHtmlColor = "#7cb342" 
        };

        var jungle = new CampaignBiome 
        { 
            BiomeTypeId = BiomeType.Jungle, 
            Name = "Jungle", 
            MovementCost = 2m, 
            DoesBlockSight = true, 
            IsTraversable = true, 
            MapEditorHtmlColor = "#006400" 
        };

        var flat = new CampaignGeography 
        { 
            CampaignMapGeographyTypeId = CampaignMapGeographyType.Flat, 
            Name = "Flat", 
            RelativeElevation = 0, 
            MovementCost = 1m, 
            IsWater = false, 
            IsTraversable = true, 
            MapEditorIcon = null, 
            IsPaintableOnCampaignMap = true 
        };
        var hilly = new CampaignGeography 
        { 
            CampaignMapGeographyTypeId = CampaignMapGeographyType.Hilly, 
            Name = "Hilly", 
            RelativeElevation = 1, 
            MovementCost = 2m, 
            IsWater = false, 
            IsTraversable = true, 
            MapEditorIcon = null, 
            IsPaintableOnCampaignMap = true 
        };

        var mountainous = new CampaignGeography
        {
            CampaignMapGeographyTypeId = CampaignMapGeographyType.Mountainous,
            Name = "Mountainous",
            RelativeElevation = 3,
            MovementCost = CampaignGeography.IMPASSABLE_MOVEMENT_COST,
            IsWater = false,
            IsTraversable = false,
            MapEditorIcon = null,
            IsPaintableOnCampaignMap = true
        };

        context.CampaignBiomes.AddRange(grassland, jungle);
        context.CampaignGeographies.AddRange(flat, hilly, mountainous);

        for (int y = 0; y < _MAP_HEIGHT; y++)
        {
            for (int x = 0; x < _MAP_WIDTH; x++)
            {
                CampaignBiome biome = (x, y) == (1, 1) ? jungle : grassland;

                context.CampaignMaps.Add(new CampaignMap
                {
                    CampaignMapId = TileId(x, y),
                    GameId = _GAME_ID,
                    XCoord = x,
                    YCoord = y,
                    BiomeTypeId = biome.BiomeTypeId,
                    CampaignMapGeographyTypeId = flat.CampaignMapGeographyTypeId,
                    Biome = biome,
                    Geography = flat,
                    Game = null!,
                    FirstVisitor = null,
                    LastVisitor = null,
                    OwnedBy = null
                });
            }
        }

        context.UnitTemplates.Add(new UnitTemplate { Id = _TEMPLATE_ID, DisplayName = "Galley", MapImagePath = "galley.png", BodyComponentId = 1 });
        context.CompiledUnitTemplates.Add(new CompiledUnitTemplate
        {
            UnitTemplateId = _TEMPLATE_ID,
            DisplayName = "Galley",
            MapImagePath = "galley.png",
            BodyComponentId = 1,
            MovementDomain = DomainType.Water,
            TotalSight = 3000,
            TotalDetection = 1500,
            TotalVisibility = 700
        });

        context.Set<Structure>().AddRange(NewStructureType(_WATCHTOWER_STRUCTURE_ID, "watchtower", sightRange: 2), NewStructureType(_FORTRESS_STRUCTURE_ID, "fortress", sightRange: 5));

        context.StructureInstances.AddRange(
            NewStructure(_STANDING_STRUCTURE_ID, _PLAYER_ID, x: 0, y: 0),
            NewStructure(_REMOVED_STRUCTURE_ID, _PLAYER_ID, x: 1, y: 0, removedOnTurnId: 5),
            NewStructure(_BATTLE_MAP_STRUCTURE_ID, _PLAYER_ID, x: 2, y: 0, battleMapTileId: 9),
            NewStructure(_OTHER_PLAYERS_STRUCTURE_ID, _OTHER_PLAYER_ID, x: 0, y: 1));

        context.UnitInstances.AddRange(
            NewUnit(_UNIT_IN_JUNGLE_ID, _PLAYER_ID, x: 1, y: 1, healthyCount: 40),
            NewUnit(_OTHER_PLAYERS_UNIT_ID, _OTHER_PLAYER_ID, x: 0, y: 2, healthyCount: 10),
            NewUnit(_FAR_UNIT_ID, _PLAYER_ID, x: 4, y: 3, healthyCount: 10));

        context.MovementHistory.AddRange(
            NewDestroyedRow(_DESTROYED_UNIT_ID, _PLAYER_ID, x: 2, y: 1, _PLAYER_TURN_ID, _DESTROYED_AT_SEQUENCE),
            NewDestroyedRow(_DESTROYED_IN_OTHER_TURN_UNIT_ID, _PLAYER_ID, x: 2, y: 2, _OTHER_PLAYER_TURN_ID, 4),
            NewDestroyedRow(_OTHER_PLAYERS_DESTROYED_UNIT_ID, _OTHER_PLAYER_ID, x: 2, y: 1, _PLAYER_TURN_ID, 5),
            // A move in the Turn in question, which is history but not a destruction, and must not turn a standing Unit into a dead one.
            new MovementHistory
            {
                GameId = _GAME_ID,
                UnitInstanceId = _UNIT_IN_JUNGLE_ID,
                PlayerId = _PLAYER_ID,
                UnitTemplateId = _TEMPLATE_ID,
                EventType = ForceHistoryEventType.Moved,
                FromCampaignMapTileId = TileId(0, 1),
                CampaignMapTileId = TileId(1, 1),
                PlayerTurnId = _PLAYER_TURN_ID,
                TurnSequence = 2
            });

        await context.SaveChangesAsync();
    }

    [Test, Description("The geometry comes off the Game itself, world-map offset included, so ids built from it name that Game's tiles.")]
    public async Task ShouldReadTheGeometryOffTheGameAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        CampaignMapGeometry? geometry = await _reader.GetGeometryAsync(context, _GAME_ID);
        CampaignMapGeometry? missing = await _reader.GetGeometryAsync(context, 999);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(geometry, Is.EqualTo(new CampaignMapGeometry(_MAP_OFFSET, _MAP_WIDTH, _MAP_HEIGHT, WrapsHorizontally: false, WrapsVertically: true)));
            Assert.That(missing, Is.Null, "A Game that does not exist has no geometry, rather than a default one.");
        }
    }

    [Test, Description("Terrain is read from the world map and keyed by coordinate, and an id belonging to no tile of the Game is simply absent.")]
    public async Task ShouldReadTerrainFromTheWorldMapKeyedByCoordinateAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        IReadOnlyDictionary<(int X, int Y), SightTerrain> terrain = await _reader.GetTerrainAsync(context, _GAME_ID, [TileId(1, 1), TileId(2, 2), 99_999]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(terrain.Keys, Is.EquivalentTo([(1, 1), (2, 2)]));
            Assert.That(terrain[(1, 1)].CampaignMapId, Is.EqualTo(TileId(1, 1)));
            Assert.That(terrain[(1, 1)].DoesBlockSight, Is.True, "The Jungle's blocking flag comes through from its Biome.");
            Assert.That(terrain[(1, 1)].BiomeName, Is.EqualTo("Jungle"));
            Assert.That(terrain[(2, 2)].DoesBlockSight, Is.False);
        }
    }

    [Test, Description("A Player's Observers are their standing campaign Structures then their Units, with a Unit's Sight divided down to whole tiles.")]
    public async Task ShouldDescribeAPlayersStandingObserversAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        IReadOnlyCollection<SightObserver> observers = await _reader.GetPlayerObserversAsync(context, _GAME_ID, _PLAYER_ID);

        SightObserver structure = observers.First();
        SightObserver unit = observers.Single(observer => observer.Id == _UNIT_IN_JUNGLE_ID);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(observers.Select(observer => observer.Id), Is.EquivalentTo([_STANDING_STRUCTURE_ID, _UNIT_IN_JUNGLE_ID, _FAR_UNIT_ID]),
                "Removed and battle-map Structures watch nothing on the campaign map, and another Player's Observers are not this Player's.");

            Assert.That(structure.RecordType, Is.EqualTo(RecordType.Structure), "Structures come first, as the map read has always reported them.");
            Assert.That(structure.BaseSightRange, Is.EqualTo(2));
            Assert.That(structure.Detection, Is.EqualTo(SightingFidelityRules.STRUCTURE_DETECTION));
            Assert.That(structure.MovementDomain, Is.EqualTo(DomainType.Land), "A Structure is judged as a land Force when blinded.");
            Assert.That(structure.Location, Is.EqualTo(new Coord(0, 0)));

            Assert.That(unit.RecordType, Is.EqualTo(RecordType.Unit));
            Assert.That(unit.BaseSightRange, Is.EqualTo(3), "A compiled Sight of 3,000 is three whole tiles.");
            Assert.That(unit.Detection, Is.EqualTo(1500));
            Assert.That(unit.MovementDomain, Is.EqualTo(DomainType.Water));
            Assert.That(unit.HealthyTroops, Is.EqualTo(40));
            Assert.That(unit.UnitTemplateId, Is.EqualTo(_TEMPLATE_ID));
            Assert.That(unit.Location, Is.EqualTo(new Coord(1, 1)));
            Assert.That(unit.DestroyedAtSequence, Is.Null);
        }
    }

    [Test, Description("The search finds the observing Players' Observers on the searched tiles, including Units destroyed there this Turn and when.")]
    public async Task ShouldFindObserversOnTheSearchedTilesIncludingThoseDestroyedThisTurnAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        IReadOnlyCollection<SightObserver> observers = await _reader.GetObserversOnTilesAsync(context, _GAME_ID, [_PLAYER_ID], _PLAYER_TURN_ID, TilesAround(1, 1));

        SightObserver destroyed = observers.Single(observer => observer.Id == _DESTROYED_UNIT_ID);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(observers.Select(observer => observer.Id), Is.EquivalentTo([_STANDING_STRUCTURE_ID, _UNIT_IN_JUNGLE_ID, _DESTROYED_UNIT_ID]),
                "Not another Player's, not anything off the searched tiles, and not a Unit destroyed in some other Turn.");

            Assert.That(destroyed.DestroyedAtSequence, Is.EqualTo(_DESTROYED_AT_SEQUENCE));
            Assert.That(destroyed.Location, Is.EqualTo(new Coord(2, 1)), "A destroyed Unit watched from where it died.");
            Assert.That(destroyed.BaseSightRange, Is.EqualTo(3), "Its Sight comes from the template its history row recorded.");
            Assert.That(destroyed.MovementDomain, Is.EqualTo(DomainType.Water));
            Assert.That(destroyed.Detection, Is.EqualTo(1500));
            Assert.That(destroyed.HealthyTroops, Is.Null, "Nothing records what was left of it.");
            Assert.That(observers.Single(observer => observer.Id == _UNIT_IN_JUNGLE_ID).DestroyedAtSequence, Is.Null, "A Unit that only moved this Turn is still standing.");
        }
    }

    [Test, Description("A Unit with both a row and a destruction record is counted once, as destroyed, so it never witnesses past its death.")]
    public async Task ShouldPreferTheDestroyedRecordWhenAUnitStillHasARowAsync()
    {
        await using (AscendantEmpireContext seedContext = await DbFactory.CreateDbContextAsync())
        {
            seedContext.MovementHistory.Add(NewDestroyedRow(_UNIT_IN_JUNGLE_ID, _PLAYER_ID, x: 1, y: 1, _PLAYER_TURN_ID, turnSequence: 6));
            await seedContext.SaveChangesAsync();
        }

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        IReadOnlyCollection<SightObserver> observers = await _reader.GetObserversOnTilesAsync(context, _GAME_ID, [_PLAYER_ID], _PLAYER_TURN_ID, TilesAround(1, 1));

        SightObserver unit = observers.Single(observer => observer.Id == _UNIT_IN_JUNGLE_ID);

        Assert.That(unit.DestroyedAtSequence, Is.EqualTo(6));
    }

    [Test, Description("A Structure razed or captured this Turn still watches for the Player who held it, until the sequence it was lost at, so its earlier sightings are not lost with it.")]
    public async Task ShouldFindStructuresLostThisTurnAsTheirFormerOwnersObserversAsync()
    {
        await SeedLostStructuresAsync();

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        IReadOnlyCollection<SightObserver> observers = await _reader.GetObserversOnTilesAsync(context, _GAME_ID, [_PLAYER_ID], _PLAYER_TURN_ID, TilesAround(1, 1));

        SightObserver razed = observers.Single(observer => observer.Id == _RAZED_THIS_TURN_STRUCTURE_ID);
        SightObserver captured = observers.Single(observer => observer.Id == _CAPTURED_THIS_TURN_STRUCTURE_ID);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(observers.Select(observer => observer.Id), Is.EquivalentTo(
            [
                _STANDING_STRUCTURE_ID,
                _UNIT_IN_JUNGLE_ID,
                _DESTROYED_UNIT_ID,
                _RAZED_THIS_TURN_STRUCTURE_ID,
                _CAPTURED_THIS_TURN_STRUCTURE_ID,
                _CAPTURED_THEN_RAZED_STRUCTURE_ID
            ]), "Not a Structure razed in some other Turn, which the Player had already lost before this one began.");

            Assert.That(razed.RecordType, Is.EqualTo(RecordType.Structure));
            Assert.That(razed.PlayerId, Is.EqualTo(_PLAYER_ID));
            Assert.That(razed.DestroyedAtSequence, Is.EqualTo(_RAZED_AT_SEQUENCE));
            Assert.That(razed.Location, Is.EqualTo(new Coord(0, 2)), "A razed Structure watched from where it stood.");
            Assert.That(razed.BaseSightRange, Is.EqualTo(2), "Its Sight comes from its kind of Structure.");
            Assert.That(razed.Detection, Is.EqualTo(SightingFidelityRules.STRUCTURE_DETECTION));

            Assert.That(captured.PlayerId, Is.EqualTo(_PLAYER_ID), "A captured Structure watches for the Player it was taken from, not the one who holds it now.");
            Assert.That(captured.DestroyedAtSequence, Is.EqualTo(_CAPTURED_AT_SEQUENCE));

            Assert.That(observers.Single(observer => observer.Id == _CAPTURED_THEN_RAZED_STRUCTURE_ID).DestroyedAtSequence, Is.EqualTo(3),
                "Taken at 3 and razed at 8, it was lost to this Player at the capture: only the first loss counts.");
        }
    }

    [Test, Description("A Structure lost this Turn is only its former owner's Observer, so the Player who took it does not gain its earlier sight, and nor does a later owner.")]
    public async Task ShouldNotCountALostStructureForAnyoneButItsFormerOwnerAsync()
    {
        await SeedLostStructuresAsync();

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        IReadOnlyCollection<SightObserver> observers = await _reader.GetObserversOnTilesAsync(context, _GAME_ID, [_OTHER_PLAYER_ID], _PLAYER_TURN_ID, TilesAround(1, 1));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(observers.Select(observer => observer.Id), Does.Not.Contain(_RAZED_THIS_TURN_STRUCTURE_ID));
            Assert.That(observers.Select(observer => observer.Id), Does.Not.Contain(_CAPTURED_THEN_RAZED_STRUCTURE_ID), "Razed by its captor, it stands for nobody now, and was never theirs before.");

            SightObserver captured = observers.Single(observer => observer.Id == _CAPTURED_THIS_TURN_STRUCTURE_ID);

            Assert.That(captured.PlayerId, Is.EqualTo(_OTHER_PLAYER_ID), "To its captor it is simply a standing Structure.");
            Assert.That(captured.DestroyedAtSequence, Is.Null);
        }
    }

    [Test, Description("A Structure and a Unit that happen to share an id are both Observers, since the two are numbered independently and one must never hide the other.")]
    public async Task ShouldKeepAStandingUnitThatSharesAnIdWithALostStructureAsync()
    {
        await SeedLostStructuresAsync();

        await using (AscendantEmpireContext seedContext = await DbFactory.CreateDbContextAsync())
        {
            seedContext.UnitInstances.Add(NewUnit(_RAZED_THIS_TURN_STRUCTURE_ID, _PLAYER_ID, x: 2, y: 2, healthyCount: 10));
            await seedContext.SaveChangesAsync();
        }

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        IReadOnlyCollection<SightObserver> observers = await _reader.GetObserversOnTilesAsync(context, _GAME_ID, [_PLAYER_ID], _PLAYER_TURN_ID, TilesAround(1, 1));

        IEnumerable<RecordType> sharingTheId = observers.Where(observer => observer.Id == _RAZED_THIS_TURN_STRUCTURE_ID).Select(observer => observer.RecordType);

        Assert.That(sharingTheId, Is.EquivalentTo([RecordType.Structure, RecordType.Unit]));
    }

    [Test, Description("The search box reaches the best Sight among the Players' Observers plus the highest ground a Force can stand on, never a Mountain.")]
    public async Task ShouldReachTheBestSightPlusTheHighestEnterableGroundAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        int reach = await _reader.GetMaxSightReachAsync(context, _GAME_ID, [_PLAYER_ID], _PLAYER_TURN_ID);

        // The galley's three tiles beat the watchtower's two, and Hilly's one is the highest enterable ground. Counting the Mountain would have made it six.
        Assert.That(reach, Is.EqualTo(4));
    }

    [Test, Description("A Structure lost this Turn counts towards the search box, or an Observer that watched before it fell could stand outside the box and never be asked.")]
    public async Task ShouldReachTheSightOfAStructureLostThisTurnAsync()
    {
        await using (AscendantEmpireContext seedContext = await DbFactory.CreateDbContextAsync())
        {
            seedContext.StructureInstances.Add(NewStructure(40, _PLAYER_ID, x: 3, y: 3, removedOnTurnId: _PLAYER_TURN_ID, structureId: _FORTRESS_STRUCTURE_ID));
            seedContext.StructureHistory.Add(NewStructureRow(40, StructureHistoryEventType.Razed, _PLAYER_ID, previousOwnerPlayerId: null, x: 3, y: 3, _PLAYER_TURN_ID, turnSequence: 4, structureId: _FORTRESS_STRUCTURE_ID));
            await seedContext.SaveChangesAsync();
        }

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        int reach = await _reader.GetMaxSightReachAsync(context, _GAME_ID, [_PLAYER_ID], _PLAYER_TURN_ID);

        // The razed fortress's five tiles, plus Hilly's one.
        Assert.That(reach, Is.EqualTo(6));
    }

    [Test, Description("A Force probe's Visibility is the compiled template's, and a template with no snapshot is simply absent.")]
    public async Task ShouldReadTheCompiledVisibilityOfEachTemplateAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        IReadOnlyDictionary<int, long> visibility = await _reader.GetTemplateVisibilityAsync(context, [_TEMPLATE_ID, 999]);

        Assert.That(visibility, Is.EquivalentTo(new Dictionary<int, long> { [_TEMPLATE_ID] = 700 }));
    }

    /// <summary>
    /// Seeds the Structures lost around (1, 1): one of Player 2's razed this Turn, one captured from Player 2 this Turn, one of Player 2's razed in another Turn, and one captured from Player 2
    /// at 3 and then razed by its captor at 8.
    /// </summary>
    private async Task SeedLostStructuresAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        context.StructureInstances.AddRange(
            NewStructure(_RAZED_THIS_TURN_STRUCTURE_ID, _PLAYER_ID, x: 0, y: 2, removedOnTurnId: _PLAYER_TURN_ID),
            NewStructure(_CAPTURED_THIS_TURN_STRUCTURE_ID, _OTHER_PLAYER_ID, x: 2, y: 2),
            NewStructure(_RAZED_IN_OTHER_TURN_STRUCTURE_ID, _PLAYER_ID, x: 1, y: 2, removedOnTurnId: _OTHER_PLAYER_TURN_ID),
            NewStructure(_CAPTURED_THEN_RAZED_STRUCTURE_ID, _OTHER_PLAYER_ID, x: 1, y: 0, removedOnTurnId: _PLAYER_TURN_ID));

        context.StructureHistory.AddRange(
            NewStructureRow(_RAZED_THIS_TURN_STRUCTURE_ID, StructureHistoryEventType.Razed, _PLAYER_ID, previousOwnerPlayerId: null, x: 0, y: 2, _PLAYER_TURN_ID, _RAZED_AT_SEQUENCE),
            NewStructureRow(_CAPTURED_THIS_TURN_STRUCTURE_ID, StructureHistoryEventType.Captured, _OTHER_PLAYER_ID, previousOwnerPlayerId: _PLAYER_ID, x: 2, y: 2, _PLAYER_TURN_ID, _CAPTURED_AT_SEQUENCE),
            NewStructureRow(_RAZED_IN_OTHER_TURN_STRUCTURE_ID, StructureHistoryEventType.Razed, _PLAYER_ID, previousOwnerPlayerId: null, x: 1, y: 2, _OTHER_PLAYER_TURN_ID, turnSequence: 2),
            // Listed razing first, so the test proves the first loss is chosen by sequence rather than by the order rows happen to come back in.
            NewStructureRow(_CAPTURED_THEN_RAZED_STRUCTURE_ID, StructureHistoryEventType.Razed, _OTHER_PLAYER_ID, previousOwnerPlayerId: null, x: 1, y: 0, _PLAYER_TURN_ID, turnSequence: 8),
            NewStructureRow(_CAPTURED_THEN_RAZED_STRUCTURE_ID, StructureHistoryEventType.Captured, _OTHER_PLAYER_ID, previousOwnerPlayerId: _PLAYER_ID, x: 1, y: 0, _PLAYER_TURN_ID, turnSequence: 3));

        await context.SaveChangesAsync();
    }

    private static int TileId(int x, int y)
    {
        return _MAP_OFFSET + x + (y * _MAP_WIDTH);
    }

    /// <summary>The ids of the three-by-three block centred on a tile, which is all the search tests need of a box.</summary>
    private static IEnumerable<int> TilesAround(int centreX, int centreY)
    {
        for (int y = centreY - 1; y <= centreY + 1; y++)
        {
            for (int x = centreX - 1; x <= centreX + 1; x++)
            {
                yield return TileId(x, y);
            }
        }
    }

    private static Structure NewStructureType(int id, string code, int sightRange)
    {
        return new Structure
        {
            Id = id,
            Code = code,
            Name = code,
            RequiresSlotType = TileSlotType.Primary,
            AllowedPositions = PositionOnTile.WholeTile,
            CanBeBuiltOnCampaignMap = true,
            CanBeBuiltOnBattleMap = false,
            DualMapBuildTrigger = false,
            BuildPointsRequired = 0,
            SightRange = sightRange,
            SightDirection = DetectionDirection.FullRadius,
            Asserts = StructureAssertion.Owned,
            MapImagePath = $"{code}.png",
            FullImagePath = $"{code}-full.png"
        };
    }

    private static StructureInstance NewStructure(int id, int ownerPlayerId, int x, int y, int? removedOnTurnId = null, int? battleMapTileId = null, int structureId = _WATCHTOWER_STRUCTURE_ID)
    {
        return new StructureInstance
        {
            Id = id,
            GameId = _GAME_ID,
            CampaignMapTileId = TileId(x, y),
            BattleMapTileId = battleMapTileId,
            StructureId = structureId,
            SlotType = TileSlotType.Primary,
            Position = PositionOnTile.WholeTile,
            OwnedByPlayerId = ownerPlayerId,
            CreatedOnTurnId = 1,
            RemovedOnTurnId = removedOnTurnId
        };
    }

    private static StructureHistory NewStructureRow(
        int structureInstanceId,
        StructureHistoryEventType eventType,
        int playerId,
        int? previousOwnerPlayerId,
        int x,
        int y,
        int playerTurnId,
        int turnSequence,
        int structureId = _WATCHTOWER_STRUCTURE_ID)
    {
        return new StructureHistory
        {
            GameId = _GAME_ID,
            StructureInstanceId = structureInstanceId,
            StructureId = structureId,
            PlayerId = playerId,
            PreviousOwnerPlayerId = previousOwnerPlayerId,
            EventType = eventType,
            CampaignMapTileId = TileId(x, y),
            PlayerTurnId = playerTurnId,
            TurnSequence = turnSequence
        };
    }

    private static UnitInstance NewUnit(int id, int playerId, int x, int y, int healthyCount)
    {
        return new UnitInstance
        {
            Id = id,
            GameId = _GAME_ID,
            PlayerId = playerId,
            CampaignMapTileId = TileId(x, y),
            UnitTemplateId = _TEMPLATE_ID,
            InitialStrength = healthyCount,
            HealthyCount = healthyCount
        };
    }

    private static MovementHistory NewDestroyedRow(int unitInstanceId, int playerId, int x, int y, int playerTurnId, int turnSequence)
    {
        return new MovementHistory
        {
            GameId = _GAME_ID,
            UnitInstanceId = unitInstanceId,
            PlayerId = playerId,
            UnitTemplateId = _TEMPLATE_ID,
            EventType = ForceHistoryEventType.Destroyed,
            CampaignMapTileId = TileId(x, y),
            PlayerTurnId = playerTurnId,
            TurnSequence = turnSequence
        };
    }
}
