using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Game.Services;
using AscendantEmpire.Application.Features.Intelligence.Services;
using AscendantEmpire.Application.Features.Maps.Biomes.Services;
using AscendantEmpire.Application.Features.Maps.CampaignMaps.Services;
using AscendantEmpire.Application.Features.Maps.Depletion.Services;
using AscendantEmpire.Application.Features.Structures.Services;
using AscendantEmpire.Application.Features.Technologies.Services;
using AscendantEmpire.Application.Features.Turns.Services.EndCampaignTurnPossibleActions;
using AscendantEmpire.Application.Features.Turns.Services.EndCampaignTurnPossibleActions.Checks;
using AscendantEmpire.Application.Features.Turns.Services.TurnPipeline;
using AscendantEmpire.Application.Features.Turns.Services.TurnPipeline.Campaign.EndRound;
using AscendantEmpire.Application.Features.Turns.Services.TurnPipeline.Campaign.EndTurn;
using AscendantEmpire.Application.Features.Turns.Services.TurnPipeline.Campaign.StartTurn;
using AscendantEmpire.Application.Features.Units.Services;
using AscendantEmpire.Domain.Enums.Turns;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

namespace AscendantEmpire.Tests.Features.Turns.Services;

/// <summary>
/// Pins the order the turn pipelines and the end-of-turn possible-action checks run in.
/// </summary>
/// <remarks>
/// <para>Every process is constructed directly and only its <c>ExecutionOrder</c> and name are read, so this fixture stands up no database at all.</para>
/// <para>These exist because the ordering is easy to break silently. Both sorts use <c>OrderBy</c>, which is stable, so two members sharing a value do
/// not fail — the sequence quietly falls through to whatever order the processes were registered in, and stops being decided by the order enums at
/// all. That is exactly how four end-of-turn processes came to share the value 100, and how two checks came to hard-code numbers that contradicted
/// <see cref="EndTurnPossibleActionOrder"/>.</para>
/// </remarks>
[TestFixture]
public class TurnPipelineExecutionOrderTests
{
    private IDbContextFactory<AscendantEmpireContext> _dbFactory = null!;

    [SetUp]
    public void SetUp()
    {
        // Several of the types below take a context factory, but none of them touch it to answer ExecutionOrder or its name.
        _dbFactory = Substitute.For<IDbContextFactory<AscendantEmpireContext>>();
    }

    // ── the pipelines under test ──────────────────────────────────────────────

    private static IPipelineProcess[] EndCampaignTurnProcesses() =>
    [
        new Gathering(
            Substitute.For<IDbContextFactory<AscendantEmpireContext>>(),
            Substitute.For<IPlayerSustenanceValueReader>(),
            Substitute.For<IUnderfedForceResolver>(),
            Substitute.For<ISightDataReader>(),
            Substitute.For<ICampaignRiverReader>(),
            NullLogger<Gathering>.Instance),
        new BuildingStructure(
            Substitute.For<IDbContextFactory<AscendantEmpireContext>>(),
            Substitute.For<IStructureLifecycleWriter>(),
            Substitute.For<IGameContextService>(),
            Substitute.For<ICampaignTerritoryReader>(),
            Substitute.For<ITileTerritoryService>(),
            NullLogger<BuildingStructure>.Instance),
        new Fortifying(),
        new EvaluateForceOnHostileTerrain(),
        new ApplyResearch(
            Substitute.For<IDbContextFactory<AscendantEmpireContext>>(),
            Substitute.For<IPlayerTechnologyAvailabilityService>(),
            NullLogger<ApplyResearch>.Instance),
        new RaiseSightingReports(
            Substitute.For<IForceSightingFoldService>(),
            Substitute.For<IStructureSightingFoldService>(),
            Substitute.For<ICombatSightingFoldService>(),
            NullLogger<RaiseSightingReports>.Instance)
    ];

    private IPipelineProcess[] EndCampaignRoundProcesses() =>
        [
            new HealTroops(_dbFactory),
            new RegenerateTileSustenance(
                _dbFactory,
                Substitute.For<IBiomeRegenerationValueReader>(),
                Substitute.For<ISightDataReader>(),
                Substitute.For<ICampaignRiverReader>(),
                NullLogger<RegenerateTileSustenance>.Instance),
            new ResolveTileTerritory(Substitute.For<ITileTerritoryService>(), NullLogger<ResolveTileTerritory>.Instance),
            new EvaluateVictoryConditions()
        ];

    private IPipelineProcess[] StartCampaignTurnProcesses() =>
        [
            new AlertSentries(),
            new CheckUnitSupply(),
            new ReplenishMovementPoints(_dbFactory, NullLogger<ReplenishMovementPoints>.Instance),
            new GenerateResearchPoints(_dbFactory, NullLogger<GenerateResearchPoints>.Instance)
        ];

    private IPossibleActionCheck[] PossibleActionChecks() =>
        [new CampaignRemainingMovementCheck(_dbFactory), new BattleRemainingMovementCheck(_dbFactory), new ResearchSelectionCheck(_dbFactory), new TradeExpiryCheck()];

