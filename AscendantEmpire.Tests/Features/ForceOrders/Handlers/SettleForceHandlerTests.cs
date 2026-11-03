using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Assets.Services;
using AscendantEmpire.Application.Features.ForceOrders.ContextActions;
using AscendantEmpire.Application.Features.ForceOrders.Handlers;
using AscendantEmpire.Application.Features.Game.Handlers;
using AscendantEmpire.Application.Features.Structures.Services;
using AscendantEmpire.Application.Features.Turns.Services;
using AscendantEmpire.Application.Features.Units.Services;
using AscendantEmpire.BackendContracts.ForceOrders.SettleForce;
using AscendantEmpire.BackendContracts.Turns.GetCurrentCampaignTurn;
using AscendantEmpire.Domain.Entities.Assets;
using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Entities.Settlements;
using AscendantEmpire.Domain.Entities.Structures;
using AscendantEmpire.Domain.Entities.Units;
using AscendantEmpire.Domain.Enums.Forces;
using AscendantEmpire.Domain.Enums.Maps;
using AscendantEmpire.Domain.Enums.Settlements;
using AscendantEmpire.Domain.Enums.Structures;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Tests.Features.ForceOrders.Handlers;

/// <summary>
/// Tests for <see cref="SettleForceHandler"/>: what founding a settlement leaves behind, and what it refuses.
/// </summary>
/// <remarks>
/// <para>
/// On the containerised tier, for the reasons <c>testing.md</c> names: the founding runs inside an explicit transaction, the lifecycle writers allocate their sequences with
/// raw SQL, and the name's uniqueness index is only a rule when a database is enforcing it. The in-memory provider has none of the three.
/// </para>
/// <para>
/// The <b>rules</b> are substituted, not the writers. <see cref="IForceContextActionService"/> stands in for the strategies, which have their own fixtures and are re-run
/// here only to prove the handler asks and obeys; the force and structure lifecycle writers are real, because what they record is half of what founding means.
/// </para>
/// <para>
/// Games are built by the real <c>GameSetupService</c>, so the tiles, players and Turn Queue are the ones play would give them.
/// </para>
/// </remarks>
[TestFixture]
internal class SettleForceHandlerTests : GameSqlServerTestBase
{
    private const int _MAP_WIDTH = 4;
    private const int _MAP_HEIGHT = 4;

    /// <summary>Four of the settler organisation's 25-strong base groups - the smallest Force that may found a village.</summary>
    private const int _SETTLER_SIZE = 100;

    /// <summary>One base group, which is the smallest Force that may establish a camp and the number a camp takes when it splits off a larger one.</summary>
    private const int _CAMP_MINIMUM_TROOPS = 25;

    /// <summary>Enough movement for the "has any movement left" rule, which is all founding needs.</summary>
    private const long _STARTING_MOVEMENT = 2_000;

    /// <summary>
    /// The settler's Power, raw Scale-1,000: 1.0.
    /// </summary>
    /// <remarks>
    /// <b>The test base's stand-in body, not the real catalogue's.</b> <c>GameSqlServerTestBase.SeedStarterCatalogueAsync</c> authors its human body at Power 1.0, where
    /// <c>UnitComponentSeedData</c> authors the real one at 7.0. Which is fine here: this fixture is about the handler reading a Force's Power and storing a rate from it,
    /// not about what that rate comes to. The real calibration - 200 healthy humans making 1,400 points a Turn - is asserted in <c>StructureSeedDataTests</c>, against the
    /// real seed values and with no database at all.
    /// </remarks>
    private const long _SETTLER_POWER_RAW = 1_000;

    private const string _VILLAGE_CODE = "village";
    private const string _CAMP_CODE = "camp";

    /// <summary>
    /// The village's work, raw Scale-1,000: twice what the fixture's settler does in a Turn, so it founds one in two Turns.
    /// </summary>
    /// <remarks>Set from the settler below rather than copied from the seed, so this fixture keeps testing two Turns even if the real calibration is retuned.</remarks>
    private const long _VILLAGE_BUILD_POINTS = _SETTLER_SIZE * _SETTLER_POWER_RAW * 2;

    /// <summary>A camp's work, half the village's, so the two tiers are plainly different builds.</summary>
    private const long _CAMP_BUILD_POINTS = _SETTLER_SIZE * _SETTLER_POWER_RAW;

    private int _gameId;
    private int _playerId;
    private int _otherPlayerId;
    private int _tileId;
    private int _villageStructureId;
    private int _campStructureId;
    private int _settlerTemplateId;

    private IForceContextActionService _contextActionService = null!;
    private ICampaignTurnService _campaignTurnService = null!;

