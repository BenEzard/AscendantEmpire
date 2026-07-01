using AscendantEmpire.Application.Features.UnitComponents.Services;
using AscendantEmpire.Domain.Entities.UnitDesigns;
using AscendantEmpire.Domain.Enums.UnitDesigns;

namespace AscendantEmpire.Tests.Services.UnitComponents;

/// <summary>
/// Pure unit tests for <see cref="SlotAssemblyValidator"/> — no database required. Components are wired
/// by navigation (no real ids needed beyond the fitted-row ids the tree references).
/// </summary>
[TestFixture]
internal class SlotAssemblyValidatorTests
{
    private const int BODY_COMPONENT_ID = 1;
    private const int WEAPON_COMPONENT_ID = 2;
    private const int MOUNT_COMPONENT_ID = 3;
    private const int GUN_COMPONENT_ID = 4;

    /// <summary>Builds a slot owned by the given component, with optional acceptance rules.</summary>
    private static UnitComponentSlot Slot(int id, int ownerComponentId, params UnitComponentSlotRule[] rules)
    {
        return new UnitComponentSlot { Id = id, UnitComponentId = ownerComponentId, Rules = rules };
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
        int? parentId,
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
            ParentTemplateComponentId = parentId,
            UnitComponentSlotId = slot.Id,
            UnitComponentSlot = slot,
            UnitComponentId = componentId,
            UnitComponent = component,
        };
    }

    [Test, Description("A valid nested tree (weapon mount in a body slot, gun in the mount's slot) passes.")]
    public void ValidNestedTreeIsAccepted()
    {
        // Body slot accepts the mount; the mount's own slot accepts a weapon.
        UnitComponentSlot bodySlot = Slot(10, BODY_COMPONENT_ID, TypeEquals(ComponentType.Equipment));
        UnitComponentSlot mountSlot = Slot(20, MOUNT_COMPONENT_ID, TypeEquals(ComponentType.Weapon));

        UnitTemplateComponent mount = Fitted(100, null, bodySlot, MOUNT_COMPONENT_ID, ComponentType.Equipment, "weapon_mount");
        UnitTemplateComponent gun = Fitted(101, 100, mountSlot, GUN_COMPONENT_ID, ComponentType.Weapon, "gun");

        List<string> errors = SlotAssemblyValidator.Validate(BODY_COMPONENT_ID, [mount, gun]);

        Assert.That(errors, Is.Empty);
    }

    [Test, Description("A slot whose owner is not the component's parent (nor the body) is rejected.")]
    public void WrongOwnerIsRejected()
    {
        // The gun's slot belongs to the MOUNT, but the gun is fitted directly into the body (parent null),
        // so its owner should be the body — mismatch.
        UnitComponentSlot mountSlot = Slot(20, MOUNT_COMPONENT_ID, TypeEquals(ComponentType.Weapon));

        UnitTemplateComponent gun = Fitted(100, null, mountSlot, GUN_COMPONENT_ID, ComponentType.Weapon, "gun");

        List<string> errors = SlotAssemblyValidator.Validate(BODY_COMPONENT_ID, [gun]);

        Assert.That(errors, Has.Count.EqualTo(1));
        Assert.That(errors[0], Does.Contain("not its owner"));
    }

    [Test, Description("A component that fails the slot's acceptance rules is rejected.")]
    public void RuleViolationIsRejected()
    {
        // Body slot only accepts Weapons, but an Armour is fitted into it.
        UnitComponentSlot bodySlot = Slot(10, BODY_COMPONENT_ID, TypeEquals(ComponentType.Weapon));

        UnitTemplateComponent armour = Fitted(100, null, bodySlot, WEAPON_COMPONENT_ID, ComponentType.Armour, "plate");

        List<string> errors = SlotAssemblyValidator.Validate(BODY_COMPONENT_ID, [armour]);

        Assert.That(errors, Has.Count.EqualTo(1));
        Assert.That(errors[0], Does.Contain("does not satisfy"));
    }

    [Test, Description("Two components filling the same slot under the same parent is rejected.")]
    public void DuplicateSlotUnderSameParentIsRejected()
    {
        UnitComponentSlot bodySlot = Slot(10, BODY_COMPONENT_ID, TypeEquals(ComponentType.Weapon));

        UnitTemplateComponent first = Fitted(100, null, bodySlot, WEAPON_COMPONENT_ID, ComponentType.Weapon, "gun_a");
        UnitTemplateComponent second = Fitted(101, null, bodySlot, GUN_COMPONENT_ID, ComponentType.Weapon, "gun_b");

        List<string> errors = SlotAssemblyValidator.Validate(BODY_COMPONENT_ID, [first, second]);

        Assert.That(errors.Any(e => e.Contains("filled more than once")), Is.True);
    }

    [Test, Description("The same slot definition filled once per parent instance is allowed (true nesting).")]
    public void SameSlotUnderDifferentParentsIsAccepted()
    {
        // Two mounts in two distinct body slots, each exposing the SAME mount-slot definition, each filled.
        UnitComponentSlot bodySlotA = Slot(10, BODY_COMPONENT_ID, TypeEquals(ComponentType.Equipment));
        UnitComponentSlot bodySlotB = Slot(11, BODY_COMPONENT_ID, TypeEquals(ComponentType.Equipment));
        UnitComponentSlot mountSlot = Slot(20, MOUNT_COMPONENT_ID, TypeEquals(ComponentType.Weapon));

        UnitTemplateComponent mountA = Fitted(100, null, bodySlotA, MOUNT_COMPONENT_ID, ComponentType.Equipment, "mount_a");
        UnitTemplateComponent mountB = Fitted(101, null, bodySlotB, MOUNT_COMPONENT_ID, ComponentType.Equipment, "mount_b");
        UnitTemplateComponent gunA = Fitted(102, 100, mountSlot, GUN_COMPONENT_ID, ComponentType.Weapon, "gun_a");
        UnitTemplateComponent gunB = Fitted(103, 101, mountSlot, GUN_COMPONENT_ID, ComponentType.Weapon, "gun_b");

        List<string> errors = SlotAssemblyValidator.Validate(BODY_COMPONENT_ID, [mountA, mountB, gunA, gunB]);

        Assert.That(errors, Is.Empty);
    }
}
