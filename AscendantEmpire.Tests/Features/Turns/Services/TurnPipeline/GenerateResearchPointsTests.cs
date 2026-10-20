using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Turns.Services.TurnPipeline;
using AscendantEmpire.Application.Features.Turns.Services.TurnPipeline.Campaign.StartTurn;
using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Entities.Settlements;
using AscendantEmpire.Domain.Entities.Technologies;
using AscendantEmpire.Domain.Entities.UnitDesigns;
using AscendantEmpire.Domain.Entities.Units;
using AscendantEmpire.Domain.Enums.Settlements;
using AscendantEmpire.Domain.Enums.Structures;
using AscendantEmpire.Domain.Enums.Turns;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace AscendantEmpire.Tests.Features.Turns.Services.TurnPipeline;

/// <summary>
/// Tests for <see cref="GenerateResearchPoints"/> — whose people count toward a Player's directed research, and how often they are counted.
/// </summary>
/// <remarks>
/// On the in-memory tier. The process loads and saves one row through the change tracker and sums two scalar projections; it filters on
/// <see cref="CompiledUnitTemplate"/>'s capability collection but never materialises the snapshot, whose <c>Attack</c>/<c>Resistance</c> complex types the
/// in-memory provider cannot read back. If a future change makes it materialise that snapshot, these tests will fail in the query shaper and must move to
/// <see cref="SqlServerTestBase"/>.
/// </remarks>
[TestFixture]
internal class GenerateResearchPointsTests : InMemoryTestBase
{
    private const int _GAME_ID = 1;
    private const int _OTHER_GAME_ID = 2;
    private const int _PLAYER_ID = 1;
    private const int _OTHER_PLAYER_ID = 2;
    private const int _TILE_ID = 112;
    private const int _CURRENT_ROUND_NUMBER = 3;

    /// <summary>A settler template: carries both camping capabilities, as the <c>camping-materials</c> component grants them together.</summary>
    private const int _SETTLER_TEMPLATE_ID = 1;

    /// <summary>A template carrying <c>create-early-camp</c> and nothing else, so the second half of the capability test is reachable on its own.</summary>
    private const int _CAMP_ONLY_TEMPLATE_ID = 2;

    /// <summary>A template with no camping capability at all — soldiers, who research nothing by standing there.</summary>
    private const int _SOLDIER_TEMPLATE_ID = 3;

    /// <summary>The divisor every test runs with, matching the figure game setup writes.</summary>
    private const int _TECH_POINTS_PER_POPULATION_SIZE = 100;

    private GenerateResearchPoints _process = null!;

    [SetUp]
    public async Task SetUp()
    {
        _process = new GenerateResearchPoints(DbFactory, NullLogger<GenerateResearchPoints>.Instance);

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        AddTemplate(context, _SETTLER_TEMPLATE_ID, "Settlers", [CapabilityTagCodes.CREATE_SETTLEMENT, CapabilityTagCodes.CREATE_EARLY_CAMP]);
        AddTemplate(context, _CAMP_ONLY_TEMPLATE_ID, "Outriders", [CapabilityTagCodes.CREATE_EARLY_CAMP]);
        AddTemplate(context, _SOLDIER_TEMPLATE_ID, "Spearmen", []);

        context.PlayerResearchStates.Add(new PlayerResearchState
        {
            PlayerId = _PLAYER_ID,
            GameId = _GAME_ID,
            TechPointsPerPopulationSize = _TECH_POINTS_PER_POPULATION_SIZE
        });

        await context.SaveChangesAsync();
    }