    [SetUp]
    public async Task SetUpGameAsync()
    {
        // SeedGameAsync already seeds the starter catalogue - the organisation tags, the components and the templates with their compiled snapshots - because game setup
        // spawns each player's starting units. Seeding it a second time duplicates the settler tag and breaks its unique index.
        await SeedGameAsync(mapWidth: _MAP_WIDTH, mapHeight: _MAP_HEIGHT, numberOfPlayers: 2);

        ServiceResult<int> setupResult = await _service.SetupGameAsync(
            new SetupGameRequest(GalleryMapId: _galleryIndexId, GameName: "Settle Force Game", NumberOfPlayers: 2, BattleMapSize: 50, GenerationSeed: null),
            CancellationToken.None);

        Assert.That(setupResult.HasSucceeded, Is.True, () => string.Join("; ", setupResult.Errors));

        _gameId = setupResult.SuccessValue;

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        int[] playerIds = await context.Players.Where(p => p.GameId == _gameId).OrderBy(p => p.Id).Select(p => p.Id).ToArrayAsync();

        _playerId = playerIds[0];
        _otherPlayerId = playerIds[1];
        _tileId = await context.CampaignMaps.Where(t => t.GameId == _gameId).OrderBy(t => t.CampaignMapId).Select(t => t.CampaignMapId).FirstAsync();
        _settlerTemplateId = await context.UnitTemplates.Where(t => t.Code == "SETTLER").Select(t => t.Id).SingleAsync();

        // The Structure catalogue is seeded data rather than migrated, so the fixture stands the village in as the seed would.
        var village = new Structure
        {
            Code = _VILLAGE_CODE,
            Name = "Village",
            RecordTypeId = (int)Domain.Enums.RecordType.Structure,
            RequiresSlotType = TileSlotType.Primary,
            AllowedPositions = PositionOnTile.WholeTile,
            CanBeBuiltOnCampaignMap = true,
            CanBeBuiltOnBattleMap = false,
            DualMapBuildTrigger = false,
            BuildPointsRequired = _VILLAGE_BUILD_POINTS,
            SightRange = 2,
            SightDirection = DetectionDirection.FullRadius,
            Asserts = StructureAssertion.Owned,
            MapImagePath = "grass-hut_40px.png",
            FullImagePath = "grass-hut_40px.png"
        };

        // The unnamed tier, stood in the same way, so the tests can found one of each.
        var camp = new Structure
        {
            Code = _CAMP_CODE,
            Name = "Camp",
            RecordTypeId = (int)Domain.Enums.RecordType.Structure,
            RequiresSlotType = TileSlotType.Primary,
            AllowedPositions = PositionOnTile.WholeTile,
            CanBeBuiltOnCampaignMap = true,
            CanBeBuiltOnBattleMap = false,
            DualMapBuildTrigger = false,
            BuildPointsRequired = _CAMP_BUILD_POINTS,
            SightRange = 1,
            SightDirection = DetectionDirection.FullRadius,
            Asserts = StructureAssertion.Owned,
            MapImagePath = "fire_40px.png",
            FullImagePath = "fire_40px.png"
        };

        context.Structures.AddRange(village, camp);
        await context.SaveChangesAsync();

        _villageStructureId = village.Id;
        _campStructureId = camp.Id;

        // The rules say yes unless a test says otherwise. The strategies themselves are covered by CreateVillageStrategyTests and SettlementFoundingRulesTests.
        _contextActionService = Substitute.For<IForceContextActionService>();

        _contextActionService
            .EvaluateActionAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<ForceActionType>(), Arg.Any<CancellationToken>())
            .Returns(ServiceResult<ForceActionAvailability>.Succeed(ForceActionAvailability.Available()));

