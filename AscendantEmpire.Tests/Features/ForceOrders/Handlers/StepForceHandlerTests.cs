using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Battles.Services;
using AscendantEmpire.Application.Features.ForceOrders.Handlers;
using AscendantEmpire.Application.Features.ForceOrders.Movement.Strategies;
using AscendantEmpire.Application.Features.Game.Handlers;
using AscendantEmpire.Application.Features.Maps.CampaignMaps.Handlers;
using AscendantEmpire.Application.Features.Turns.Services;
using AscendantEmpire.Application.Features.Units.Services;
using AscendantEmpire.BackendContracts.CampaignMaps.GetPlayerCampaignMap;
using AscendantEmpire.BackendContracts.ForceOrders.StepForce;
using AscendantEmpire.BackendContracts.Games.GetGameContext;
using AscendantEmpire.BackendContracts.Turns.GetCurrentCampaignTurn;
using AscendantEmpire.Domain.Entities.Battles;
using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Entities.Maps.CampaignMaps;
using AscendantEmpire.Domain.Entities.UnitDesigns;
using AscendantEmpire.Domain.Entities.Units;
using AscendantEmpire.Domain.Enums.Forces;
using AscendantEmpire.Domain.Enums.Maps;

using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Tests.Features.ForceOrders.Handlers;

/// <summary>
/// Tests for <see cref="StepForceHandler"/>, the single-tile campaign move.
/// </summary>
/// <remarks>
/// <para>
/// On the in-memory tier. The handler's own database work is an ordinary query, an update and a <c>SaveChanges</c>, and although it reads
/// <see cref="CompiledUnitTemplate"/> — which owns EF complex types the in-memory provider cannot materialise — it only ever touches that entity's
/// scalar columns, which <c>testing.md</c> records as safe. The fog-of-war refresh is the part that needs raw SQL, and it is mocked out here.
/// </para>
/// <para>
/// The lifecycle writer is the real one, so a step's <see cref="MovementHistory"/> row is genuinely written and read back. Only the sequence allocator under it is substituted, because
/// allocation is a raw SQL statement - covered on the containerised tier by the Player Turn fixtures.
/// </para>
/// <para>
/// The map is a 5 x 5 grid that does not wrap. Row 2 is flat grassland, row 1 is ocean, row 3 is mountain, so a Force starting in the middle of
/// row 2 has open ground east and west, water to the north and an impassable tile to the south.
/// </para>
/// </remarks>
[TestFixture]
internal class StepForceHandlerTests : InMemoryTestBase
{
    private const int _GAME_ID = 1;
    private const int _PLAYER_ID = 1;
    private const int _OTHER_PLAYER_ID = 2;
    private const int _FORCE_ID = 5;
    private const int _TEMPLATE_ID = 1;

    private const int _MAP_WIDTH = 5;
    private const int _MAP_HEIGHT = 5;
    private const int _MAP_OFFSET = 1_000;

    /// <summary>The Force starts here: the middle of the open row.</summary>
    private const int _START_X = 2;
    private const int _START_Y = 2;

    /// <summary>A full turn's movement for the test template — 2.0 points on the Scale 1,000 convention.</summary>
    private const long _CAMPAIGN_MOVEMENT_POINTS = 2_000;

    /// <summary>The open Turn the current campaign turn reports, which every recorded step is stamped with.</summary>
    private const int _PLAYER_TURN_ID = 7;

    /// <summary>The sequence number the substituted allocator hands out. Deliberately not 1, so a row stamped with a default would not pass by coincidence.</summary>
    private const int _TURN_SEQUENCE = 3;

    /// <summary>The id the substituted engagement service reports for a Battle it started.</summary>
    private const int _BATTLE_ID = 11;

    /// <summary>An enemy Force's id, distinct from the stepping Force's.</summary>
    private const int _ENEMY_FORCE_ID = 6;

    private IMediator _mediator = null!;
    private ICampaignTurnService _campaignTurnService = null!;
    private IPlayerTurnService _playerTurnService = null!;
    private IBattleEngagementService _battleEngagementService = null!;
    private StepForceHandler _handler = null!;

