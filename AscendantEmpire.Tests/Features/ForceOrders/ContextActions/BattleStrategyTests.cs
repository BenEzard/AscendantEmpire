using AscendantEmpire.Application.Features.ForceOrders.ContextActions;
using AscendantEmpire.Application.Features.ForceOrders.ContextActions.Strategies;
using AscendantEmpire.Domain.Enums.Forces;
using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.Tests.Features.ForceOrders.ContextActions;

/// <summary>
/// Pure unit tests for <see cref="BattleStrategy"/> — no database required, because the strategy reads only the context handed to it.
/// </summary>
[TestFixture]
internal class BattleStrategyTests
{
    private BattleStrategy _strategy = null!;

    [SetUp]
    public void SetUp()
    {
        _strategy = new BattleStrategy();
    }

    /// <summary>Builds the selected Force, defaulting to one that is not in battle.</summary>
    private static ForceActionForce Force(bool isInBattle = false, long campaignMovementRemaining = 1_000, int healthyCount = 50)
    {
        return new ForceActionForce
        {
            UnitInstanceId = 5,
            PlayerId = 1,
            CampaignMapTileId = 112,
            DisplayName = "Warrior",
            HealthyCount = healthyCount,
            InjuredCount = 0,
            CampaignMovementRemaining = campaignMovementRemaining,
            CampaignMovementPoints = 1_000,
            Organisation = null,
            CapabilityTagCodes = [],
            TileIsWater = false,
            TileGeographyType = CampaignMapGeographyType.Flat,
            IsInBattle = isInBattle
        };
    }

    private static ForceActionContext Context(ForceActionForce force)
    {
        return new ForceActionContext { SelectedForce = force, OtherForcesOnTile = [] };
    }

    [Test, Description("The action type doubles as display order, so a wrong one would move the button as well as mislabel it.")]
    public void ShouldIdentifyItselfAsTheBattleAction()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(_strategy.ActionType, Is.EqualTo(ForceActionType.Battle));
            Assert.That(_strategy.Label, Is.EqualTo("Battle"));
        }
    }

    [Test, Description("A Force that is not fighting has no battle to open, so it is hidden the button rather than shown one it can never press.")]
    public void ShouldHideTheActionWhenTheForceIsNotInBattle()
    {
        ForceActionAvailability? result = _strategy.Evaluate(Context(Force(isInBattle: false)));

        Assert.That(result, Is.Null);
    }

    [Test, Description("A Force in battle can always look at its battle, whatever its movement, so spent movement must not grey the button out.")]
    public void ShouldOfferTheActionToAForceInBattleEvenWithNoMovementLeft()
    {
        ForceActionAvailability? result = _strategy.Evaluate(Context(Force(isInBattle: true, campaignMovementRemaining: -500)));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.Not.Null);
            Assert.That(result!.IsEnabled, Is.True);
            Assert.That(result.DisabledReasons, Is.Empty);
        }
    }
}
