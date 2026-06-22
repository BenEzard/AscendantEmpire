using AscendantEmpire.Domain.Enums.UnitDesigns;

namespace AscendantEmpire.BackendContracts.UnitDesigns.Shared;

/// <summary>
/// A single acceptance rule on a slot, surfaced so the UI can show/explain what a slot accepts.
/// </summary>
public record DesignSlotRule
{
    public required SlotRuleType RuleType { get; init; }
    public required SlotRuleOperator Operator { get; init; }
    public required string Value { get; init; }
}
