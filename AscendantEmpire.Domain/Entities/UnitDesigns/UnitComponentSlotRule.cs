using AscendantEmpire.Domain.Enums.UnitDesigns;

using System.ComponentModel.DataAnnotations;

namespace AscendantEmpire.Domain.Entities.UnitDesigns;

/// <summary>
/// A single acceptance rule for a <see cref="UnitComponentSlot"/>.
/// </summary>
/// <remarks>
/// A slot has one or more rules; a candidate component must satisfy them to be fitted.
/// </remarks>
public class UnitComponentSlotRule
{
    public const int VALUE_MAX_LENGTH = 100;

    public int Id { get; set; }

    public int UnitComponentSlotId { get; set; }

    /// <summary>
    /// Which component attribute the rule matches against.
    /// </summary>
    public SlotRuleType RuleType { get; set; }

    /// <summary>
    /// How the rule's <see cref="Value"/> is compared.
    /// </summary>
    public SlotRuleOperator Operator { get; set; }

    /// <summary>
    /// The value to compare against — a <c>ComponentType</c> name or a buff-tag code, depending on
    /// <see cref="RuleType"/>. For the <c>In</c> operator this is a comma-separated list.
    /// </summary>
    [MaxLength(VALUE_MAX_LENGTH)]
    public required string Value { get; set; }

    public UnitComponentSlot Slot { get; set; } = null!;
}
