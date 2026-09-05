using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Turns.Services.TurnPipeline;
using AscendantEmpire.Application.Features.Turns.Services.TurnPipeline.Campaign.StartTurn;
using AscendantEmpire.Domain.Entities.UnitDesigns;
using AscendantEmpire.Domain.Entities.Units;
using AscendantEmpire.Domain.Enums.Turns;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace AscendantEmpire.Tests.Features.Turns.Services.TurnPipeline;

/// <summary>
/// Tests for <see cref="ReplenishMovementPoints"/> — who gets a movement allowance when a Campaign Turn opens, and how often.
/// </summary>
/// <remarks>
/// <para>
/// On the in-memory tier. The process loads and saves through the change tracker rather than <c>ExecuteUpdateAsync</c>, and it reads the allowance as a
/// <b>scalar</b> projection off <see cref="CompiledUnitTemplate"/> — never materialising the entity, whose <c>Attack</c>/<c>Resistance</c> complex types
/// the in-memory provider cannot read back. If a future change makes it materialise that snapshot, these tests will fail in the query shaper and must
/// move to <see cref="SqlServerTestBase"/>.
/// </para>
/// <para>
/// The arithmetic itself is pinned separately, without a database, in <see cref="MovementReplenishmentTests"/>. What is tested here is the orchestration
/// around it.
/// </para>
/// </remarks>
[TestFixture]
internal class ReplenishMovementPointsTests : InMemoryTestBase
{
    private const int _GAME_ID = 1;
    private const int _OTHER_GAME_ID = 2;
    private const int _PLAYER_ID = 1;
    private const int _OTHER_PLAYER_ID = 2;
    private const int _TILE_ID = 112;
    private const int _TEMPLATE_ID = 1;
    private const int _UNCOMPILED_TEMPLATE_ID = 2;
    private const int _CURRENT_ROUND_NUMBER = 3;

    /// <summary>A 2.2 MP allowance — the fractional remainder is what makes the carry visible.</summary>
    private const long _CAMPAIGN_MOVEMENT_POINTS = 2_200;

    private const long _BATTLEFIELD_MOVEMENT_POINTS = 4_000;

    private ReplenishMovementPoints _process = null!;

    [SetUp]
    public async Task SetUp()
    {
        _process = new ReplenishMovementPoints(DbFactory, NullLogger<ReplenishMovementPoints>.Instance);

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        context.UnitTemplates.Add(new UnitTemplate
        {
            Id = _TEMPLATE_ID,
            DisplayName = "Cavalry",
            MapImagePath = "cavalry.png",
            BodyComponentId = 1
        });

        context.CompiledUnitTemplates.Add(new CompiledUnitTemplate
        {
            UnitTemplateId = _TEMPLATE_ID,
            DisplayName = "Cavalry",
            MapImagePath = "cavalry.png",
            BodyComponentId = 1,
            CampaignMovementPoints = _CAMPAIGN_MOVEMENT_POINTS,
            BattlefieldMovementPoints = _BATTLEFIELD_MOVEMENT_POINTS
        });

        // Deliberately has no compiled snapshot — a template that was never compiled, or whose snapshot has gone since a Unit was fielded from it.
        context.UnitTemplates.Add(new UnitTemplate
        {
            Id = _UNCOMPILED_TEMPLATE_ID,
            DisplayName = "Orphan",
            MapImagePath = "orphan.png",
            BodyComponentId = 1
        });

        await context.SaveChangesAsync();
    }

