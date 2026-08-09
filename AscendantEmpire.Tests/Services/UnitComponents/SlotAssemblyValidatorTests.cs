using AscendantEmpire.Application.Features.UnitComponents.Services;
using AscendantEmpire.Domain.Entities.UnitDesigns;
using AscendantEmpire.Domain.Enums.UnitDesigns;

namespace AscendantEmpire.Tests.Services.UnitComponents;

/// <summary>
/// Pure unit tests for <see cref="SlotAssemblyValidator"/> — no database required. Components are wired
/// by navigation (no real ids needed beyond the fitted-row ids). Only bodies expose slots, so every
/// fitted component sits directly in one of the template body's slots.
/// </summary>
[TestFixture]
internal class SlotAssemblyValidatorTests
{
    private const int BODY_COMPONENT_ID = 1;
    private const int OTHER_BODY_COMPONENT_ID = 2;
    private const int WEAPON_COMPONENT_ID = 3;
    private const int GUN_COMPONENT_ID = 4;

    /// <summary>Builds a slot owned by the given body, with optional acceptance rules.</summary>
    private static UnitComponentSlot Slot(int id, int ownerBodyId, params UnitComponentSlotRule[] rules)
    {
        return new UnitComponentSlot { Id = id, Code = $"SLOT_{id}", UnitComponentBodyId = ownerBodyId, Rules = rules };
    }

    private static UnitComponentSlotRule TypeEquals(ComponentType type)
    {
        return new UnitComponentSlotRule
        {
            RuleType = SlotRuleType.ComponentType,
            Operator = SlotRuleOperator.Equals,
            Value = type.ToString(),
        };
    }

    /// <summary>Builds a fitted-component row, wiring its slot and the component it carries.</summary>
    private static UnitTemplateComponent Fitted(
        int id,
        UnitComponentSlot slot,
        int componentId,
        ComponentType type,
        string code,
        params string[] tagCodes)
    {
        var component = new UnitComponent
        {
            Id = componentId,
            Code = code,
            DisplayName = code,
            ComponentType = type,
            Tags = [.. tagCodes.Select(c => new UnitComponentTag { Tag = new Tag { Code = c, DisplayName = c, TagType = TagType.Ability } })],
        };

        return new UnitTemplateComponent
        {
            Id = id,
            UnitComponentSlotId = slot.Id,
            UnitComponentSlot = slot,
            UnitComponentId = componentId,
            UnitComponent = component,
        };
    }

    [Test, Description("A valid assembly (a weapon fitted into a matching body slot) passes.")]
    public void ValidAssemblyIsAccepted()
    {
        UnitComponentSlot bodySlot = Slot(10, BODY_COMPONENT_ID, TypeEquals(ComponentType.Weapon));

        UnitTemplateComponent gun = Fitted(100, bodySlot, WEAPON_COMPONENT_ID, ComponentType.Weapon, "gun");

        List<string> errors = SlotAssemblyValidator.Validate(BODY_COMPONENT_ID, [gun]);

        Assert.That(errors, Is.Empty);
    }

    [Test, Description("A slot that belongs to a different body than the template's is rejected.")]
    public void WrongOwnerIsRejected()
    {
        // The slot belongs to another body, but is used in this template — mismatch.
        UnitComponentSlot foreignSlot = Slot(20, OTHER_BODY_COMPONENT_ID, TypeEquals(ComponentType.Weapon));

        UnitTemplateComponent gun = Fitted(100, foreignSlot, GUN_COMPONENT_ID, ComponentType.Weapon, "gun");

        List<string> errors = SlotAssemblyValidator.Validate(BODY_COMPONENT_ID, [gun]);

        Assert.That(errors, Has.Count.EqualTo(1));
        Assert.That(errors[0], Does.Contain("not the template's body"));
    }

    [Test, Description("A component that fails the slot's acceptance rules is rejected.")]
    public void RuleViolationIsRejected()
    {
        // Body slot only accepts Weapons, but an Armour is fitted into it.
        UnitComponentSlot bodySlot = Slot(10, BODY_COMPONENT_ID, TypeEquals(ComponentType.Weapon));

        UnitTemplateComponent armour = Fitted(100, bodySlot, WEAPON_COMPONENT_ID, ComponentType.Armour, "plate");

        List<string> errors = SlotAssemblyValidator.Validate(BODY_COMPONENT_ID, [armour]);

        Assert.That(errors, Has.Count.EqualTo(1));
        Assert.That(errors[0], Does.Contain("does not satisfy"));
    }

    [Test, Description("Two components filling the same body slot is rejected.")]
    public void DuplicateSlotIsRejected()
    {
        UnitComponentSlot bodySlot = Slot(10, BODY_COMPONENT_ID, TypeEquals(ComponentType.Weapon));

        UnitTemplateComponent first = Fitted(100, bodySlot, WEAPON_COMPONENT_ID, ComponentType.Weapon, "gun_a");
        UnitTemplateComponent second = Fitted(101, bodySlot, GUN_COMPONENT_ID, ComponentType.Weapon, "gun_b");

        List<string> errors = SlotAssemblyValidator.Validate(BODY_COMPONENT_ID, [first, second]);

        Assert.That(errors.Any(e => e.Contains("filled more than once")), Is.True);
    }
}
