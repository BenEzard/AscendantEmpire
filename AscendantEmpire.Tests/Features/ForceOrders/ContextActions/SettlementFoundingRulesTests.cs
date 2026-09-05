using AscendantEmpire.Application.Features.ForceOrders.ContextActions;
using AscendantEmpire.Application.Features.ForceOrders.ContextActions.Strategies;
using AscendantEmpire.Domain.Enums.Maps;

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
        CampaignMapGeographyType tileGeographyType = CampaignMapGeographyType.Flat)
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
            TileGeographyType = tileGeographyType
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

        Assert.That(reasons, Is.EqualTo(new[] { "Settler is on water, and a camp must be founded on land." }));
    }

    [Test, Description("Mountains are named as unsettleable by the ticket, and the rule must hold even though nothing can currently stand on one to test it in play.")]
    public void ShouldBlockWhenTheTileIsMountainous()
    {
        List<string> reasons = Collect(Force(tileGeographyType: CampaignMapGeographyType.Mountainous));

        Assert.That(reasons, Is.EqualTo(new[] { "Settler is in mountainous terrain, which cannot be settled." }));
    }

    [Test, Description("Hills are elevated but explicitly not excluded, so widening the mountain rule to any raised ground would refuse a legal site.")]
    public void ShouldAllowWhenTheTileIsMerelyHilly()
    {
        Assert.That(Collect(Force(tileGeographyType: CampaignMapGeographyType.Hilly)), Is.Empty);
    }

    [Test, Description("Founding costs movement, so a Force that has spent its whole allowance must wait for the next turn.")]
    public void ShouldBlockWhenNoMovementRemains()
    {
        List<string> reasons = Collect(Force(campaignMovementRemaining: 0));

        Assert.That(reasons, Is.EqualTo(new[] { "Settler has no campaign movement left this turn." }));
    }

    [Test, Description("Movement can legitimately go negative since entering a tile costs half but spends all, and a Force in that debt has less than nothing left to spend.")]
    public void ShouldBlockWhenMovementIsInDebt()
    {
        List<string> reasons = Collect(Force(campaignMovementRemaining: -500));

        Assert.That(reasons, Is.EqualTo(new[] { "Settler has no campaign movement left this turn." }));
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

        Assert.That(reasons, Is.EqualTo(new[] { "Settler has 24 troops, and a camp needs at least 25." }));
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
            tileGeographyType: CampaignMapGeographyType.Mountainous));

        Assert.That(reasons, Is.EqualTo(new[]
        {
            "Settler is on water, and a camp must be founded on land.",
            "Settler is in mountainous terrain, which cannot be settled.",
            "Settler has no campaign movement left this turn.",
            "Settler has 1 troops, and a camp needs at least 25."
        }));
    }
}