    [Test, Description("A Force is given its compiled template's allowance plus the carry the replenishment rule allows when its owner's Turn opens.")]
    public async Task ShouldGrantTheAllowancePlusTheCarryAsync()
    {
        await AddForceAsync(unitInstanceId: 1, campaignMovementRemaining: 200);

        string error = await _process.ProcessAsync(StartTurnContext());

        UnitInstance force = await ReloadForceAsync(1);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(error, Is.Empty);
            Assert.That(force.CampaignMovementRemaining, Is.EqualTo(2_400), "2.2 granted, with the 0.2 it did not spend carried on top.");
        }
    }

    [Test, Description("The Round stamp is advanced to the Round the allowance was granted in, so the grant can be told apart from one made in an earlier Round.")]
    public async Task ShouldStampTheRoundTheAllowanceWasGrantedInAsync()
    {
        await AddForceAsync(unitInstanceId: 1, campaignMovementRemaining: 0);

        await _process.ProcessAsync(StartTurnContext());

        UnitInstance force = await ReloadForceAsync(1);

        Assert.That(force.CampaignMovementReplenishedOnRound, Is.EqualTo(_CURRENT_ROUND_NUMBER));
    }

    [Test, Description("Running the process twice in one Round grants once: without the stamp the second run would see a leftover at or above the allowance, bank nothing, and silently destroy the carry the first run just gave.")]
    public async Task ShouldGrantOnlyOncePerRoundAsync()
    {
        await AddForceAsync(unitInstanceId: 1, campaignMovementRemaining: 200);

        await _process.ProcessAsync(StartTurnContext());
        await _process.ProcessAsync(StartTurnContext());

        UnitInstance force = await ReloadForceAsync(1);

        Assert.That(force.CampaignMovementRemaining, Is.EqualTo(2_400), "The carry survives the second run rather than being reset to a plain 2.2.");
    }

    [Test, Description("A Force already stamped for the Round being opened is left alone, which is what stops the starting Units game setup fields before Round 1 from being granted a second allowance.")]
    public async Task ShouldLeaveAForceAlreadyStampedForThisRoundUntouchedAsync()
    {
        await AddForceAsync(unitInstanceId: 1, campaignMovementRemaining: 500, replenishedOnRound: _CURRENT_ROUND_NUMBER);

        await _process.ProcessAsync(StartTurnContext());

        UnitInstance force = await ReloadForceAsync(1);

        Assert.That(force.CampaignMovementRemaining, Is.EqualTo(500));
    }

    [Test, Description("Only the Player whose Turn is starting is replenished — another Player in the same Game keeps whatever their last Turn left them.")]
    public async Task ShouldNotReplenishAnotherPlayersForcesAsync()
    {
        await AddForceAsync(unitInstanceId: 1, campaignMovementRemaining: 500, playerId: _OTHER_PLAYER_ID);

        await _process.ProcessAsync(StartTurnContext());

        UnitInstance force = await ReloadForceAsync(1);

        Assert.That(force.CampaignMovementRemaining, Is.EqualTo(500));
    }

    [Test, Description("Forces belonging to another Game are left alone, so opening a Turn in one Game cannot reach into another.")]
    public async Task ShouldNotReplenishForcesInAnotherGameAsync()
    {
        await AddForceAsync(unitInstanceId: 1, campaignMovementRemaining: 500, gameId: _OTHER_GAME_ID);

        await _process.ProcessAsync(StartTurnContext());

        UnitInstance force = await ReloadForceAsync(1);

        Assert.That(force.CampaignMovementRemaining, Is.EqualTo(500));
    }

    [Test, Description("A Force whose template has no compiled snapshot is skipped and left unstamped rather than failing the Player's Turn, so the grant is retried next Round once a recompile may have fixed it.")]
    public async Task ShouldSkipAForceWithNoCompiledSnapshotAsync()
    {
        await AddForceAsync(unitInstanceId: 1, campaignMovementRemaining: 500, unitTemplateId: _UNCOMPILED_TEMPLATE_ID);

        string error = await _process.ProcessAsync(StartTurnContext());

        UnitInstance force = await ReloadForceAsync(1);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(error, Is.Empty, "The pipeline is fail-fast, so an orphaned Force must not be allowed to block the whole Turn.");
            Assert.That(force.CampaignMovementRemaining, Is.EqualTo(500));
            Assert.That(force.CampaignMovementReplenishedOnRound, Is.Zero, "Left unstamped so the grant is retried.");
        }
    }

    [Test, Description("A Force standing on a Battle Map still receives its campaign allowance, so it is not frozen on the campaign map when the Battle releases it.")]
    public async Task ShouldReplenishAForceOnABattleMapAsync()
    {
        await AddForceAsync(unitInstanceId: 1, campaignMovementRemaining: 0, battleMapTileId: 77);

        await _process.ProcessAsync(StartTurnContext());

        UnitInstance force = await ReloadForceAsync(1);

        Assert.That(force.CampaignMovementRemaining, Is.EqualTo(_CAMPAIGN_MOVEMENT_POINTS));
    }

    [Test, Description("A wiped-out Force is not replenished, matching the possible-action checks which already refuse to report one as having movement.")]
    public async Task ShouldNotReplenishAWipedOutForceAsync()
    {
        await AddForceAsync(unitInstanceId: 1, campaignMovementRemaining: 0, healthyCount: 0, injuredCount: 0);

        await _process.ProcessAsync(StartTurnContext());

        UnitInstance force = await ReloadForceAsync(1);

        Assert.That(force.CampaignMovementRemaining, Is.Zero);
    }

    [Test, Description("Battle movement is untouched by campaign replenishment, because Battle Rounds are not Campaign Rounds and the two pools run on different clocks.")]
    public async Task ShouldLeaveBattleMovementUntouchedAsync()
    {
        await AddForceAsync(unitInstanceId: 1, campaignMovementRemaining: 0, battleMovementRemaining: 1_000);

        await _process.ProcessAsync(StartTurnContext());

        UnitInstance force = await ReloadForceAsync(1);

        Assert.That(force.BattleMovementRemaining, Is.EqualTo(1_000));
    }

    [Test, Description("A start-of-Turn context carrying no Player Id fails the pipeline rather than replenishing every Player in the Game.")]
    public async Task ShouldFailWhenTheContextCarriesNoPlayerAsync()
    {
        await AddForceAsync(unitInstanceId: 1, campaignMovementRemaining: 500);

        var contextWithNoPlayer = new TurnPipelineContextData(TurnPipelineType.StartCampaignTurn, _GAME_ID, _CURRENT_ROUND_NUMBER);

        string error = await _process.ProcessAsync(contextWithNoPlayer);

        UnitInstance force = await ReloadForceAsync(1);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(error, Is.Not.Empty);
            Assert.That(force.CampaignMovementRemaining, Is.EqualTo(500), "Nothing was granted.");
        }
    }

    // ── arrangement ───────────────────────────────────────────────────────────

    private static TurnPipelineContextData StartTurnContext() =>
        new(TurnPipelineType.StartCampaignTurn, _GAME_ID, _PLAYER_ID, _CURRENT_ROUND_NUMBER);

    /// <summary>Adds a Force, defaulting to the acting Player's own full-strength cavalry on the campaign map.</summary>
    private async Task AddForceAsync(
        int unitInstanceId,
        long campaignMovementRemaining,
        int gameId = _GAME_ID,
        int playerId = _PLAYER_ID,
        int unitTemplateId = _TEMPLATE_ID,
        int healthyCount = 100,
        int injuredCount = 0,
        int? battleMapTileId = null,
        long battleMovementRemaining = 0,
        int replenishedOnRound = 0)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        context.UnitInstances.Add(new UnitInstance
        {
            Id = unitInstanceId,
            GameId = gameId,
            PlayerId = playerId,
            CampaignMapTileId = _TILE_ID,
            BattleMapTileId = battleMapTileId,
            UnitTemplateId = unitTemplateId,
            InitialStrength = healthyCount + injuredCount,
            HealthyCount = healthyCount,
            InjuredCount = injuredCount,
            CampaignMovementRemaining = campaignMovementRemaining,
            BattleMovementRemaining = battleMovementRemaining,
            CampaignMovementReplenishedOnRound = replenishedOnRound
        });

        await context.SaveChangesAsync();
    }

    private async Task<UnitInstance> ReloadForceAsync(int unitInstanceId)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        return await context.UnitInstances.SingleAsync(u => u.Id == unitInstanceId);
    }
}
