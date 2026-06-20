using AscendantEmpire.Application.Features.UnitComponents.Services;
using AscendantEmpire.Domain.Entities.UnitDesigns;
using AscendantEmpire.Domain.Enums.UnitDesigns;

namespace AscendantEmpire.Tests.Services.UnitComponents;

/// <summary>
/// Pure unit tests for <see cref="SlotRuleEvaluator"/> — no database required.
/// </summary>
[TestFixture]
internal class SlotRuleEvaluatorTests
{
    private static UnitComponentSlotRule Rule(SlotRuleType type, SlotRuleOperator op, string value)
    {
        return new UnitComponentSlotRule { RuleType = type, Operator = op, Value = value };
    }

    [Test, Description("ComponentType Equals accepts a matching type and rejects a non-matching one.")]
    public void ComponentTypeEqualsMatchesByType()
    {
        UnitComponentSlotRule[] rules = [Rule(SlotRuleType.ComponentType, SlotRuleOperator.Equals, nameof(ComponentType.Weapon))];

        using (Assert.EnterMultipleScope())
        {
            Assert.That(SlotRuleEvaluator.Accepts(ComponentType.Weapon, [], rules), Is.True, "Weapon should fit a Weapon-only slot.");
            Assert.That(SlotRuleEvaluator.Accepts(ComponentType.Armour, [], rules), Is.False, "Armour should not fit a Weapon-only slot.");
        }
    }

    [Test, Description("ComponentType In accepts any type in the comma-separated list.")]
    public void ComponentTypeInMatchesAnyListedType()
    {
        UnitComponentSlotRule[] rules = [Rule(SlotRuleType.ComponentType, SlotRuleOperator.In, "Weapon, Equipment")];

        using (Assert.EnterMultipleScope())
        {
            Assert.That(SlotRuleEvaluator.Accepts(ComponentType.Weapon, [], rules), Is.True);
            Assert.That(SlotRuleEvaluator.Accepts(ComponentType.Equipment, [], rules), Is.True);
            Assert.That(SlotRuleEvaluator.Accepts(ComponentType.Armour, [], rules), Is.False);
        }
    }

    [Test, Description("ComponentType NotEquals rejects the named type and accepts others.")]
    public void ComponentTypeNotEqualsExcludesNamedType()
    {
        UnitComponentSlotRule[] rules = [Rule(SlotRuleType.ComponentType, SlotRuleOperator.NotEquals, nameof(ComponentType.Body))];

        using (Assert.EnterMultipleScope())
        {
            Assert.That(SlotRuleEvaluator.Accepts(ComponentType.Body, [], rules), Is.False);
            Assert.That(SlotRuleEvaluator.Accepts(ComponentType.Weapon, [], rules), Is.True);
        }
    }

    [Test, Description("HasTag Equals requires the candidate to carry the tag.")]
    public void HasTagEqualsRequiresTag()
    {
        UnitComponentSlotRule[] rules = [Rule(SlotRuleType.HasTag, SlotRuleOperator.Equals, "amphibious")];

        using (Assert.EnterMultipleScope())
        {
            Assert.That(SlotRuleEvaluator.Accepts(ComponentType.Weapon, ["amphibious", "stealth"], rules), Is.True);
            Assert.That(SlotRuleEvaluator.Accepts(ComponentType.Weapon, ["stealth"], rules), Is.False);
        }
    }

    [Test, Description("HasTag NotEquals rejects candidates carrying the tag.")]
    public void HasTagNotEqualsExcludesTagHolders()
    {
        UnitComponentSlotRule[] rules = [Rule(SlotRuleType.HasTag, SlotRuleOperator.NotEquals, "oversized")];

        using (Assert.EnterMultipleScope())
        {
            Assert.That(SlotRuleEvaluator.Accepts(ComponentType.Weapon, ["oversized"], rules), Is.False);
            Assert.That(SlotRuleEvaluator.Accepts(ComponentType.Weapon, ["light"], rules), Is.True);
        }
    }

    [Test, Description("HasTag In accepts a candidate that carries any one of the listed tags.")]
    public void HasTagInMatchesAnyListedTag()
    {
        UnitComponentSlotRule[] rules = [Rule(SlotRuleType.HasTag, SlotRuleOperator.In, "amphibious, hover")];

        using (Assert.EnterMultipleScope())
        {
            Assert.That(SlotRuleEvaluator.Accepts(ComponentType.Weapon, ["hover"], rules), Is.True);
            Assert.That(SlotRuleEvaluator.Accepts(ComponentType.Weapon, ["wheeled"], rules), Is.False);
        }
    }

    [Test, Description("All rules must pass (logical AND): right type but missing tag is rejected.")]
    public void MultipleRulesAreAndedTogether()
    {
        UnitComponentSlotRule[] rules =
        [
            Rule(SlotRuleType.ComponentType, SlotRuleOperator.Equals, nameof(ComponentType.Weapon)),
            Rule(SlotRuleType.HasTag, SlotRuleOperator.Equals, "naval"),
        ];

        using (Assert.EnterMultipleScope())
        {
            Assert.That(SlotRuleEvaluator.Accepts(ComponentType.Weapon, ["naval"], rules), Is.True, "Right type and tag.");
            Assert.That(SlotRuleEvaluator.Accepts(ComponentType.Weapon, [], rules), Is.False, "Right type, missing tag.");
            Assert.That(SlotRuleEvaluator.Accepts(ComponentType.Armour, ["naval"], rules), Is.False, "Wrong type.");
        }
    }

    [Test, Description("A slot with no rules accepts anything (vacuous truth).")]
    public void NoRulesAcceptsAnything()
    {
        Assert.That(SlotRuleEvaluator.Accepts(ComponentType.Weapon, [], []), Is.True);
    }
}