    [SetUp]
    public async Task SetUp()
    {
        _mediator = Substitute.For<IMediator>();
        _campaignTurnService = Substitute.For<ICampaignTurnService>();
        _playerTurnService = Substitute.For<IPlayerTurnService>();

        // Starting a Battle saves inside a transaction and allocates sequences with raw SQL, neither of which this tier can do, so the engagement itself is substituted. What is under
        // test here is the handler's decision to start one and what it does with the answer; the engagement is proven on the containerised tier by BattleEngagementServicePersistenceTests.
        _battleEngagementService = Substitute.For<IBattleEngagementService>();

        _battleEngagementService.CommenceAsync(Arg.Any<AscendantEmpireContext>(), Arg.Any<BattleEngagement>(), Arg.Any<CancellationToken>())
            .Returns(ServiceResult<int>.Succeed(_BATTLE_ID));

        // The Turn is open by default and hands out one number, so a step can be recorded unless a test says otherwise.
        _playerTurnService.AllocateSequenceAsync(Arg.Any<AscendantEmpireContext>(), _PLAYER_TURN_ID, 1, Arg.Any<CancellationToken>())
            .Returns(ServiceResult<TurnSequenceBlock>.Succeed(new TurnSequenceBlock(_TURN_SEQUENCE, _TURN_SEQUENCE)));

        _mediator.Send(Arg.Any<GetGameContextRequest>(), Arg.Any<CancellationToken>())
            .Returns(ServiceResult<IReadOnlyCollection<GameContext>>.Succeed([GameContextInfo()]));

        // The step commits before this runs, so an empty refresh is a valid answer: the player simply gets no new terrain with the response.
        _mediator.Send(Arg.Any<GetPlayerCampaignMapRequest>(), Arg.Any<CancellationToken>())
            .Returns(ServiceResult<IReadOnlyCollection<ObserverSight>>.Succeed([]));

        // Most tests are not about the turn queue, so by default it is this player's turn.
        _campaignTurnService.GetCurrentCampaignTurnAsync(_GAME_ID, Arg.Any<CancellationToken>())
            .Returns(ServiceResult<CurrentCampaignTurn>.Succeed(CurrentTurn(_PLAYER_ID)));

        _handler = new StepForceHandler(
            new NullLogger<StepForceHandler>(),
            DbFactory,
            _mediator,
            _campaignTurnService,
            AllMovementDomainStrategies(),
            new ForceLifecycleWriter(NullLogger<ForceLifecycleWriter>.Instance, _playerTurnService),
            _battleEngagementService);

        await SeedMapAndTemplateAsync();
    }

    private static IReadOnlyCollection<IMovementDomainStrategy> AllMovementDomainStrategies()
    {
        return
        [
            new LandMovementStrategy(),
            new WaterMovementStrategy(),
            new BelowWaterMovementStrategy(),
            new AirMovementStrategy(),
            new NearEarthOrbitMovementStrategy()
        ];
    }

    private static GameContext GameContextInfo()
    {
        return new GameContext
        {
            GameId = _GAME_ID,
            GameName = "Test Game",
            NumberOfPlayers = 2,
            MapWidth = _MAP_WIDTH,
            MapHeight = _MAP_HEIGHT,
            CampaignMapOffsetId = _MAP_OFFSET,
            HasHorizontalWrapping = false,
            HasVerticalWrapping = false,
            Players =
            [
                new GameContextPlayer
                {
                    PlayerId = _PLAYER_ID,
                    RulerTitle = "King",
                    RulerName = "Player 1",
                    FactionName = "Faction 1",
                    RulerLabel = "King Player 1",
                    CampaignMapOffsetId = 5_000
                }
            ]
        };
    }

    private static CurrentCampaignTurn CurrentTurn(int playerId)
    {
        return new CurrentCampaignTurn
        {
            GameId = _GAME_ID,
            TurnQueueParticipantId = 1,
            TurnQueueId = 1,
            PlayerId = playerId,
            TurnOrder = 1,
            IsLastTurnInRound = false,
            CurrentRoundNumber = 1,
            FactionName = playerId == _PLAYER_ID ? "Faction 1" : "Faction 2",
            PlayerTurnId = _PLAYER_TURN_ID
        };
    }

    /// <summary>The world-map tile id for a coordinate, laid out the way the rest of the game addresses tiles.</summary>
    private static int TileId(int x, int y)
    {
        return _MAP_OFFSET + x + (y * _MAP_WIDTH);
    }

    /// <summary>
    /// Builds the 5 x 5 map, the geographies and biomes it draws on, and the template the test Force is fielded from.
    /// </summary>
    private async Task SeedMapAndTemplateAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        // Costs and flags copied from the seeded catalogue so the arithmetic under test is the arithmetic the game actually uses.
        var flat = new CampaignGeography { CampaignMapGeographyTypeId = CampaignMapGeographyType.Flat, Name = "Flat", MovementCost = 1m, MapEditorIcon = null, IsPaintableOnCampaignMap = true };
        var hilly = new CampaignGeography { CampaignMapGeographyTypeId = CampaignMapGeographyType.Hilly, Name = "Hilly", MovementCost = 2m, MapEditorIcon = null, IsPaintableOnCampaignMap = true };
        var deepWater = new CampaignGeography { CampaignMapGeographyTypeId = CampaignMapGeographyType.DeepWater, Name = "Deep Water", MovementCost = 1m, MapEditorIcon = null, IsPaintableOnCampaignMap = true };
        var mountainous = new CampaignGeography
        {
            CampaignMapGeographyTypeId = CampaignMapGeographyType.Mountainous,
            Name = "Mountainous",
            MovementCost = CampaignGeography.IMPASSABLE_MOVEMENT_COST,
            MapEditorIcon = null,
            IsPaintableOnCampaignMap = true
        };