        _campaignTurnService = Substitute.For<ICampaignTurnService>();
    }

    [Test, Description("Founding a village writes the settlement with the player's name, the founding round, the whole force's strength as its population, and a structure at 0% on the force's tile.")]
    public async Task ShouldFoundTheSettlementAndItsStructureAsync()
    {
        int playerTurnId = await OpenFirstPlayerTurnAsync(_gameId);
        ItIsThisPlayersTurn(playerTurnId, roundNumber: 1);

        int settlerId = await SeedSettlerAsync();

        ServiceResult<SettleForceResult> result = await HandleAsync(settlerId, SettlementTier.Village, "Riverhold");

        Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        Settlement settlement = await context.Settlements.AsNoTracking().SingleAsync(s => s.GameId == _gameId);
        StructureInstance structure = await context.StructureInstances.AsNoTracking().SingleAsync(s => s.Id == settlement.StructureInstanceId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(settlement.Name, Is.EqualTo("Riverhold"));
            Assert.That(settlement.Tier, Is.EqualTo(SettlementTier.Village));
            Assert.That(settlement.Population, Is.EqualTo(_SETTLER_SIZE), "Every soldier in the force becomes a person in the settlement.");
            Assert.That(settlement.FoundingPlayerId, Is.EqualTo(_playerId));
            Assert.That(settlement.FoundedOnRoundNumber, Is.EqualTo(1));
            Assert.That(settlement.FoundedOnTurnId, Is.EqualTo(playerTurnId));

            Assert.That(structure.StructureId, Is.EqualTo(_villageStructureId));
            Assert.That(structure.CampaignMapTileId, Is.EqualTo(_tileId), "The settlement goes where the force was standing.");
            Assert.That(structure.OwnedByPlayerId, Is.EqualTo(_playerId));
            Assert.That(structure.SlotType, Is.EqualTo(TileSlotType.Primary));
            Assert.That(structure.BuildPointsProgress, Is.Zero, "Founding starts construction rather than finishing it.");
            Assert.That(structure.CompletedOnTurnId, Is.Null);

            Assert.That(structure.BuildPointsPerTurn, Is.EqualTo(_SETTLER_SIZE * _SETTLER_POWER_RAW), "The rate is fixed from the force that founded it.");
            Assert.That(result.SuccessValue!.TurnsToComplete, Is.EqualTo(2), "A full-strength settler on a full turn founds this village in two turns.");
            Assert.That(result.SuccessValue.Population, Is.EqualTo(_SETTLER_SIZE));
        }
    }

    [Test, Description("The settler is gone the moment the settlement is founded, with a Destroyed row recording it, so its people are in one place rather than two.")]
    public async Task ShouldConsumeTheSettlerAndRecordItsDestructionAsync()
    {
        int playerTurnId = await OpenFirstPlayerTurnAsync(_gameId);
        ItIsThisPlayersTurn(playerTurnId, roundNumber: 1);

        int settlerId = await SeedSettlerAsync();

        ServiceResult<SettleForceResult> result = await HandleAsync(settlerId, SettlementTier.Village, "Riverhold");

        Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await context.UnitInstances.AnyAsync(u => u.Id == settlerId), Is.False);
            Assert.That(
                await context.MovementHistory.AnyAsync(row => row.UnitInstanceId == settlerId && row.EventType == ForceHistoryEventType.Destroyed),
                Is.True,
                "No record, no change: the destruction is in the feed a watching player reads.");
        }
    }

    [Test, Description("A force at least twice the camp minimum drops off a camp and marches on, so a strong column is not spent founding a waystop.")]
    public async Task ShouldLeaveTheForceStandingWhenItCanSpareACampAsync()
    {
        int playerTurnId = await OpenFirstPlayerTurnAsync(_gameId);
        ItIsThisPlayersTurn(playerTurnId, roundNumber: 1);

        // 100 healthy, against a camp minimum of 25: four times what a camp needs.
        int settlerId = await SeedSettlerAsync();

        ServiceResult<SettleForceResult> result = await HandleAsync(settlerId, SettlementTier.Camp);

        Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        UnitInstance? survivor = await context.UnitInstances.FirstOrDefaultAsync(u => u.Id == settlerId);
        Settlement settlement = await context.Settlements.SingleAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(survivor, Is.Not.Null, "The force is still here.");
            Assert.That(survivor!.HealthyCount, Is.EqualTo(_SETTLER_SIZE - _CAMP_MINIMUM_TROOPS), "It is lighter by exactly the camp's minimum.");
            Assert.That(settlement.Population, Is.EqualTo(_CAMP_MINIMUM_TROOPS), "And the camp holds exactly those people, so nobody is counted twice.");
        }
    }

    [Test, Description("At exactly twice the minimum the force still survives, because what is left of it could found another camp - which is where the boundary is drawn.")]
    public async Task ShouldLeaveTheForceStandingAtExactlyTwiceTheMinimumAsync()
    {
        int playerTurnId = await OpenFirstPlayerTurnAsync(_gameId);
        ItIsThisPlayersTurn(playerTurnId, roundNumber: 1);

        int settlerId = await SeedSettlerAsync(healthyCount: _CAMP_MINIMUM_TROOPS * 2);

        ServiceResult<SettleForceResult> result = await HandleAsync(settlerId, SettlementTier.Camp);

        Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        UnitInstance? survivor = await context.UnitInstances.FirstOrDefaultAsync(u => u.Id == settlerId);

        Assert.That(survivor?.HealthyCount, Is.EqualTo(_CAMP_MINIMUM_TROOPS));
    }

    [Test, Description("One short of twice the minimum the whole force becomes the camp, so a party that cannot spare the settlers goes in with them.")]
    public async Task ShouldConsumeTheForceJustBelowTwiceTheMinimumAsync()
    {
        int playerTurnId = await OpenFirstPlayerTurnAsync(_gameId);
        ItIsThisPlayersTurn(playerTurnId, roundNumber: 1);

        const int strength = (_CAMP_MINIMUM_TROOPS * 2) - 1;
        int settlerId = await SeedSettlerAsync(healthyCount: strength);

        ServiceResult<SettleForceResult> result = await HandleAsync(settlerId, SettlementTier.Camp);

        Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        Settlement settlement = await context.Settlements.SingleAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await context.UnitInstances.AnyAsync(u => u.Id == settlerId), Is.False, "The force is gone.");
            Assert.That(settlement.Population, Is.EqualTo(strength), "And every one of its soldiers is a person in the camp.");
        }
    }

    [Test, Description("A village consumes its founder however large it is, which is the whole difference between the two foundable tiers.")]
    public async Task ShouldConsumeTheForceFoundingAVillageHoweverLargeAsync()
    {
        int playerTurnId = await OpenFirstPlayerTurnAsync(_gameId);
        ItIsThisPlayersTurn(playerTurnId, roundNumber: 1);

        // Twice the village minimum, which for a camp would have been a comfortable split.
        int settlerId = await SeedSettlerAsync(healthyCount: _SETTLER_SIZE * 2);

        ServiceResult<SettleForceResult> result = await HandleAsync(settlerId, SettlementTier.Village, "Riverhold");

        Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await context.UnitInstances.AnyAsync(u => u.Id == settlerId), Is.False);
            Assert.That((await context.Settlements.SingleAsync()).Population, Is.EqualTo(_SETTLER_SIZE * 2));
        }
    }

    [Test, Description("A camp's founders are drawn from the healthy first, so a column does not settle its wounded and march on with the able-bodied.")]
    public async Task ShouldTakeACampsFoundersFromTheHealthyFirstAsync()
    {
        int playerTurnId = await OpenFirstPlayerTurnAsync(_gameId);
        ItIsThisPlayersTurn(playerTurnId, roundNumber: 1);

        int settlerId = await SeedSettlerAsync(healthyCount: 60, injuredCount: 20);

        ServiceResult<SettleForceResult> result = await HandleAsync(settlerId, SettlementTier.Camp);

        Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        UnitInstance survivor = await context.UnitInstances.SingleAsync(u => u.Id == settlerId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(survivor.HealthyCount, Is.EqualTo(60 - _CAMP_MINIMUM_TROOPS), "All 25 came out of the healthy.");
            Assert.That(survivor.InjuredCount, Is.EqualTo(20), "And none out of the wounded.");
        }
    }

    [Test, Description("A force that survives founding a camp keeps no Destroyed row, since nothing was destroyed and a watching player must not be told otherwise.")]
    public async Task ShouldNotRecordADestructionWhenTheForceSurvivesAsync()
    {
        int playerTurnId = await OpenFirstPlayerTurnAsync(_gameId);
        ItIsThisPlayersTurn(playerTurnId, roundNumber: 1);

        int settlerId = await SeedSettlerAsync();

        await HandleAsync(settlerId, SettlementTier.Camp);

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        Assert.That(
            await context.MovementHistory.AnyAsync(row => row.UnitInstanceId == settlerId && row.EventType == ForceHistoryEventType.Destroyed),
            Is.False);
    }

    [Test, Description("A Founded row is written against the turn, which is what puts the founding into another player's intelligence feed if they watched it.")]
    public async Task ShouldRecordTheFoundingAgainstTheTurnAsync()
    {
        int playerTurnId = await OpenFirstPlayerTurnAsync(_gameId);
        ItIsThisPlayersTurn(playerTurnId, roundNumber: 1);

        int settlerId = await SeedSettlerAsync();

        await HandleAsync(settlerId, SettlementTier.Village, "Riverhold");

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        StructureHistory row = await context.StructureHistory.AsNoTracking().SingleAsync(r => r.GameId == _gameId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(row.EventType, Is.EqualTo(StructureHistoryEventType.Founded));
            Assert.That(row.PlayerId, Is.EqualTo(_playerId));
            Assert.That(row.PlayerTurnId, Is.EqualTo(playerTurnId));
            Assert.That(row.CampaignMapTileId, Is.EqualTo(_tileId));
            Assert.That(row.CompletionPercent, Is.Zero);
        }
    }

    [Test, Description("A settler with a standing order can still settle: the order is cleared with it, rather than its foreign key refusing the force's deletion.")]
    public async Task ShouldClearTheSettlersStandingOrdersAsync()
    {
        int playerTurnId = await OpenFirstPlayerTurnAsync(_gameId);
        ItIsThisPlayersTurn(playerTurnId, roundNumber: 1);

        int settlerId = await SeedSettlerAsync();
        int orderId = await SeedMoveOrderAsync(settlerId);

        ServiceResult<SettleForceResult> result = await HandleAsync(settlerId, SettlementTier.Village, "Riverhold");

        Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await context.ForceOrders.AnyAsync(o => o.Id == orderId), Is.False);
            Assert.That(await context.ForceOrderParticipants.AnyAsync(p => p.ForceOrderId == orderId), Is.False);
            Assert.That(await context.ForceOrderMovementSteps.AnyAsync(s => s.ForceOrderId == orderId), Is.False);
        }
    }

    [Test, Description("A structure already in the tile's Primary slot is razed to make room, keeping its history rather than being deleted out from under it.")]
    public async Task ShouldRazeTheStructureAlreadyHoldingThePrimarySlotAsync()
    {
        int playerTurnId = await OpenFirstPlayerTurnAsync(_gameId);
        ItIsThisPlayersTurn(playerTurnId, roundNumber: 1);

        int displacedId = await SeedStandingStructureAsync();
        int settlerId = await SeedSettlerAsync();

        ServiceResult<SettleForceResult> result = await HandleAsync(settlerId, SettlementTier.Village, "Riverhold");

        Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        StructureInstance displaced = await context.StructureInstances.AsNoTracking().SingleAsync(s => s.Id == displacedId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(displaced.RemovedOnTurnId, Is.EqualTo(playerTurnId), "Razing is a soft delete, so the row stays and its history keeps pointing at it.");
            Assert.That(
                await context.StructureHistory.AnyAsync(r => r.StructureInstanceId == displacedId && r.EventType == StructureHistoryEventType.Razed),
                Is.True);
        }
    }

    [Test, Description("A name already used in this game is refused by name, so the player is told which name to change rather than meeting a constraint violation.")]
    public async Task ShouldRefuseANameAlreadyUsedInThisGameAsync()
    {
        int playerTurnId = await OpenFirstPlayerTurnAsync(_gameId);
        ItIsThisPlayersTurn(playerTurnId, roundNumber: 1);

        int firstSettlerId = await SeedSettlerAsync();
        await HandleAsync(firstSettlerId, SettlementTier.Village, "Riverhold");

        int secondSettlerId = await SeedSettlerAsync();
        ServiceResult<SettleForceResult> result = await HandleAsync(secondSettlerId, SettlementTier.Village, "Riverhold");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(string.Join(" ", result.Errors), Does.Contain("Riverhold"));
        }
    }

    /// <summary>
    /// The index takes its case-insensitivity from the database's default collation, so a differently-cased duplicate has to be caught by the handler's own check. Letting it
    /// through to the database would turn a refusal the player can act on into a 500.
    /// </summary>
    [Test, Description("A name differing only in case is refused too, because the uniqueness index is case-insensitive and the database would otherwise refuse it as a 500.")]
    public async Task ShouldRefuseANameDifferingOnlyInCaseAsync()
    {
        int playerTurnId = await OpenFirstPlayerTurnAsync(_gameId);
        ItIsThisPlayersTurn(playerTurnId, roundNumber: 1);

        await HandleAsync(await SeedSettlerAsync(), SettlementTier.Village, "Riverhold");

        ServiceResult<SettleForceResult> result = await HandleAsync(await SeedSettlerAsync(), SettlementTier.Village, "riverhold");

        Assert.That(result.HasSucceeded, Is.False);
    }

    [Test, Description("The name is trimmed before it is stored, so a trailing space cannot sidestep the uniqueness index nor reach the map label.")]
    public async Task ShouldTrimTheNameBeforeStoringItAsync()
    {
        int playerTurnId = await OpenFirstPlayerTurnAsync(_gameId);
        ItIsThisPlayersTurn(playerTurnId, roundNumber: 1);

        ServiceResult<SettleForceResult> result = await HandleAsync(await SeedSettlerAsync(), SettlementTier.Village, "  Riverhold  ");

        Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That((await context.Settlements.AsNoTracking().SingleAsync()).Name, Is.EqualTo("Riverhold"));
            Assert.That(result.SuccessValue!.Name, Is.EqualTo("Riverhold"), "The response carries what was stored, not what was sent.");
        }
    }

    /// <summary>
    /// A camp is a place the settlers stopped rather than one they founded, so the player is never asked to name it and
    /// nothing invents one for them. <c>SettlementTierRules.IsNamed</c> is what says which tiers are which.
    /// </summary>
    [Test, Description("A camp is founded with no name at all, because a camp is a tier the player does not name.")]
    public async Task ShouldFoundACampWithNoNameAsync()
    {
        int playerTurnId = await OpenFirstPlayerTurnAsync(_gameId);
        ItIsThisPlayersTurn(playerTurnId, roundNumber: 1);

        int settlerId = await SeedSettlerAsync();

        ServiceResult<SettleForceResult> result = await HandleAsync(settlerId, SettlementTier.Camp);

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        Settlement settlement = await context.Settlements.AsNoTracking().SingleAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));
            Assert.That(settlement.Name, Is.Null);
            Assert.That(settlement.Tier, Is.EqualTo(SettlementTier.Camp));
            Assert.That(result.SuccessValue!.Name, Is.Null, "The response says there is no name as plainly as the row does.");
        }
    }

    /// <summary>
    /// The rule is the server's, so a request that ignores it is corrected rather than obeyed: a client sending a name for an
    /// unnamed tier cannot make a camp carry one.
    /// </summary>
    [Test, Description("A name sent for a camp is dropped rather than stored, because which tiers are named is the server's rule.")]
    public async Task ShouldIgnoreANameSentForACampAsync()
    {
        int playerTurnId = await OpenFirstPlayerTurnAsync(_gameId);
        ItIsThisPlayersTurn(playerTurnId, roundNumber: 1);

        int settlerId = await SeedSettlerAsync();

        ServiceResult<SettleForceResult> result = await HandleAsync(settlerId, SettlementTier.Camp, "First Fire");

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));
            Assert.That((await context.Settlements.AsNoTracking().SingleAsync()).Name, Is.Null);
        }
    }

    /// <summary>
    /// The regression this pins: SQL Server treats NULLs as equal in a unique index, so an unfiltered
    /// <c>UX_Settlement_Game_Name</c> would let a Game hold exactly one unnamed Settlement and reject the second. The filter
    /// on <c>[Name] IS NOT NULL</c> is what keeps uniqueness to the Settlements that actually have a name.
    /// </summary>
    [Test, Description("A second camp can be founded in the same game, because the name uniqueness rule only covers settlements that have one.")]
    public async Task ShouldAllowManyUnnamedSettlementsInOneGameAsync()
    {
        int playerTurnId = await OpenFirstPlayerTurnAsync(_gameId);
        ItIsThisPlayersTurn(playerTurnId, roundNumber: 1);

        ServiceResult<SettleForceResult> first = await HandleAsync(await SeedSettlerAsync(), SettlementTier.Camp);
        ServiceResult<SettleForceResult> second = await HandleAsync(await SeedSettlerAsync(), SettlementTier.Camp);

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(first.HasSucceeded, Is.True, () => string.Join("; ", first.Errors));
            Assert.That(second.HasSucceeded, Is.True, () => string.Join("; ", second.Errors));
            Assert.That(await context.Settlements.CountAsync(s => s.Name == null), Is.EqualTo(2));
        }
    }

    [Test, Description("A blank or over-long name is refused, and nothing is founded, so the force survives a refusal the player can correct.")]
    [TestCase("", Description = "Empty.")]
    [TestCase("   ", Description = "Nothing but whitespace, which trims to empty.")]
    [TestCase("This settlement name is far too long to store", Description = "Over the 30-character limit.")]
    public async Task ShouldRefuseANameItCannotStoreAsync(string name)
    {
        int playerTurnId = await OpenFirstPlayerTurnAsync(_gameId);
        ItIsThisPlayersTurn(playerTurnId, roundNumber: 1);

        int settlerId = await SeedSettlerAsync();

        ServiceResult<SettleForceResult> result = await HandleAsync(settlerId, SettlementTier.Village, name);

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(await context.Settlements.AnyAsync(), Is.False);
            Assert.That(await context.UnitInstances.AnyAsync(u => u.Id == settlerId), Is.True, "A refused founding leaves the force where it was.");
        }
    }

    /// <summary>
    /// The strategy's refusal is the player-facing one - "has no campaign movement left this turn" - and the handler passes it through rather than rewording it, so the
    /// message the disabled button showed is the message the refusal gives.
    /// </summary>
    [Test, Description("When the rules refuse the action, the founding is refused with their own wording and nothing is created.")]
    public async Task ShouldRefuseWhenTheRulesSayNoAsync()
    {
        int playerTurnId = await OpenFirstPlayerTurnAsync(_gameId);
        ItIsThisPlayersTurn(playerTurnId, roundNumber: 1);

        _contextActionService
            .EvaluateActionAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<ForceActionType>(), Arg.Any<CancellationToken>())
            .Returns(ServiceResult<ForceActionAvailability>.Succeed(
                ForceActionAvailability.Blocked(["Settlers has no campaign movement left this turn."])));

        int settlerId = await SeedSettlerAsync();

        ServiceResult<SettleForceResult> result = await HandleAsync(settlerId, SettlementTier.Village, "Riverhold");

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(result.Errors, Does.Contain("Settlers has no campaign movement left this turn."));
            Assert.That(await context.Settlements.AnyAsync(), Is.False);
            Assert.That(await context.UnitInstances.AnyAsync(u => u.Id == settlerId), Is.True);
        }
    }

    [Test, Description("A tier reached by growth cannot be founded directly, so asking for one is refused rather than falling through to no structure at all.")]
    public async Task ShouldRefuseATierThatCannotBeFoundedAsync()
    {
        int playerTurnId = await OpenFirstPlayerTurnAsync(_gameId);
        ItIsThisPlayersTurn(playerTurnId, roundNumber: 1);

        ServiceResult<SettleForceResult> result = await HandleAsync(await SeedSettlerAsync(), SettlementTier.City, "Riverhold");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(string.Join(" ", result.Errors), Does.Contain("City"));
        }
    }

    [Test, Description("Another player's force cannot be settled, since the game token ties the caller to their own forces and the handler checks rather than trusting it.")]
    public async Task ShouldRefuseAForceTheCallerDoesNotOwnAsync()
    {
        int playerTurnId = await OpenFirstPlayerTurnAsync(_gameId);
        ItIsThisPlayersTurn(playerTurnId, roundNumber: 1);

        int otherPlayersSettlerId = await SeedSettlerAsync(_otherPlayerId);

        ServiceResult<SettleForceResult> result = await HandleAsync(otherPlayersSettlerId, SettlementTier.Village, "Riverhold");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.Forbidden));
        }
    }

    [Test, Description("Nothing can be founded outside the player's own turn, the same gate stepping a force applies, since the toolbar itself does not check whose turn it is.")]
    public async Task ShouldRefuseOutsideThePlayersOwnTurnAsync()
    {
        int playerTurnId = await OpenFirstPlayerTurnAsync(_gameId);

        // The turn in progress belongs to somebody else.
        _campaignTurnService
            .GetCurrentCampaignTurnAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(ServiceResult<CurrentCampaignTurn>.Succeed(new CurrentCampaignTurn
            {
                PlayerId = _otherPlayerId,
                PlayerTurnId = playerTurnId,
                CurrentRoundNumber = 1,
                FactionName = "The Other Lot"
            }));

        int settlerId = await SeedSettlerAsync();

        ServiceResult<SettleForceResult> result = await HandleAsync(settlerId, SettlementTier.Village, "Riverhold");

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.Forbidden));
            Assert.That(await context.Settlements.AnyAsync(), Is.False);
        }
    }

    [Test, Description("A force that is not in the game is a not-found rather than a refusal, since there is nothing for the rules to judge.")]
    public async Task ShouldReportAMissingForceAsNotFoundAsync()
    {
        int playerTurnId = await OpenFirstPlayerTurnAsync(_gameId);
        ItIsThisPlayersTurn(playerTurnId, roundNumber: 1);

        ServiceResult<SettleForceResult> result = await HandleAsync(unitInstanceId: 999_999, SettlementTier.Village, "Riverhold");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.NotFound));
        }
    }

    /// <summary>
    /// The catalogue is seeded rather than migrated, so a database nobody has seeded reaches this. It is a configuration fault, and the refusal says which seed to run rather
    /// than leaving the player with a founding that silently did nothing.
    /// </summary>
    [Test, Description("With the structure catalogue unseeded the founding is refused and names the seed to run, and the settler is left standing.")]
    public async Task ShouldRefuseWhenTheStructureCatalogueIsUnseededAsync()
    {
        int playerTurnId = await OpenFirstPlayerTurnAsync(_gameId);
        ItIsThisPlayersTurn(playerTurnId, roundNumber: 1);

        await using (AscendantEmpireContext setup = await DbFactory.CreateDbContextAsync())
        {
            Structure village = await setup.Structures.SingleAsync(s => s.Id == _villageStructureId);
            setup.Structures.Remove(village);
            await setup.SaveChangesAsync();
        }

        int settlerId = await SeedSettlerAsync();

        ServiceResult<SettleForceResult> result = await HandleAsync(settlerId, SettlementTier.Village, "Riverhold");

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(string.Join(" ", result.Errors), Does.Contain("Seed"));
            Assert.That(await context.UnitInstances.AnyAsync(u => u.Id == settlerId), Is.True);
        }
    }

    [Test, Description("Founding a village creates the empty asset bank it is born with, so the settlement holds something from its first turn and anything it builds can have a cost.")]
    public async Task ShouldGiveANewVillageItsAssetBankAsync()
    {
        int playerTurnId = await OpenFirstPlayerTurnAsync(_gameId);
        ItIsThisPlayersTurn(playerTurnId, roundNumber: 1);

        int settlerId = await SeedSettlerAsync();

        ServiceResult<SettleForceResult> result = await HandleAsync(settlerId, SettlementTier.Village, "Riverhold");

        Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        AssetBank bank = await context.AssetBanks.SingleAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(bank.PlayerId, Is.EqualTo(_playerId), "The bank belongs to the founding player.");
            Assert.That(bank.SettlementId, Is.EqualTo(result.SuccessValue!.SettlementId), "And it is the new village's own store.");
            Assert.That(bank.NetworkKey, Is.Not.EqualTo(Guid.Empty), "Keyed on the way out of the real pipeline, not only in the service in isolation.");
            Assert.That(await context.AssetBankBalances.AnyAsync(), Is.False, "Born empty: nothing seeds a starting quantity.");
        }
    }

    [Test, Description("Founding a camp creates the empty asset bank it is born with, because a camp is the first settlement of a game and what it builds has to have somewhere to bank.")]
    public async Task ShouldGiveANewCampItsAssetBankAsync()
    {
        int playerTurnId = await OpenFirstPlayerTurnAsync(_gameId);
        ItIsThisPlayersTurn(playerTurnId, roundNumber: 1);

        int settlerId = await SeedSettlerAsync();

        ServiceResult<SettleForceResult> result = await HandleAsync(settlerId, SettlementTier.Camp);

        Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        AssetBank bank = await context.AssetBanks.SingleAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(bank.PlayerId, Is.EqualTo(_playerId), "The bank belongs to the founding player.");
            Assert.That(bank.SettlementId, Is.EqualTo(result.SuccessValue!.SettlementId), "And it is the new camp's own store.");
            Assert.That(await context.AssetBankBalances.AnyAsync(), Is.False, "Born empty: nothing seeds a starting quantity.");
        }
    }

    /// <summary>Tells the substituted turn service that the Turn in progress is this Player's.</summary>
    private void ItIsThisPlayersTurn(int playerTurnId, int roundNumber)
    {
        _campaignTurnService
            .GetCurrentCampaignTurnAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(ServiceResult<CurrentCampaignTurn>.Succeed(new CurrentCampaignTurn
            {
                PlayerId = _playerId,
                PlayerTurnId = playerTurnId,
                CurrentRoundNumber = roundNumber,
                FactionName = "Us"
            }));
    }

    private async Task<ServiceResult<SettleForceResult>> HandleAsync(int unitInstanceId, SettlementTier tier, string? name = null)
    {
        var playerTurnService = new PlayerTurnService(NullLogger<PlayerTurnService>.Instance, DbFactory);

        var handler = new SettleForceHandler(
            NullLogger<SettleForceHandler>.Instance,
            DbFactory,
            _campaignTurnService,
            _contextActionService,
            new ForceLifecycleWriter(NullLogger<ForceLifecycleWriter>.Instance, playerTurnService),
            new StructureLifecycleWriter(NullLogger<StructureLifecycleWriter>.Instance, playerTurnService),
            new AssetBankProvisioningService(
                NullLogger<AssetBankProvisioningService>.Instance,
                new AssetNetworkConnectivityService(NullLogger<AssetNetworkConnectivityService>.Instance)));

        return await handler.Handle(new SettleForceRequest(_gameId, _playerId, unitInstanceId, tier, name), CancellationToken.None);
    }

    /// <summary>A settler standing on the fixture's tile with a full turn's movement, saved and ready to settle.</summary>
    /// <param name="healthyCount">Defaults to the whole settler. Varied by the tests that pin which pool a camp's founders are taken from.</param>
    private async Task<int> SeedSettlerAsync(int? playerId = null, int? healthyCount = null, int injuredCount = 0)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        int healthy = healthyCount ?? _SETTLER_SIZE;

        var settler = new UnitInstance
        {
            GameId = _gameId,
            PlayerId = playerId ?? _playerId,
            CampaignMapTileId = _tileId,
            UnitTemplateId = _settlerTemplateId,
            InitialStrength = healthy + injuredCount,
            HealthyCount = healthy,
            InjuredCount = injuredCount,
            CampaignMovementRemaining = _STARTING_MOVEMENT
        };

        context.UnitInstances.Add(settler);
        await context.SaveChangesAsync();

        return settler.Id;
    }

    /// <summary>A village already standing in the tile's Primary slot, for the displacement test.</summary>
    private async Task<int> SeedStandingStructureAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        var standing = new StructureInstance
        {
            GameId = _gameId,
            CampaignMapTileId = _tileId,
            StructureId = _villageStructureId,
            SlotType = TileSlotType.Primary,
            Position = PositionOnTile.WholeTile,
            OwnedByPlayerId = _playerId,
            CreatedOnTurnId = 0,
            // Standing and finished, so the settle has something to raze: all its work done, which for this kind is its full total.
            BuildPointsProgress = _VILLAGE_BUILD_POINTS
        };

        context.StructureInstances.Add(standing);
        await context.SaveChangesAsync();

        return standing.Id;
    }

    /// <summary>A standing move order for the settler, with a participant row and a movement step, so the deletion has all three to clear.</summary>
    private async Task<int> SeedMoveOrderAsync(int unitInstanceId)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        var order = new ForceOrder
        {
            GameId = _gameId,
            UnitInstanceId = unitInstanceId,
            OrderType = ForceOrderType.Move,
            OrderStatus = ForceOrderStatus.Pending,
            TargetCampaignMapTileId = _tileId
        };

        context.ForceOrders.Add(order);
        await context.SaveChangesAsync();

        context.ForceOrderParticipants.Add(new ForceOrderParticipant
        {
            ForceOrderId = order.Id,
            UnitInstanceId = unitInstanceId,
            Role = ForceOrderParticipantRole.Primary
        });

        context.ForceOrderMovementSteps.Add(new ForceOrderMovementStep
        {
            ForceOrderId = order.Id,
            StepIndex = 0,
            CampaignMapTileId = _tileId,
            MovementCost = 1,
            IsCompleted = false
        });

        await context.SaveChangesAsync();

        return order.Id;
    }
}
