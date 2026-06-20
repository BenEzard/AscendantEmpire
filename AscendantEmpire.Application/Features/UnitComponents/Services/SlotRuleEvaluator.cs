using AscendantEmpire.Domain.Entities.UnitDesigns;
using AscendantEmpire.Domain.Enums.UnitDesigns;

namespace AscendantEmpire.Application.Features.UnitComponents.Services;

/// <summary>
/// Decides whether a candidate component satisfies a Template Slot's acceptance rules.
/// </summary>
/// <remarks>
/// <para>This is the single authoritative "what fits where" evaluator, shared by the slot-candidate
/// read endpoint and (later) template compilation. The slot's <see cref="UnitComponentSlotRule"/> set
/// is the source of truth — not the prototype's slotTypes/allowedBaseIds shorthand.</para>
/// <para>A candidate is accepted only when it satisfies <b>every</b> rule on the slot (logical AND).</para>
/// </remarks>
public static class SlotRuleEvaluator
{
    /// <summary>
    /// Returns true when the candidate (identified by its type and tag codes) satisfies all the slot rules.
    /// </summary>
    public static bool Accepts(ComponentType candidateType, IReadOnlyCollection<string> candidateTagCodes, IEnumerable<UnitComponentSlotRule> rules)
    {
        foreach (UnitComponentSlotRule rule in rules)
        {
            if (!RulePasses(rule, candidateType, candidateTagCodes))
            {
                return false;
            }
        }

        return true;
    }

    private static bool RulePasses(UnitComponentSlotRule rule, ComponentType candidateType, IReadOnlyCollection<string> candidateTagCodes)
    {
        return rule.RuleType switch
        {
            // The candidate attribute is a single value: its component-type name.
            SlotRuleType.ComponentType => ComponentTypeRulePasses(rule.Operator, rule.Value, candidateType.ToString()),

            // The candidate attribute is a set: the codes of the tags it carries.
            SlotRuleType.HasTag => HasTagRulePasses(rule.Operator, rule.Value, candidateTagCodes),

            _ => false,
        };
    }

    private static bool ComponentTypeRulePasses(SlotRuleOperator op, string ruleValue, string candidateTypeName)
    {
        return op switch
        {
            SlotRuleOperator.Equals => Eq(candidateTypeName, ruleValue),
            SlotRuleOperator.NotEquals => !Eq(candidateTypeName, ruleValue),
            SlotRuleOperator.In => SplitValues(ruleValue).Any(v => Eq(candidateTypeName, v)),
            _ => false,
        };
    }

    private static bool HasTagRulePasses(SlotRuleOperator op, string ruleValue, IReadOnlyCollection<string> candidateTagCodes)
    {
        return op switch
        {
            // Equals: the candidate carries exactly this tag.
            SlotRuleOperator.Equals => candidateTagCodes.Any(t => Eq(t, ruleValue)),

            // NotEquals: the candidate does NOT carry this tag.
            SlotRuleOperator.NotEquals => !candidateTagCodes.Any(t => Eq(t, ruleValue)),

            // In: the candidate carries any one of the listed tags.
            SlotRuleOperator.In => SplitValues(ruleValue).Any(v => candidateTagCodes.Any(t => Eq(t, v))),

            _ => false,
        };
    }

    private static IEnumerable<string> SplitValues(string value)
    {
        return value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static bool Eq(string a, string b)
    {
        return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    }
}