        var grassland = new CampaignBiome { BiomeTypeId = BiomeType.Grassland, Name = "Grassland", MovementCost = 1m, IsWater = false, IsTraversable = true, MapEditorHtmlColor = "#7cb342" };
        var ocean = new CampaignBiome { BiomeTypeId = BiomeType.Ocean, Name = "Ocean", MovementCost = 1m, IsWater = true, IsTraversable = true, MapEditorHtmlColor = "#1565c0" };

        context.CampaignGeographies.AddRange(flat, hilly, deepWater, mountainous);
        context.CampaignBiomes.AddRange(grassland, ocean);

        for (int y = 0; y < _MAP_HEIGHT; y++)
        {
            for (int x = 0; x < _MAP_WIDTH; x++)
            {
                // Row 1 is open sea, row 3 is mountain, and everything else is walkable grassland.
                (CampaignGeography geography, CampaignBiome biome) = y switch
                {
                    1 => (deepWater, ocean),
                    3 => (mountainous, grassland),
                    _ => (flat, grassland)
                };

                // Column 4 of the open row is hilly, so there is somewhere nearby that costs more than one point to enter.
                if (y == _START_Y && x == 4)
                {
                    geography = hilly;
                }

                context.CampaignMaps.Add(new CampaignMap
                {
                    CampaignMapId = TileId(x, y),
                    GameId = _GAME_ID,
                    XCoord = x,
                    YCoord = y,
                    BiomeTypeId = biome.BiomeTypeId,
                    CampaignMapGeographyTypeId = geography.CampaignMapGeographyTypeId,
                    Biome = biome,
                    Geography = geography,
                    Game = null!,
                    FirstVisitor = null,
                    LastVisitor = null,
                    ControlledBy = null
                });
            }
        }

        context.UnitTemplates.Add(new UnitTemplate
        {
            Id = _TEMPLATE_ID,
            DisplayName = "Warrior",
            MapImagePath = "warrior.png",
            BodyComponentId = 1
        });