    [Test, Description("Settled people and people still carrying their camp are one pool divided once, so a hundred of each produces two points rather than none.")]
    public async Task ShouldSumSettlementAndCampingPopulationBeforeDividingAsync()
    {
        await AddSettlementAsync(settlementId: 1, structureInstanceId: 1, population: 150);
        await AddForceAsync(unitInstanceId: 1, healthyCount: 50);

        string error = await _process.ProcessAsync(StartTurnContext());

        PlayerResearchState state = await ReloadStateAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(error, Is.Empty);
            Assert.That(state.UnspentDirectedPoints, Is.EqualTo(2), "200 people at 100 per point. Divided separately each would have floored to 1.");
        }
    }

    [Test, Description("A population short of the divisor produces nothing rather than a fraction, because a research point is a whole thing.")]
    public async Task ShouldGenerateNothingWhenThePopulationIsBelowTheDivisorAsync()
    {
        await AddSettlementAsync(settlementId: 1, structureInstanceId: 1, population: 99);

        await _process.ProcessAsync(StartTurnContext());

        PlayerResearchState state = await ReloadStateAsync();

        Assert.That(state.UnspentDirectedPoints, Is.Zero);
    }

    [Test, Description("A settlement still being built counts in full: its settlers left the map and became its people the moment it was founded, so excluding it would make them vanish for the several Turns construction takes.")]
    public async Task ShouldCountASettlementStillUnderConstructionAsync()
    {
        await AddSettlementAsync(settlementId: 1, structureInstanceId: 1, population: 300, completedOnTurnId: null);

        await _process.ProcessAsync(StartTurnContext());

        PlayerResearchState state = await ReloadStateAsync();

        Assert.That(state.UnspentDirectedPoints, Is.EqualTo(3));
    }

    [Test, Description("A razed settlement counts nothing. The Settlement row survives a raze — the removal is stamped on the Structure — so without the filter its dead would go on researching for ever.")]
    public async Task ShouldIgnoreARazedSettlementAsync()
    {
        await AddSettlementAsync(settlementId: 1, structureInstanceId: 1, population: 300, removedOnTurnId: 9);

        await _process.ProcessAsync(StartTurnContext());

        PlayerResearchState state = await ReloadStateAsync();

        Assert.That(state.UnspentDirectedPoints, Is.Zero);
    }

    [Test, Description("A captured settlement researches for whoever holds it now, not for whoever founded it, because ownership is the live fact and the founder never changes.")]
    public async Task ShouldCountASettlementByItsOwnerRatherThanItsFounderAsync()
    {
        await AddSettlementAsync(
            settlementId: 1,
            structureInstanceId: 1,
            population: 400,
            ownedByPlayerId: _PLAYER_ID,
            foundingPlayerId: _OTHER_PLAYER_ID);

        await _process.ProcessAsync(StartTurnContext());

        PlayerResearchState state = await ReloadStateAsync();

        Assert.That(state.UnspentDirectedPoints, Is.EqualTo(4));
    }

    [Test, Description("A settlement this Player founded but has since lost counts for them no longer, which is the other half of ownership deciding.")]
    public async Task ShouldIgnoreASettlementThisPlayerFoundedButNoLongerOwnsAsync()
    {
        await AddSettlementAsync(
            settlementId: 1,
            structureInstanceId: 1,
            population: 400,
            ownedByPlayerId: _OTHER_PLAYER_ID,
            foundingPlayerId: _PLAYER_ID);

        await _process.ProcessAsync(StartTurnContext());

        PlayerResearchState state = await ReloadStateAsync();

        Assert.That(state.UnspentDirectedPoints, Is.Zero);
    }

    [Test, Description("Either camping capability is enough. They are authored separately onto the same component, so matching only one would quietly halve a Force that carries the other.")]
    [TestCase(_SETTLER_TEMPLATE_ID, 2, Description = "A Force that can found a settlement counts.")]
    [TestCase(_CAMP_ONLY_TEMPLATE_ID, 2, Description = "A Force that can only establish a camp counts too.")]
    [TestCase(_SOLDIER_TEMPLATE_ID, 0, Description = "A Force with neither capability carries no settlers and counts nothing.")]
    public async Task ShouldCountAForceByItsCampingCapabilityAsync(int unitTemplateId, int expectedPoints)
    {
        await AddForceAsync(unitInstanceId: 1, healthyCount: 200, unitTemplateId: unitTemplateId);

        await _process.ProcessAsync(StartTurnContext());

        PlayerResearchState state = await ReloadStateAsync();

        Assert.That(state.UnspentDirectedPoints, Is.EqualTo(expectedPoints));
    }

    [Test, Description("Injured settlers are counted alongside healthy ones, matching how a Settlement's population is seeded from the whole strength of the Force that founded it.")]
    public async Task ShouldCountInjuredSettlersAlongsideHealthyOnesAsync()
    {
        await AddForceAsync(unitInstanceId: 1, healthyCount: 150, injuredCount: 50);

        await _process.ProcessAsync(StartTurnContext());

        PlayerResearchState state = await ReloadStateAsync();

        Assert.That(state.UnspentDirectedPoints, Is.EqualTo(2));
    }

    [Test, Description("Another Player's settlements and Forces in the same Game produce nothing for the Player whose Turn is opening, because research is per empire.")]
    public async Task ShouldIgnoreAnotherPlayersPeopleAsync()
    {
        await AddSettlementAsync(settlementId: 1, structureInstanceId: 1, population: 500, ownedByPlayerId: _OTHER_PLAYER_ID, foundingPlayerId: _OTHER_PLAYER_ID);
        await AddForceAsync(unitInstanceId: 1, healthyCount: 500, playerId: _OTHER_PLAYER_ID);

        await _process.ProcessAsync(StartTurnContext());

        PlayerResearchState state = await ReloadStateAsync();

        Assert.That(state.UnspentDirectedPoints, Is.Zero);
    }

    [Test, Description("People in another Game produce nothing, so opening a Turn in one Game cannot reach into another.")]
    public async Task ShouldIgnorePeopleInAnotherGameAsync()
    {
        await AddSettlementAsync(settlementId: 1, structureInstanceId: 1, population: 500, gameId: _OTHER_GAME_ID);
        await AddForceAsync(unitInstanceId: 1, healthyCount: 500, gameId: _OTHER_GAME_ID);

        await _process.ProcessAsync(StartTurnContext());

        PlayerResearchState state = await ReloadStateAsync();

        Assert.That(state.UnspentDirectedPoints, Is.Zero);
    }

    [Test, Description("The Round stamp is advanced to the Round the points were generated in, so a grant made this Round can be told from one made earlier.")]
    public async Task ShouldStampTheRoundThePointsWereGeneratedInAsync()
    {
        await AddSettlementAsync(settlementId: 1, structureInstanceId: 1, population: 100);

        await _process.ProcessAsync(StartTurnContext());

        PlayerResearchState state = await ReloadStateAsync();

        Assert.That(state.LastResearchGeneratedOnRound, Is.EqualTo(_CURRENT_ROUND_NUMBER));
    }

    [Test, Description("Running twice in one Round generates once. A retried start of Turn must not re-run the sum after the Player has already spent against it.")]
    public async Task ShouldGenerateOnlyOncePerRoundAsync()
    {
        await AddSettlementAsync(settlementId: 1, structureInstanceId: 1, population: 100);

        await _process.ProcessAsync(StartTurnContext());

        // Stands in for the Player spending during their Turn: a second run must leave this alone rather than resetting it to the generated figure.
        await SetUnspentDirectedPointsAsync(0);

        await _process.ProcessAsync(StartTurnContext());

        PlayerResearchState state = await ReloadStateAsync();

        Assert.That(state.UnspentDirectedPoints, Is.Zero);
    }

    [Test, Description("The next Round generates again, so the stamp gates a repeat rather than the grant itself.")]
    public async Task ShouldGenerateAgainInTheFollowingRoundAsync()
    {
        await AddSettlementAsync(settlementId: 1, structureInstanceId: 1, population: 100);

        await _process.ProcessAsync(StartTurnContext());
        await SetUnspentDirectedPointsAsync(0);
        await _process.ProcessAsync(StartTurnContext(_CURRENT_ROUND_NUMBER + 1));

        PlayerResearchState state = await ReloadStateAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(state.UnspentDirectedPoints, Is.EqualTo(1));
            Assert.That(state.LastResearchGeneratedOnRound, Is.EqualTo(_CURRENT_ROUND_NUMBER + 1));
        }
    }

    [Test, Description("A Player with no research state row is skipped rather than failing the Turn, because the pipeline is fail-fast and a Game set up before the Technology schema existed must still be playable.")]
    public async Task ShouldSkipAPlayerWithNoResearchStateAsync()
    {
        await AddSettlementAsync(settlementId: 1, structureInstanceId: 1, population: 500, ownedByPlayerId: _OTHER_PLAYER_ID, foundingPlayerId: _OTHER_PLAYER_ID);

        string error = await _process.ProcessAsync(StartTurnContext(playerId: _OTHER_PLAYER_ID));

        Assert.That(error, Is.Empty);
    }

    [Test, Description("A start-of-Turn context carrying no Player Id fails the pipeline rather than generating points for every Player in the Game.")]
    public async Task ShouldFailWhenTheContextCarriesNoPlayerAsync()
    {
        await AddSettlementAsync(settlementId: 1, structureInstanceId: 1, population: 500);

        var contextWithNoPlayer = new TurnPipelineContextData(TurnPipelineType.StartCampaignTurn, _GAME_ID, _CURRENT_ROUND_NUMBER);

        string error = await _process.ProcessAsync(contextWithNoPlayer);

        PlayerResearchState state = await ReloadStateAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(error, Is.Not.Empty);
            Assert.That(state.UnspentDirectedPoints, Is.Zero, "Nothing was generated.");
        }
    }

    // ── arrangement ───────────────────────────────────────────────────────────

    private static TurnPipelineContextData StartTurnContext(int roundNumber = _CURRENT_ROUND_NUMBER, int playerId = _PLAYER_ID) =>
        new(TurnPipelineType.StartCampaignTurn, _GAME_ID, playerId, roundNumber);

    /// <summary>Adds a template and its compiled snapshot, the snapshot carrying the capability tags the process matches on.</summary>
    private static void AddTemplate(AscendantEmpireContext context, int templateId, string displayName, string[] capabilityTagCodes)
    {
        string mapImagePath = $"{displayName.ToLowerInvariant()}.png";

        context.UnitTemplates.Add(new UnitTemplate
        {
            Id = templateId,
            DisplayName = displayName,
            MapImagePath = mapImagePath,
            BodyComponentId = 1
        });

        context.CompiledUnitTemplates.Add(new CompiledUnitTemplate
        {
            UnitTemplateId = templateId,
            DisplayName = displayName,
            MapImagePath = mapImagePath,
            BodyComponentId = 1,
            Capabilities = [.. capabilityTagCodes.Select(tagCode => new CompiledUnitTemplateCapability
            {
                TagCode = tagCode,
                DisplayName = tagCode
            })]
        });
    }

    /// <summary>Adds a settlement and the Structure it sits on, defaulting to one the acting Player founded, holds and has finished building.</summary>
    private async Task AddSettlementAsync(
        int settlementId,
        int structureInstanceId,
        int population,
        int gameId = _GAME_ID,
        int? ownedByPlayerId = null,
        int? foundingPlayerId = null,
        int? completedOnTurnId = 1,
        int? removedOnTurnId = null)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        context.StructureInstances.Add(new StructureInstance
        {
            Id = structureInstanceId,
            GameId = gameId,
            CampaignMapTileId = _TILE_ID,
            StructureId = 1,
            SlotType = TileSlotType.Primary,
            Position = PositionOnTile.WholeTile,
            OwnedByPlayerId = ownedByPlayerId ?? _PLAYER_ID,
            CreatedOnTurnId = 1,
            CompletedOnTurnId = completedOnTurnId,
            RemovedOnTurnId = removedOnTurnId
        });

        context.Settlements.Add(new Settlement
        {
            Id = settlementId,
            GameId = gameId,
            StructureInstanceId = structureInstanceId,
            FoundingPlayerId = foundingPlayerId ?? ownedByPlayerId ?? _PLAYER_ID,
            Name = $"Settlement {settlementId}",
            Tier = SettlementTier.Village,
            Population = population,
            FoundedOnRoundNumber = 1,
            FoundedOnTurnId = 1
        });

        await context.SaveChangesAsync();
    }

    /// <summary>Adds a Force, defaulting to the acting Player's own full-strength settlers.</summary>
    private async Task AddForceAsync(
        int unitInstanceId,
        int healthyCount,
        int injuredCount = 0,
        int gameId = _GAME_ID,
        int playerId = _PLAYER_ID,
        int unitTemplateId = _SETTLER_TEMPLATE_ID)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        context.UnitInstances.Add(new UnitInstance
        {
            Id = unitInstanceId,
            GameId = gameId,
            PlayerId = playerId,
            CampaignMapTileId = _TILE_ID,
            UnitTemplateId = unitTemplateId,
            InitialStrength = healthyCount + injuredCount,
            HealthyCount = healthyCount,
            InjuredCount = injuredCount
        });

        await context.SaveChangesAsync();
    }

    private async Task SetUnspentDirectedPointsAsync(int unspentDirectedPoints)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        PlayerResearchState state = await context.PlayerResearchStates.SingleAsync(row => row.PlayerId == _PLAYER_ID);
        state.UnspentDirectedPoints = unspentDirectedPoints;

        await context.SaveChangesAsync();
    }

    private async Task<PlayerResearchState> ReloadStateAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        return await context.PlayerResearchStates.SingleAsync(row => row.PlayerId == _PLAYER_ID);
    }
}