    /// <summary>Sorts a pipeline the way <c>TurnServicePipeline</c> does, and returns the names in the order they would run.</summary>
    private static string[] RunOrderOf(IPipelineProcess[] processes) => [.. processes.OrderBy(p => p.ExecutionOrder).Select(p => p.ItemName)];

    // ── declared run order ────────────────────────────────────────────────────

    [Test, Description("Ending a turn gathers, builds and fortifies before attrition is judged on what the Force did with the turn, closes the research books, and writes down what others watched last of all.")]
    public void ShouldRunEndCampaignTurnProcessesInDeclaredOrder()
    {
        string[] expected =
        [
            "Gathering",
            "Building Structure",
            "Fortifying",
            "Evaluate Force on Hostile Terrain",
            "Apply Research",
            "Raise Sighting Reports"
        ];

        Assert.That(RunOrderOf(EndCampaignTurnProcesses()), Is.EqualTo(expected));
    }

    [Test, Description("Sightings are raised after attrition, so a Force seen and then destroyed by hostile terrain in the same Turn is still reported as seen.")]
    public void ShouldRaiseSightingReportsAfterAttrition()
    {
        int attritionOrder = (int)EndCampaignTurnProcessOrder.EvaluateForceOnHostileTerrain;
        int sightingsOrder = (int)EndCampaignTurnProcessOrder.RaiseSightingReports;

        // Stated separately from the sequence assertion above, which is rewritten every time a process is inserted. This constraint is the reason the two values are where they are.
        Assert.That(attritionOrder, Is.LessThan(sightingsOrder));
    }

    [Test, Description("The wounded recover, tiles regrow and borders settle before victory is judged, because victory is judged on the state the Round leaves behind.")]
    public void ShouldRunEndCampaignRoundProcessesInDeclaredOrder()
    {
        string[] expected = ["Heal Troops", "Regenerate Tile Sustenance", "Resolve Tile Territory", "Evaluate Victory Conditions"];

        Assert.That(RunOrderOf(EndCampaignRoundProcesses()), Is.EqualTo(expected));
    }

    [Test, Description("Starting a turn reports what the sentries see, then reckons supply, then hands out the movement allowance, and finally counts the Player's people into research points.")]
    public void ShouldRunStartCampaignTurnProcessesInDeclaredOrder()
    {
        string[] expected = ["Alert Sentries", "Check Unit Supply", "Replenish Movement Points", "Generate Research Points"];

        Assert.That(RunOrderOf(StartCampaignTurnProcesses()), Is.EqualTo(expected));
    }

    [Test, Description("Supply resolves before the movement allowance is granted: a fuel-requiring Unit with no fuel must receive no campaign movement at all, which cannot be decided once the allowance has already been handed out.")]
    public void ShouldReckonUnitSupplyBeforeReplenishingMovementPoints()
    {
        int supplyOrder = (int)StartCampaignTurnProcessOrder.CheckUnitSupply;
        int replenishOrder = (int)StartCampaignTurnProcessOrder.ReplenishMovementPoints;

        // Stated separately from the sequence assertion above, which is rewritten every time a process is inserted. This constraint is the reason the
        // two values are where they are, and it has to survive that rewriting.
        Assert.That(supplyOrder, Is.LessThan(replenishOrder),
            "The roadmap originally specified ReplenishMovementPoints at 5,000, ahead of supply. An allowance granted before supply is reckoned would have to be taken back.");
    }

    [Test, Description("Outstanding movement is reported before research and trade, as EndTurnPossibleActionOrder declares — not after, as hard-coded numbers once made it.")]
    public void ShouldRunPossibleActionChecksInDeclaredOrder()
    {
        string[] expected = ["Campaign Force Remaining Movement", "Battle Force Remaining Movement", "Research Selection", "Trade Deal Expiry"];

        string[] actual = [.. PossibleActionChecks().OrderBy(c => c.ExecutionOrder).Select(c => c.CheckName)];

        Assert.That(actual, Is.EqualTo(expected));
    }

    // ── no ties ───────────────────────────────────────────────────────────────

    [Test, Description("No two members of a pipeline share an execution order, which would silently hand the sequence back to registration order.")]
    public void ShouldGiveEveryMemberOfAPipelineADistinctExecutionOrder()
    {
        using (Assert.EnterMultipleScope())
        {
            AssertDistinct(EndCampaignTurnProcesses().Select(p => p.ExecutionOrder), "End Campaign Turn");
            AssertDistinct(EndCampaignRoundProcesses().Select(p => p.ExecutionOrder), "End Campaign Round");
            AssertDistinct(StartCampaignTurnProcesses().Select(p => p.ExecutionOrder), "Start Campaign Turn");
            AssertDistinct(PossibleActionChecks().Select(c => c.ExecutionOrder), "End Turn Possible Actions");
        }
    }

    private static void AssertDistinct(IEnumerable<int> executionOrders, string pipelineName)
    {
        int[] orders = [.. executionOrders];

        Assert.That(orders.Distinct().Count(), Is.EqualTo(orders.Length), $"The {pipelineName} pipeline has two members claiming the same execution order.");
    }
}
