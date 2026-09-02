using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Turns.Services.EndCampaignTurnPossibleActions;
using AscendantEmpire.Application.Features.Turns.Services.EndCampaignTurnPossibleActions.Checks;
using AscendantEmpire.Application.Features.Turns.Services.TurnPipeline;
using AscendantEmpire.Application.Features.Turns.Services.TurnPipeline.Campaign.EndRound;
using AscendantEmpire.Application.Features.Turns.Services.TurnPipeline.Campaign.EndTurn;
using AscendantEmpire.Application.Features.Turns.Services.TurnPipeline.Campaign.StartTurn;
using AscendantEmpire.Domain.Enums.Turns;

using Microsoft.EntityFrameworkCore;

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
        // Two of the types below take a context factory, but none of them touch it to answer ExecutionOrder or its name.
        _dbFactory = Substitute.For<IDbContextFactory<AscendantEmpireContext>>();
    }

    // ── the pipelines under test ──────────────────────────────────────────────

    private static IPipelineProcess[] EndCampaignTurnProcesses() =>
        [new Gathering(), new BuildingStructure(), new Fortifying(), new EvaluateForceOnHostileTerrain()];

    private IPipelineProcess[] EndCampaignRoundProcesses() =>
        [new HealTroops(_dbFactory), new EvaluateVictoryConditions()];

    private static IPipelineProcess[] StartCampaignTurnProcesses() =>
        [new AlertSentries(), new CheckUnitSupply()];

    private IPossibleActionCheck[] PossibleActionChecks() =>
        [new CampaignRemainingMovementCheck(_dbFactory), new BattleRemainingMovementCheck(_dbFactory), new ResearchSelectionCheck(), new TradeExpiryCheck()];

    /// <summary>Sorts a pipeline the way <c>TurnServicePipeline</c> does, and returns the names in the order they would run.</summary>
    private static string[] RunOrderOf(IPipelineProcess[] processes) => [.. processes.OrderBy(p => p.ExecutionOrder).Select(p => p.ItemName)];

    // ── declared run order ────────────────────────────────────────────────────

    [Test, Description("Ending a turn gathers, builds and fortifies before attrition is judged on what the Force did with the turn.")]
    public void ShouldRunEndCampaignTurnProcessesInDeclaredOrder()
    {
        string[] expected = ["Gathering", "Building Structure", "Fortifying", "Evaluate Force on Hostile Terrain"];

        Assert.That(RunOrderOf(EndCampaignTurnProcesses()), Is.EqualTo(expected));
    }

    [Test, Description("Victory is judged last, on the state the rest of the end-of-round processing leaves behind.")]
    public void ShouldRunEndCampaignRoundProcessesInDeclaredOrder()
    {
        string[] expected = ["Heal Troops", "Evaluate Victory Conditions"];

        Assert.That(RunOrderOf(EndCampaignRoundProcesses()), Is.EqualTo(expected));
    }

    [Test, Description("Starting a turn reports what the sentries see before supply is reckoned against the Forces in the field.")]
    public void ShouldRunStartCampaignTurnProcessesInDeclaredOrder()
    {
        string[] expected = ["Alert Sentries", "Check Unit Supply"];

        Assert.That(RunOrderOf(StartCampaignTurnProcesses()), Is.EqualTo(expected));
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