        await context.SaveChangesAsync();
    }

    /// <summary>Compiles the template with a movement domain and allowance, defaulting to a land Force with a full turn's movement.</summary>
    private async Task CompileTemplateAsync(DomainType movementDomain = DomainType.Land, long campaignMovementPoints = _CAMPAIGN_MOVEMENT_POINTS)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        context.CompiledUnitTemplates.Add(new CompiledUnitTemplate
        {
            UnitTemplateId = _TEMPLATE_ID,
            DisplayName = "Warrior",
            MapImagePath = "warrior.png",
            BodyComponentId = 1,
            MovementDomain = movementDomain,
            CampaignMovementPoints = campaignMovementPoints
        });

        await context.SaveChangesAsync();
    }

    /// <summary>Fields a Force, defaulting to the test Force: the player's own healthy warrior standing in the middle of the open row.</summary>
    private async Task AddForceAsync(
        int playerId = _PLAYER_ID,
        long campaignMovementRemaining = _CAMPAIGN_MOVEMENT_POINTS,
        int healthyCount = 100,
        int? battleMapTileId = null,
        int x = _START_X,
        int y = _START_Y,
        int unitInstanceId = _FORCE_ID)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        context.UnitInstances.Add(new UnitInstance
        {
            Id = unitInstanceId,
            GameId = _GAME_ID,
            PlayerId = playerId,
            CampaignMapTileId = TileId(x, y),
            BattleMapTileId = battleMapTileId,
            UnitTemplateId = _TEMPLATE_ID,
            Name = "Warrior",
            InitialStrength = 100,
            HealthyCount = healthyCount,
            InjuredCount = 0,
            CampaignMovementRemaining = campaignMovementRemaining
        });

        await context.SaveChangesAsync();
    }

    /// <summary>Sets up a land Force in the default position, which is what most of these tests start from.</summary>
    private async Task GivenALandForceAsync(long campaignMovementRemaining = _CAMPAIGN_MOVEMENT_POINTS)
    {
        await CompileTemplateAsync();
        await AddForceAsync(campaignMovementRemaining: campaignMovementRemaining);
    }

    private async Task<ServiceResult<StepForceResult>> StepAsync(MoveDirection direction, int playerId = _PLAYER_ID)
    {
        return await _handler.Handle(new StepForceRequest(_GAME_ID, playerId, _FORCE_ID, direction), CancellationToken.None);
    }

    private async Task<UnitInstance> ReloadForceAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        return context.UnitInstances.Single(force => force.Id == _FORCE_ID);
    }

    private async Task<List<MovementHistory>> LoadHistoryAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        return [.. context.MovementHistory.OrderBy(row => row.Id)];
    }

    [Test, Description("The happy path: the force lands on the neighbouring tile and pays the whole cost of entering it.")]
    public async Task ShouldMoveTheForceAndSpendTheFullTileCostAsync()
    {
        await GivenALandForceAsync();

        ServiceResult<StepForceResult> result = await StepAsync(MoveDirection.East);

        UnitInstance moved = await ReloadForceAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.True, string.Join("; ", result.Errors));
            Assert.That(moved.CampaignMapTileId, Is.EqualTo(TileId(_START_X + 1, _START_Y)), "The force should be standing on the tile to its east.");
            Assert.That(moved.CampaignMovementRemaining, Is.EqualTo(1_000), "Flat grassland costs 1.0, so 2.0 becomes 1.0.");
            Assert.That(result.SuccessValue!.XCoord, Is.EqualTo(_START_X + 1));
            Assert.That(result.SuccessValue.YCoord, Is.EqualTo(_START_Y));
            Assert.That(result.SuccessValue.CampaignMovementRemaining, Is.EqualTo(1.0m), "The response reports points as decimals, not the raw scaled value.");
            Assert.That(result.SuccessValue.CampaignMovementPoints, Is.EqualTo(2.0m), "The full allowance travels too, so the client can show what is left of a turn.");
        }
    }

    [Test, Description("Diagonals are ordinary neighbours on this grid, so a corner step must move on both axes and cost the same as a straight one.")]
    public async Task ShouldMoveDiagonallyAsync()
    {
        await GivenALandForceAsync();

        ServiceResult<StepForceResult> result = await StepAsync(MoveDirection.NorthEast);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False, "North-east of the start is open sea, so the diagonal is refused on terrain rather than on geometry.");
            Assert.That(result.Errors.Single(), Does.Contain("water"));
        }

        // Take the diagonal that stays on land to prove the geometry itself works.
        ServiceResult<StepForceResult> southEast = await StepAsync(MoveDirection.SouthEast);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(southEast.HasSucceeded, Is.False, "South-east is mountain, which nothing can cross.");
            Assert.That(southEast.Errors.Single(), Is.EqualTo("Mountain terrain cannot be entered."));
        }
    }

    [Test, Description("Pins the entry gate in unit-movement.md: any movement at all gets a force in, the whole cost is what it pays, and the balance may go negative.")]
    public async Task ShouldLetAForceEnterOnAnythingItHoldsAndFinishInDebtAsync()
    {
        // 1.0 movement point against the hilly tile east of the start, which costs 2.0 to cross.
        await CompileTemplateAsync();
        await AddForceAsync(campaignMovementRemaining: 1_000, x: 3, y: _START_Y);

        ServiceResult<StepForceResult> result = await StepAsync(MoveDirection.East);

        UnitInstance moved = await ReloadForceAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.True, string.Join("; ", result.Errors));
            Assert.That(moved.CampaignMapTileId, Is.EqualTo(TileId(4, _START_Y)), "The hill costs more than the force is holding, which is no longer a reason to refuse it.");
            Assert.That(moved.CampaignMovementRemaining, Is.EqualTo(-1_000), "The whole 2.0 is spent, leaving the force a point in debt to work off.");
        }
    }

    [Test, Description("A fractional remainder buys a tile worth several times as much, which is exactly the step the old proportional threshold refused outright.")]
    public async Task ShouldLetARemainderBuyATileWorthSeveralTimesItAsync()
    {
        // 0.4 of a point against a hilly tile costing 2.0. The old rule wanted 1.0 up front and would have left the force standing.
        await CompileTemplateAsync();
        await AddForceAsync(campaignMovementRemaining: 400, x: 3, y: _START_Y);

        ServiceResult<StepForceResult> result = await StepAsync(MoveDirection.East);

        UnitInstance moved = await ReloadForceAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.True, string.Join("; ", result.Errors));
            Assert.That(moved.CampaignMapTileId, Is.EqualTo(TileId(4, _START_Y)));
            Assert.That(moved.CampaignMovementRemaining, Is.EqualTo(-1_600), "0.4 against a 2.0 tile is 1.6 of debt, which the turns that follow pay down.");
        }
    }

    [Test, Description("The stranding guardrail refuses a step that would hold the force for more turns than the limit, and a refused step must not move it or take its points.")]
    public async Task ShouldRefuseAStepThatWouldStrandTheForceAsync()
    {
        // A 0.2 allowance holding 0.1 against the hilly tile east of the start: 1.9 of debt at 0.2 a turn holds it for ten turns, past the limit of six.
        await CompileTemplateAsync(campaignMovementPoints: 200);
        await AddForceAsync(campaignMovementRemaining: 100, x: 3, y: _START_Y);

        ServiceResult<StepForceResult> result = await StepAsync(MoveDirection.East);

        UnitInstance unmoved = await ReloadForceAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(result.Errors.Single(), Does.Contain("10 turns"), "The refusal quotes the count, because that is the thing the player is being asked to weigh.");
            Assert.That(unmoved.CampaignMapTileId, Is.EqualTo(TileId(3, _START_Y)));
            Assert.That(unmoved.CampaignMovementRemaining, Is.EqualTo(100));
        }
    }

    [Test, Description("A force with nothing left is told so plainly, rather than being quoted arithmetic about a tile it was never going to reach.")]
    public async Task ShouldRefuseAStepByASpentForceAsync()
    {
        await GivenALandForceAsync(campaignMovementRemaining: 0);

        ServiceResult<StepForceResult> result = await StepAsync(MoveDirection.East);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(result.Errors.Single(), Does.Contain("no movement points left"));
        }
    }

    [Test, Description("Mountain is impassable to every domain, and that is settled before the domain strategy is consulted.")]
    public async Task ShouldRefuseAnImpassableTileAsync()
    {
        await GivenALandForceAsync();

        ServiceResult<StepForceResult> result = await StepAsync(MoveDirection.South);

        UnitInstance unmoved = await ReloadForceAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(result.Errors.Single(), Is.EqualTo("Mountain terrain cannot be entered."));
            Assert.That(unmoved.CampaignMovementRemaining, Is.EqualTo(_CAMPAIGN_MOVEMENT_POINTS), "A refused step costs nothing.");
        }
    }

    [Test, Description("The land strategy is selected from the compiled template's domain, so a land force is stopped at the water's edge.")]
    public async Task ShouldRefuseALandForceEnteringWaterAsync()
    {
        await GivenALandForceAsync();

        ServiceResult<StepForceResult> result = await StepAsync(MoveDirection.North);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(result.Errors.Single(), Does.Contain("water"));
        }
    }

    [Test, Description("The mirror case proves the domain really is read off the template rather than land being hard-coded.")]
    public async Task ShouldLetAWaterForceEnterWaterAsync()
    {
        await CompileTemplateAsync(DomainType.Water);
        await AddForceAsync();

        ServiceResult<StepForceResult> result = await StepAsync(MoveDirection.North);

        UnitInstance moved = await ReloadForceAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.True, string.Join("; ", result.Errors));
            Assert.That(moved.CampaignMapTileId, Is.EqualTo(TileId(_START_X, _START_Y - 1)));
        }
    }

    [Test, Description("This map does not wrap, so a step off the edge has no destination and must be refused rather than looking for a missing tile.")]
    public async Task ShouldRefuseAStepOffTheEdgeOfANonWrappingMapAsync()
    {
        await CompileTemplateAsync();
        await AddForceAsync(x: 0, y: _START_Y);

        ServiceResult<StepForceResult> result = await StepAsync(MoveDirection.West);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(result.Errors.Single(), Does.Contain("edge of the map"));
        }
    }

    [Test, Description("Ordering another player's force is forbidden rather than merely invalid, and must not move it.")]
    public async Task ShouldRefuseToStepAForceTheCallerDoesNotOwnAsync()
    {
        await CompileTemplateAsync();
        await AddForceAsync(playerId: _OTHER_PLAYER_ID);

        ServiceResult<StepForceResult> result = await StepAsync(MoveDirection.East);

        UnitInstance unmoved = await ReloadForceAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.Forbidden));
            Assert.That(unmoved.CampaignMapTileId, Is.EqualTo(TileId(_START_X, _START_Y)));
        }
    }

    [Test, Description("A force in battle is committed to the fight over its tile, so a campaign step has to be refused.")]
    public async Task ShouldRefuseToStepAForceThatIsInBattleAsync()
    {
        await CompileTemplateAsync();
        await AddForceAsync(battleMapTileId: 42);

        ServiceResult<StepForceResult> result = await StepAsync(MoveDirection.East);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(result.Errors.Single(), Does.Contain("in battle"));
        }
    }

    [Test, Description("A wiped-out force should not be walking around, whatever its movement points say.")]
    public async Task ShouldRefuseToStepAForceWithNoTroopsAsync()
    {
        await CompileTemplateAsync();
        await AddForceAsync(healthyCount: 0);

        ServiceResult<StepForceResult> result = await StepAsync(MoveDirection.East);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(result.Errors.Single(), Does.Contain("no troops"));
        }
    }

    [Test, Description("A force that is not in this game cannot be stepped, and the caller is told it was not found rather than refused.")]
    public async Task ShouldAnswerNotFoundForAForceThatDoesNotExistAsync()
    {
        await CompileTemplateAsync();

        ServiceResult<StepForceResult> result = await StepAsync(MoveDirection.East);

        Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.NotFound));
    }

    [Test, Description("Moving during another player's turn is refused, which is the check MoveForceHandler left as a TODO.")]
    public async Task ShouldRefuseAStepTakenDuringAnotherPlayersTurnAsync()
    {
        await GivenALandForceAsync();

        _campaignTurnService.GetCurrentCampaignTurnAsync(_GAME_ID, Arg.Any<CancellationToken>())
            .Returns(ServiceResult<CurrentCampaignTurn>.Succeed(CurrentTurn(_OTHER_PLAYER_ID)));

        ServiceResult<StepForceResult> result = await StepAsync(MoveDirection.East);

        UnitInstance unmoved = await ReloadForceAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.Forbidden));
            Assert.That(result.Errors.Single(), Does.Contain("Faction 2"));
            Assert.That(unmoved.CampaignMapTileId, Is.EqualTo(TileId(_START_X, _START_Y)));
        }
    }

    [Test, Description("A started game always has a current turn, so having none is a broken invariant - and with no Turn to stamp, the step could not be recorded.")]
    public async Task ShouldRefuseAStepWhenTheGameHasNoCurrentTurnAsync()
    {
        await GivenALandForceAsync();

        _campaignTurnService.GetCurrentCampaignTurnAsync(_GAME_ID, Arg.Any<CancellationToken>())
            .Returns(ServiceResult<CurrentCampaignTurn>.NotFound("No active Campaign Turn Queue."));

        ServiceResult<StepForceResult> result = await StepAsync(MoveDirection.East);

        UnitInstance unmoved = await ReloadForceAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(result.Errors.Single(), Does.Contain("No turn is in progress"));
            Assert.That(unmoved.CampaignMapTileId, Is.EqualTo(TileId(_START_X, _START_Y)));
        }
    }

    [Test, Description("A current turn with no open PlayerTurn row has nothing to record the step against, so the step is refused rather than made unrecorded.")]
    public async Task ShouldRefuseAStepWhenTheCurrentTurnHasNotBeenOpenedAsync()
    {
        await GivenALandForceAsync();

        _campaignTurnService.GetCurrentCampaignTurnAsync(_GAME_ID, Arg.Any<CancellationToken>())
            .Returns(ServiceResult<CurrentCampaignTurn>.Succeed(CurrentTurn(_PLAYER_ID) with { PlayerTurnId = null }));

        ServiceResult<StepForceResult> result = await StepAsync(MoveDirection.East);

        UnitInstance unmoved = await ReloadForceAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(unmoved.CampaignMapTileId, Is.EqualTo(TileId(_START_X, _START_Y)));
            Assert.That(await LoadHistoryAsync(), Is.Empty);
        }
    }

    [Test, Description("A step is recorded as one Moved row naming both tiles and the Turn's sequence, which is what the intelligence fold reads a path from.")]
    public async Task ShouldRecordTheStepAsAMovedRowStampedWithTheTurnAsync()
    {
        await GivenALandForceAsync();

        ServiceResult<StepForceResult> result = await StepAsync(MoveDirection.East);

        List<MovementHistory> history = await LoadHistoryAsync();

        Assert.That(result.HasSucceeded, Is.True, string.Join("; ", result.Errors));
        Assert.That(history, Has.Count.EqualTo(1), "One step, one row.");

        MovementHistory row = history.Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(row.EventType, Is.EqualTo(ForceHistoryEventType.Moved));
            Assert.That(row.UnitInstanceId, Is.EqualTo(_FORCE_ID));
            Assert.That(row.FromCampaignMapTileId, Is.EqualTo(TileId(_START_X, _START_Y)));
            Assert.That(row.CampaignMapTileId, Is.EqualTo(TileId(_START_X + 1, _START_Y)));
            Assert.That(row.PlayerTurnId, Is.EqualTo(_PLAYER_TURN_ID));
            Assert.That(row.TurnSequence, Is.EqualTo(_TURN_SEQUENCE));
            Assert.That(row.PlayerId, Is.EqualTo(_PLAYER_ID));
            Assert.That(row.UnitTemplateId, Is.EqualTo(_TEMPLATE_ID));
            Assert.That(row.GameId, Is.EqualTo(_GAME_ID));
        }
    }

    [Test, Description("A step that cannot be recorded is not made at all, so a Force never moves without the intelligence feeds knowing about it.")]
    public async Task ShouldNotMoveTheForceWhenTheStepCannotBeRecordedAsync()
    {
        await GivenALandForceAsync();

        _playerTurnService.AllocateSequenceAsync(Arg.Any<AscendantEmpireContext>(), _PLAYER_TURN_ID, 1, Arg.Any<CancellationToken>())
            .Returns(ServiceResult<TurnSequenceBlock>.Fail("Player Turn Id 7 does not exist or has already ended."));

        ServiceResult<StepForceResult> result = await StepAsync(MoveDirection.East);

        UnitInstance unmoved = await ReloadForceAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(result.Errors, Has.Some.Contains("could not be recorded"));
            Assert.That(unmoved.CampaignMapTileId, Is.EqualTo(TileId(_START_X, _START_Y)));
            Assert.That(unmoved.CampaignMovementRemaining, Is.EqualTo(_CAMPAIGN_MOVEMENT_POINTS), "An unrecorded step costs nothing either.");
            Assert.That(await LoadHistoryAsync(), Is.Empty);
        }
    }

    [Test, Description("A refused step changed nothing, so it records nothing and does not spend a sequence number on the Turn.")]
    public async Task ShouldNotRecordARefusedStepAsync()
    {
        await GivenALandForceAsync();

        // South of the start is mountain, which nothing can enter.
        await StepAsync(MoveDirection.South);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await LoadHistoryAsync(), Is.Empty);
            await _playerTurnService.DidNotReceive().AllocateSequenceAsync(Arg.Any<AscendantEmpireContext>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
        }
    }

    [Test, Description("The move is committed before vision is refreshed, so a failure to refresh must not report the step itself as failed.")]
    public async Task ShouldStillReportSuccessWhenRefreshingTheMapViewFailsAsync()
    {
        await GivenALandForceAsync();

        _mediator.Send(Arg.Any<GetPlayerCampaignMapRequest>(), Arg.Any<CancellationToken>())
            .Returns(ServiceResult<IReadOnlyCollection<ObserverSight>>.Fail("The map read blew up."));

        ServiceResult<StepForceResult> result = await StepAsync(MoveDirection.East);

        UnitInstance moved = await ReloadForceAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.True, string.Join("; ", result.Errors));
            Assert.That(result.SuccessValue!.Observers, Is.Empty, "The player simply gets no new terrain until the next map read.");
            Assert.That(moved.CampaignMapTileId, Is.EqualTo(TileId(_START_X + 1, _START_Y)), "The step itself still stands.");
        }
    }

    [Test, Description("The refreshed view is fetched only after a step is applied, so a refused step must not pay for a map read at all.")]
    public async Task ShouldNotRefreshTheMapViewWhenTheStepIsRefusedAsync()
    {
        await GivenALandForceAsync();

        await StepAsync(MoveDirection.South);

        await _mediator.DidNotReceive().Send(Arg.Any<GetPlayerCampaignMapRequest>(), Arg.Any<CancellationToken>());
    }

    [Test, Description("A step onto another player's Force is an attack, so the engagement service is handed the attacker with its move and payment staged, and every enemy on the tile.")]
    public async Task ShouldStartABattleWhenTheTileHoldsAnEnemyForceAsync()
    {
        await GivenALandForceAsync();
        await AddForceAsync(playerId: _OTHER_PLAYER_ID, x: _START_X + 1, unitInstanceId: _ENEMY_FORCE_ID);

        ServiceResult<StepForceResult> result = await StepAsync(MoveDirection.East);

        Assert.That(result.HasSucceeded, Is.True, string.Join("; ", result.Errors));

        // The one call's engagement, read back off the substitute so each part of it can be checked and reported on its own.
        BattleEngagement engagement = (BattleEngagement)_battleEngagementService.ReceivedCalls().Single().GetArguments()[1]!;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.SuccessValue!.StartedBattle, Is.True, "The response tells the client a battle has begun.");

            Assert.That(engagement.GameId, Is.EqualTo(_GAME_ID));
            Assert.That(engagement.CampaignMapTileId, Is.EqualTo(TileId(_START_X + 1, _START_Y)));
            Assert.That(engagement.StepDirection, Is.EqualTo(MoveDirection.East), "The direction decides which edge the attacker starts on.");
            Assert.That(engagement.RoundNumber, Is.EqualTo(1), "The current turn's Round.");
            Assert.That(engagement.PlayerTurnId, Is.EqualTo(_PLAYER_TURN_ID));

            Assert.That(engagement.Attacker.Id, Is.EqualTo(_FORCE_ID));
            Assert.That(engagement.Attacker.CampaignMapTileId, Is.EqualTo(TileId(_START_X + 1, _START_Y)), "The move is staged before the Battle is commenced, so the two save together.");
            Assert.That(engagement.Attacker.CampaignMovementRemaining, Is.EqualTo(1_000), "And so is the payment for the step.");

            Assert.That(engagement.Defenders.Select(defender => defender.Id), Is.EqualTo(new[] { _ENEMY_FORCE_ID }));
        }
    }

    [Test, Description("When the Battle cannot be started the step is refused outright, so the Force never stands on an enemy's tile without the fight having begun.")]
    public async Task ShouldRefuseTheStepWhenTheBattleCannotBeStartedAsync()
    {
        await GivenALandForceAsync();
        await AddForceAsync(playerId: _OTHER_PLAYER_ID, x: _START_X + 1, unitInstanceId: _ENEMY_FORCE_ID);

        _battleEngagementService.CommenceAsync(Arg.Any<AscendantEmpireContext>(), Arg.Any<BattleEngagement>(), Arg.Any<CancellationToken>())
            .Returns(ServiceResult<int>.Fail("The battlefield could not be prepared, so the battle cannot begin."));

        ServiceResult<StepForceResult> result = await StepAsync(MoveDirection.East);

        UnitInstance unmoved = await ReloadForceAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(result.Errors, Has.Some.Contains("could not attack"));
            Assert.That(result.Errors, Has.Some.Contains("battlefield could not be prepared"), "The underlying reason reaches the player too.");
            Assert.That(unmoved.CampaignMapTileId, Is.EqualTo(TileId(_START_X, _START_Y)));
            Assert.That(unmoved.CampaignMovementRemaining, Is.EqualTo(_CAMPAIGN_MOVEMENT_POINTS), "A refused attack costs nothing.");
            Assert.That(await LoadHistoryAsync(), Is.Empty);
            await _mediator.DidNotReceive().Send(Arg.Any<GetPlayerCampaignMapRequest>(), Arg.Any<CancellationToken>());
        }
    }

    [Test, Description("Only another player's Force is an enemy, so stepping onto a tile shared with the player's own Force is an ordinary move.")]
    public async Task ShouldMoveWithoutABattleOntoATileHoldingOnlyTheirOwnForceAsync()
    {
        await GivenALandForceAsync();
        await AddForceAsync(x: _START_X + 1, unitInstanceId: _ENEMY_FORCE_ID);

        ServiceResult<StepForceResult> result = await StepAsync(MoveDirection.East);

        UnitInstance moved = await ReloadForceAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.True, string.Join("; ", result.Errors));
            Assert.That(result.SuccessValue!.StartedBattle, Is.False);
            Assert.That(moved.CampaignMapTileId, Is.EqualTo(TileId(_START_X + 1, _START_Y)), "Saved by the handler itself, as every ordinary step is.");
            await _battleEngagementService.DidNotReceive().CommenceAsync(Arg.Any<AscendantEmpireContext>(), Arg.Any<BattleEngagement>(), Arg.Any<CancellationToken>());
        }
    }

    [Test, Description("A wiped-out enemy Force has nobody left to fight, so it does not turn the step into a battle.")]
    public async Task ShouldMoveWithoutABattleOntoATileHoldingOnlyAWipedOutEnemyAsync()
    {
        await GivenALandForceAsync();
        await AddForceAsync(playerId: _OTHER_PLAYER_ID, healthyCount: 0, x: _START_X + 1, unitInstanceId: _ENEMY_FORCE_ID);

        ServiceResult<StepForceResult> result = await StepAsync(MoveDirection.East);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.True, string.Join("; ", result.Errors));
            Assert.That(result.SuccessValue!.StartedBattle, Is.False);
            await _battleEngagementService.DidNotReceive().CommenceAsync(Arg.Any<AscendantEmpireContext>(), Arg.Any<BattleEngagement>(), Arg.Any<CancellationToken>());
        }
    }

    [Test, Description("Joining a battle already under way is not built yet (COMM-4), so entering its tile is refused rather than walking into the fight untouched by it.")]
    public async Task ShouldRefuseAStepOntoATileWithABattleUnderWayAsync()
    {
        await GivenALandForceAsync();
        await AddBattleAsync(x: _START_X + 1, y: _START_Y);

        ServiceResult<StepForceResult> result = await StepAsync(MoveDirection.East);

        UnitInstance unmoved = await ReloadForceAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(result.Errors.Single(), Does.Contain("already under way"));
            Assert.That(unmoved.CampaignMapTileId, Is.EqualTo(TileId(_START_X, _START_Y)));
            await _battleEngagementService.DidNotReceive().CommenceAsync(Arg.Any<AscendantEmpireContext>(), Arg.Any<BattleEngagement>(), Arg.Any<CancellationToken>());
            await _playerTurnService.DidNotReceive().AllocateSequenceAsync(Arg.Any<AscendantEmpireContext>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
        }
    }

    [Test, Description("A battle that has ended no longer holds its tile, so it must not go on refusing entry to it.")]
    public async Task ShouldLetAForceEnterATileWhoseBattleHasEndedAsync()
    {
        await GivenALandForceAsync();
        await AddBattleAsync(x: _START_X + 1, y: _START_Y, concludedAtRoundNumber: 1);

        ServiceResult<StepForceResult> result = await StepAsync(MoveDirection.East);

        UnitInstance moved = await ReloadForceAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.True, string.Join("; ", result.Errors));
            Assert.That(moved.CampaignMapTileId, Is.EqualTo(TileId(_START_X + 1, _START_Y)));
        }
    }

    /// <summary>Records a Battle on a tile, open unless a Round it concluded in is given.</summary>
    private async Task AddBattleAsync(int x, int y, int? concludedAtRoundNumber = null)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        context.Battles.Add(new Battle
        {
            Id = _BATTLE_ID,
            GameId = _GAME_ID,
            CampaignMapTileId = TileId(x, y),
            CommencedAtRoundNumber = 1,
            ConcludedAtRoundNumber = concludedAtRoundNumber,
            OriginalTileStatus = TileOwnershipStatus.Unclaimed,
            RowVersion = []
        });

        await context.SaveChangesAsync();
    }

    [Test, Description("A direction outside the eight is rejected by validation rather than reaching the neighbour arithmetic and throwing.")]
    public async Task ShouldRejectADirectionThatIsNotOneOfTheEightAsync()
    {
        await GivenALandForceAsync();

        ServiceResult<StepForceResult> result = await _handler.Handle(
            new StepForceRequest(_GAME_ID, _PLAYER_ID, _FORCE_ID, (MoveDirection)99),
            CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.GenericError));
            Assert.That(result.Errors, Has.Some.Contains("compass directions"));
        }
    }
}
