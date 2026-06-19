namespace AscendantEmpire.Domain.Enums.UnitDesigns;

/// <summary>
/// The attribute a Template Slot acceptance rule matches a candidate component against.
/// </summary>
/// <remarks>
/// Weight/Subtype-based rules are deferred until those become first-class component
/// properties; for now rules match on component type and buff tags only.
/// </remarks>
public enum SlotRuleType
{
    /// <summary>
    /// Matches against the candidate component's <c>ComponentType</c>.
    /// </summary>
    ComponentType,

    /// <summary>
    /// Matches when the candidate component carries a specific buff tag (by tag code).
    /// </summary>
    HasTag,
}
