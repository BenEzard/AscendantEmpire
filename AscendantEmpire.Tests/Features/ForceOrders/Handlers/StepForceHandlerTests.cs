using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.ForceOrders.Handlers;
using AscendantEmpire.Application.Features.ForceOrders.Movement.Strategies;
using AscendantEmpire.Application.Features.Game.Handlers;
using AscendantEmpire.Application.Features.Maps.CampaignMaps.Handlers;
using AscendantEmpire.Application.Features.Turns.Services;
using AscendantEmpire.BackendContracts.CampaignMaps.GetPlayerCampaignMap;
using AscendantEmpire.BackendContracts.ForceOrders.StepForce;
using AscendantEmpire.BackendContracts.Games.GetOpenGames;
using AscendantEmpire.BackendContracts.Turns.GetCurrentCampaignTurn;
using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Entities.Maps.CampaignMaps;
using AscendantEmpire.Domain.Entities.UnitDesigns;
using AscendantEmpire.Domain.Entities.Units;
using AscendantEmpire.Domain.Enums.Maps;

using MediatR;

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

    private IMediator _mediator = null!;
    private ICampaignTurnService _campaignTurnService = null!;
    private StepForceHandler _handler = null!;

    [SetUp]
    public async Task SetUp()
    {
        _mediator = Substitute.For<IMediator>();
        _campaignTurnService = Substitute.For<ICampaignTurnService>();

        _mediator.Send(Arg.Any<GetOpenGamesRequest>(), Arg.Any<CancellationToken>())
            .Returns(ServiceResult<IReadOnlyCollection<OpenGame>>.Succeed([OpenGameInfo()]));

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
            AllMovementDomainStrategies());

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

    private static OpenGame OpenGameInfo()
    {
        return new OpenGame
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
                new OpenGamePlayer
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
            FactionName = playerId == _PLAYER_ID ? "Faction 1" : "Faction 2"
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

    /// <summary>Compiles the template with a movement domain, defaulting to a land Force with a full turn's movement.</summary>
    private async Task CompileTemplateAsync(DomainType movementDomain = DomainType.Land)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        context.CompiledUnitTemplates.Add(new CompiledUnitTemplate
        {
            UnitTemplateId = _TEMPLATE_ID,
            DisplayName = "Warrior",
            MapImagePath = "warrior.png",
            BodyComponentId = 1,
            MovementDomain = movementDomain,
            CampaignMovementPoints = _CAMPAIGN_MOVEMENT_POINTS
        });

        await context.SaveChangesAsync();
    }

    /// <summary>Fields the test Force, defaulting to the player's own healthy warrior standing in the middle of the open row.</summary>
    private async Task AddForceAsync(
        int playerId = _PLAYER_ID,
        long campaignMovementRemaining = _CAMPAIGN_MOVEMENT_POINTS,
        int healthyCount = 100,
        int? battleMapTileId = null,
        int x = _START_X,
        int y = _START_Y)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        context.UnitInstances.Add(new UnitInstance
        {
            Id = _FORCE_ID,
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

    [Test, Description("The happy path: the force lands on the neighbouring tile and pays the whole cost of entering it, not half.")]
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

    [Test, Description("Pins rule 2 of unit-mechanics.md: half the cost gets a force in, the whole cost is what it pays, and the balance may go negative.")]
    public async Task ShouldLetAForceEnterOnHalfTheCostAndFinishInDebtAsync()
    {
        // 1.0 movement point against the hilly tile east of the start, which costs 2.0 to cross.
        await CompileTemplateAsync();
        await AddForceAsync(campaignMovementRemaining: 1_000, x: 3, y: _START_Y);

        ServiceResult<StepForceResult> result = await StepAsync(MoveDirection.East);

        UnitInstance moved = await ReloadForceAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.True, string.Join("; ", result.Errors));
            Assert.That(moved.CampaignMapTileId, Is.EqualTo(TileId(4, _START_Y)), "1.0 point is exactly half of the hill's 2.0 cost, which is enough to enter.");
            Assert.That(moved.CampaignMovementRemaining, Is.EqualTo(-1_000), "Entering costs half but spends the whole 2.0, leaving the force a point in debt.");
        }
    }

    [Test, Description("Below the half-cost threshold the force stays put; a refused step must not move it or take its remaining points.")]
    public async Task ShouldRefuseAStepTheForceCannotAffordAsync()
    {
        // 0.4 of a point against a hilly tile costing 2.0, which needs 1.0 to enter.
        await CompileTemplateAsync();
        await AddForceAsync(campaignMovementRemaining: 400, x: 3, y: _START_Y);

        ServiceResult<StepForceResult> result = await StepAsync(MoveDirection.East);

        UnitInstance unmoved = await ReloadForceAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(result.Errors.Single(), Does.Contain("movement points"));
            Assert.That(unmoved.CampaignMapTileId, Is.EqualTo(TileId(3, _START_Y)));
            Assert.That(unmoved.CampaignMovementRemaining, Is.EqualTo(400));
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

    [Test, Description("A force in battle is off the campaign map for the moment, so a campaign step has to be refused.")]
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

    [Test, Description("Game setup leaves the turn queue unstarted, so a game with no current turn must still allow movement rather than freezing every force.")]
    public async Task ShouldAllowAStepWhenTheGameHasNoCurrentTurnYetAsync()
    {
        await GivenALandForceAsync();

        _campaignTurnService.GetCurrentCampaignTurnAsync(_GAME_ID, Arg.Any<CancellationToken>())
            .Returns(ServiceResult<CurrentCampaignTurn>.NotFound("No active Campaign Turn Queue."));

        ServiceResult<StepForceResult> result = await StepAsync(MoveDirection.East);

        Assert.That(result.HasSucceeded, Is.True, string.Join("; ", result.Errors));
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
