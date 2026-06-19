namespace AscendantEmpire.Domain.Enums.UnitDesigns;

/// <summary>
/// How a Template Slot acceptance rule compares the rule's <c>Value</c> against the candidate component.
/// </summary>
public enum SlotRuleOperator
{
    Equals,
    NotEquals,

    /// <summary>
    /// The candidate matches when its attribute is one of a comma-separated list held in the rule's <c>Value</c>.
    /// </summary>
    In,
}
