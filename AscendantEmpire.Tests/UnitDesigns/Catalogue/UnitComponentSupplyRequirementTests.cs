using AscendantEmpire.Application.Features.UnitComponents.Services;
using AscendantEmpire.BackendContracts.UnitDesigns.BulkUpsertUnitComponents;
using AscendantEmpire.Domain.Entities.Assets;
using AscendantEmpire.Domain.Enums.Assets;

using static AscendantEmpire.Tests.UnitDesigns.Shared.UnitDesignPayloads;

namespace AscendantEmpire.Tests.UnitDesigns.Catalogue;

/// <summary>
/// Pins the rules a component's supply requirements must satisfy before anything is written.
/// </summary>
/// <remarks>
/// No database: the rules are pure functions of the payload and the sets that exist. That the create and update endpoints actually apply them, and
/// that the rows reconcile, is covered end to end in <see cref="UpsertUnitComponentHandlerTests"/>, which has a real engine.
/// </remarks>
internal sealed class UnitComponentSupplyRequirementTests
{
    private const string _HUMAN_FOOD = "human-food";
    private const string _IRON_RATIONS = "iron-rations";
    private const string _MOUNT_FODDER = "mount-fodder";
    private const string _HUMAN_MEDICAL = "human-medical";

    #region Placement

    [Test, Description("A requirement on a Component that is not a Body is refused, naming the component and its type.")]
    public void ShouldRefuseWhenRequirementIsOnANonBodyComponent()
    {
        UnitComponentDto weapon = BuildWeapon("test_weapon") with
        {
            SupplyRequirements = [Requirement(_HUMAN_FOOD)]
        };

        List<string> errors = Validate(weapon, (_HUMAN_FOOD, SupplyType.Food));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(errors, Has.Some.Contains("test_weapon"));
            Assert.That(errors, Has.Some.Contains("Weapon"));
        }
    }

    [Test, Description("A Body carrying a well-formed requirement is accepted, so the rules here cannot be passing for the wrong reason.")]
    public void ShouldAcceptWhenRequirementIsOnABodyComponent()
    {
        UnitComponentDto body = BuildBodyWithSlots() with
        {
            SupplyRequirements = [Requirement(_HUMAN_FOOD), Requirement(_MOUNT_FODDER)]
        };

        List<string> errors = Validate(body, (_HUMAN_FOOD, SupplyType.Food), (_MOUNT_FODDER, SupplyType.Fuel));

        Assert.That(errors, Is.Empty);
    }

    #endregion

    #region References

    [Test, Description("An unknown SupplySet code fails the save and names it.")]
    public void ShouldRefuseWhenSupplySetCodeIsUnknown()
    {
        UnitComponentDto body = BuildBodyWithSlots() with
        {
            SupplyRequirements = [Requirement("NoSuchSet")]
        };

        List<string> errors = Validate(body, (_HUMAN_FOOD, SupplyType.Food));

        Assert.That(errors, Has.Some.Contains("NoSuchSet"));
    }

    #endregion

    #region Supply type discipline

    [Test, Description("The same SupplySet twice on one Component is refused, since it would double the need.")]
    public void ShouldRefuseWhenOneSupplySetIsNamedTwice()
    {
        UnitComponentDto body = BuildBodyWithSlots() with
        {
            SupplyRequirements = [Requirement(_HUMAN_FOOD), Requirement(_HUMAN_FOOD, quantity: 2_000)]
        };

        List<string> errors = Validate(body, (_HUMAN_FOOD, SupplyType.Food));

        Assert.That(errors, Has.Some.Contains(_HUMAN_FOOD));
    }

    [Test, Description("Two different Food sets on one Body are refused, a Force owing one ration and not a choice of them.")]
    public void ShouldRefuseWhenBodyCarriesTwoFoodSets()
    {
        UnitComponentDto body = BuildBodyWithSlots() with
        {
            SupplyRequirements = [Requirement(_HUMAN_FOOD), Requirement(_IRON_RATIONS)]
        };

        List<string> errors = Validate(body, (_HUMAN_FOOD, SupplyType.Food), (_IRON_RATIONS, SupplyType.Food));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(errors, Has.Some.Contains("Food"));
            Assert.That(errors, Has.Some.Contains(_HUMAN_FOOD));
            Assert.That(errors, Has.Some.Contains(_IRON_RATIONS));
        }
    }

    [Test, Description("One set of each type on one Body is allowed, which is what a mounted body carrying medical supplies needs.")]
    public void ShouldAcceptWhenBodyCarriesOneSetOfEachType()
    {
        UnitComponentDto body = BuildBodyWithSlots() with
        {
            SupplyRequirements = [Requirement(_HUMAN_FOOD), Requirement(_MOUNT_FODDER), Requirement(_HUMAN_MEDICAL)]
        };

        List<string> errors = Validate(
            body,
            (_HUMAN_FOOD, SupplyType.Food),
            (_MOUNT_FODDER, SupplyType.Fuel),
            (_HUMAN_MEDICAL, SupplyType.Repair));

        Assert.That(errors, Is.Empty);
    }

    #endregion

    #region Quantity

    [TestCase(0L, Description = "A requirement for nothing is not a requirement, so zero is refused.")]
    [TestCase(-1_000L, Description = "A negative quantity is refused.")]
    public void ShouldRefuseWhenQuantityIsNotPositive(long quantity)
    {
        UnitComponentDto body = BuildBodyWithSlots() with
        {
            SupplyRequirements = [Requirement(_HUMAN_FOOD, quantity)]
        };

        List<string> errors = Validate(body, (_HUMAN_FOOD, SupplyType.Food));

        Assert.That(errors, Has.Some.Contains("Quantity"));
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Validates the component against sets named by code and supply type, which is all the rules read of a set.
    /// </summary>
    private static List<string> Validate(UnitComponentDto component, params (string Code, SupplyType Type)[] supplySets)
    {
        List<string> errors = [];

        Dictionary<string, SupplySet> supplySetsByCode = supplySets.ToDictionary(
            set => set.Code,
            set => new SupplySet { Code = set.Code, DisplayName = set.Code, SupplyType = set.Type });

        UnitComponentReconciler.ValidateSupplyRequirements(component, supplySetsByCode, errors);

        return errors;
    }

    private static UnitComponentSupplyRequirementDto Requirement(string supplySetCode, long quantity = 1_000)
    {
        return new UnitComponentSupplyRequirementDto
        {
            SupplySetCode = supplySetCode,
            Quantity = quantity
        };
    }

    #endregion
}
