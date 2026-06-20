using AscendantEmpire.Domain.Enums.UnitDesigns;

namespace AscendantEmpire.BackendContracts.UnitDesigns.BulkUpsertUnitComponents;

/// <summary>
/// A single acceptance rule constraining which components may be fitted into a body slot.
/// </summary>
public record UnitComponentSlotRuleDto
{
    /// <summary>
    /// Which component attribute the rule matches against (component type or buff tag).
    /// </summary>
    public required SlotRuleType RuleType { get; init; }

    /// <summary>
    /// How the rule's <see cref="Value"/> is compared.
    /// </summary>
    public required SlotRuleOperator Operator { get; init; }

    /// <summary>
    /// The value to compare against — a <c>ComponentType</c> name or a buff-tag code, depending on
    /// <see cref="RuleType"/>. For the <c>In</c> operator this is a comma-separated list.
    /// </summary>
    public required string Value { get; init; }
}
