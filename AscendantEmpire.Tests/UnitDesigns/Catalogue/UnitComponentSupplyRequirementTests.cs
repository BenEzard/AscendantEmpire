using AscendantEmpire.Application.Features.UnitComponents.Services;
using AscendantEmpire.BackendContracts.UnitDesigns.BulkUpsertUnitComponents;

using static AscendantEmpire.Tests.UnitDesigns.Shared.UnitDesignPayloads;

namespace AscendantEmpire.Tests.UnitDesigns.Catalogue;

/// <summary>
/// Pins the rules a component's supply requirements must satisfy before anything is written.
/// </summary>
/// <remarks>
/// No database: the rules are pure functions of the payload and the set codes that exist. That the create and update endpoints actually apply them, and
/// that the rows reconcile, is covered end to end in <see cref="UpsertUnitComponentHandlerTests"/>, which has a real engine.
/// </remarks>
internal sealed class UnitComponentSupplyRequirementTests
{
    private const string _HUMAN_FOOD = "human-food";
    private const string _MOUNT_FODDER = "mount-fodder";

    #region Placement

    [Test, Description("A requirement on a Component that is not a Body is refused, naming the component and its type.")]
    public void Reconcile_RejectsSupplyRequirementOnNonBodyComponent()
    {
        UnitComponentDto weapon = BuildWeapon("test_weapon") with
        {
            SupplyRequirements = [Requirement(_HUMAN_FOOD)]
        };

        List<string> errors = Validate(weapon, [_HUMAN_FOOD]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(errors, Has.Some.Contains("test_weapon"));
            Assert.That(errors, Has.Some.Contains("Weapon"));
        }
    }

    [Test, Description("A Body carrying a well-formed requirement is accepted, so the rules here cannot be passing for the wrong reason.")]
    public void Reconcile_AcceptsRequirementOnBodyComponent()
    {
        UnitComponentDto body = BuildBodyWithSlots() with
        {
            SupplyRequirements = [Requirement(_HUMAN_FOOD), Requirement(_MOUNT_FODDER)]
        };

        List<string> errors = Validate(body, [_HUMAN_FOOD, _MOUNT_FODDER]);

        Assert.That(errors, Is.Empty);
    }

    #endregion

    #region References

    [Test, Description("An unknown SupplySet code fails the save and names it.")]
    public void Reconcile_RejectsUnknownSupplySetCode()
    {
        UnitComponentDto body = BuildBodyWithSlots() with
        {
            SupplyRequirements = [Requirement("NoSuchSet")]
        };

        List<string> errors = Validate(body, [_HUMAN_FOOD]);

        Assert.That(errors, Has.Some.Contains("NoSuchSet"));
    }

    [Test, Description("The same SupplySet twice on one Component is refused, since it would double the need.")]
    public void Reconcile_RejectsDuplicateSupplySetOnOneComponent()
    {
        UnitComponentDto body = BuildBodyWithSlots() with
        {
            SupplyRequirements = [Requirement(_HUMAN_FOOD), Requirement(_HUMAN_FOOD, quantity: 2_000)]
        };

        List<string> errors = Validate(body, [_HUMAN_FOOD]);

        Assert.That(errors, Has.Some.Contains(_HUMAN_FOOD));
    }

    #endregion

    #region Quantity

    [TestCase(0L, Description = "A requirement for nothing is not a requirement, so zero is refused.")]
    [TestCase(-1_000L, Description = "A negative quantity is refused.")]
    public void Reconcile_RejectsNonPositiveQuantity(long quantity)
    {
        UnitComponentDto body = BuildBodyWithSlots() with
        {
            SupplyRequirements = [Requirement(_HUMAN_FOOD, quantity)]
        };

        List<string> errors = Validate(body, [_HUMAN_FOOD]);

        Assert.That(errors, Has.Some.Contains("Quantity"));
    }

    #endregion

    #region Helpers

    private static List<string> Validate(UnitComponentDto component, IReadOnlyCollection<string> knownSupplySetCodes)
    {
        List<string> errors = [];

        UnitComponentReconciler.ValidateSupplyRequirements(component, knownSupplySetCodes, errors);

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
