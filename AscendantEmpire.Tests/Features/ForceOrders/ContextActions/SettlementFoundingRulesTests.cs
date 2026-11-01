using AscendantEmpire.Application.Features.ForceOrders.ContextActions;
using AscendantEmpire.Application.Features.ForceOrders.ContextActions.Strategies;

namespace AscendantEmpire.Tests.Features.ForceOrders.ContextActions;

/// <summary>
/// Pure unit tests for <see cref="SettlementFoundingRules"/> — the terrain, movement and size rules both settlement actions share.
/// </summary>
/// <remarks>
/// The boundaries are what these pin. Each rule is exercised once through the shared collector here, so the strategy fixtures can concentrate on
/// what actually differs between them: the capability that unlocks the action and the troop threshold it demands.
/// </remarks>
[TestFixture]
internal class SettlementFoundingRulesTests
{
    private const int _MINIMUM_TROOPS = 25;
    private const long _FULL_CAMPAIGN_MOVEMENT = 1_000;

    /// <summary>Builds a Force that passes every shared rule, so a test only has to spoil the one it is about.</summary>
    private static ForceActionForce Force(
        int healthyCount = _MINIMUM_TROOPS,
        long campaignMovementRemaining = _FULL_CAMPAIGN_MOVEMENT,
        bool tileIsWater = false,
        bool tileGeographyIsTraversable = true,
        string tileGeographyName = "Flat")
    {
        return new ForceActionForce
        {
            UnitInstanceId = 5,
            PlayerId = 1,
            CampaignMapTileId = 112,
            DisplayName = "Settler",
            HealthyCount = healthyCount,
            InjuredCount = 0,
            CampaignMovementRemaining = campaignMovementRemaining,
            CampaignMovementPoints = _FULL_CAMPAIGN_MOVEMENT,
            Organisation = null,
            CapabilityTagCodes = ["found-camp"],
            TileIsWater = tileIsWater,
            TileGeographyIsTraversable = tileGeographyIsTraversable,
            TileGeographyName = tileGeographyName,
            IsInBattle = false
        };
    }

    private static List<string> Collect(ForceActionForce force)
    {
        return SettlementFoundingRules.CollectBlockingReasons(force, "camp", _MINIMUM_TROOPS);
    }

    [Test, Description("A Force meeting every condition must produce no reasons at all, because the caller turns an empty list into an enabled button.")]
    public void ShouldFindNoReasonsWhenEveryConditionIsMet()
    {
        Assert.That(Collect(Force()), Is.Empty);
    }

    [Test, Description("Settling is a thing done to land, so a Force floating on a water biome is refused whatever else is true of it.")]
    public void ShouldBlockWhenTheTileIsWater()
    {
        List<string> reasons = Collect(Force(tileIsWater: true));

        Assert.That(reasons, Is.EqualTo(["Settler is on water, and a camp must be founded on land."]));
    }

    [Test, Description("Mountains are named as unsettleable by the ticket, and the reason names the terrain so the player knows to move rather than to reinforce.")]
    public void ShouldBlockWhenTheTileIsMountainous()
    {
        List<string> reasons = Collect(Force(tileGeographyIsTraversable: false, tileGeographyName: "Mountainous"));

        Assert.That(reasons, Is.EqualTo(["Settler is in Mountainous terrain, which cannot be settled."]));
    }

    [Test, Description("Any ground the catalogue marks untraversable is unsettleable, so clearing the flag on a geography that is not Mountainous is refused without the rule being touched.")]
    public void ShouldBlockWhenTheGroundIsUntraversable()
    {
        // Hilly rather than Mountainous on purpose. Mountainous is the only geography the seed ships untraversable, so a rule written against the
        // enum satisfies every test that uses it and still refuses nothing the day the Geography Manager clears the flag on another one.
        List<string> reasons = Collect(Force(tileGeographyIsTraversable: false, tileGeographyName: "Hilly"));

        Assert.That(reasons, Is.EqualTo(["Settler is in Hilly terrain, which cannot be settled."]));
    }

    [Test, Description("Elevation is not what the rule reads, so traversable high ground stays a legal site however steep the catalogue says it is.")]
    public void ShouldAllowWhenTheGroundIsTraversable()
    {
        ForceActionForce force = Force(tileGeographyIsTraversable: true, tileGeographyName: "Hilly");

        Assert.That(Collect(force), Is.Empty);
    }

    [Test, Description("Founding costs movement, so a Force that has spent its whole allowance must wait for the next turn.")]
    public void ShouldBlockWhenNoMovementRemains()
    {
        List<string> reasons = Collect(Force(campaignMovementRemaining: 0));

        Assert.That(reasons, Is.EqualTo(["Settler has no campaign movement left this turn."]));
    }

    [Test, Description("Movement can legitimately go negative since entering a tile costs half but spends all, and a Force in that debt has less than nothing left to spend.")]
    public void ShouldBlockWhenMovementIsInDebt()
    {
        List<string> reasons = Collect(Force(campaignMovementRemaining: -500));

        Assert.That(reasons, Is.EqualTo(["Settler has no campaign movement left this turn."]));
    }

    [Test, Description("A fraction of a point is still movement left, so the rule is 'any remaining' rather than 'a whole point'.")]
    public void ShouldAllowWhenOnlyAFractionOfAMovementPointRemains()
    {
        Assert.That(Collect(Force(campaignMovementRemaining: 1)), Is.Empty);
    }

    [Test, Description("The threshold is a minimum rather than a strict greater-than, so a Force sitting exactly on it qualifies.")]
    public void ShouldAllowWhenTheForceIsExactlyAtTheMinimumSize()
    {
        Assert.That(Collect(Force(healthyCount: _MINIMUM_TROOPS)), Is.Empty);
    }

    [Test, Description("One soldier short is short, and the reason names both numbers so the player knows how far off they are.")]
    public void ShouldBlockWhenTheForceIsOneShortOfTheMinimumSize()
    {
        List<string> reasons = Collect(Force(healthyCount: _MINIMUM_TROOPS - 1));

        Assert.That(reasons, Is.EqualTo(["Settler has 24 troops, and a camp needs at least 25."]));
    }

    [Test, Description("Injured soldiers are still soldiers, so a Force at the threshold only because some of it is hurt still qualifies.")]
    public void ShouldCountInjuredTroopsTowardTheMinimumSize()
    {
        ForceActionForce force = Force(healthyCount: 20) with { InjuredCount = 5 };

        Assert.That(Collect(force), Is.Empty);
    }

    [Test, Description("Every failing rule is reported at once, so a player fixing one problem does not merely uncover the next.")]
    public void ShouldReportEveryFailingRuleTogetherInAFixedOrder()
    {
        List<string> reasons = Collect(Force(
            healthyCount: 1,
            campaignMovementRemaining: 0,
            tileIsWater: true,
            tileGeographyIsTraversable: false,
            tileGeographyName: "Mountainous"));

        Assert.That(reasons, Is.EqualTo(
        [
            "Settler is on water, and a camp must be founded on land.",
            "Settler is in Mountainous terrain, which cannot be settled.",
            "Settler has no campaign movement left this turn.",
            "Settler has 1 troops, and a camp needs at least 25."
        ]));
    }
}
