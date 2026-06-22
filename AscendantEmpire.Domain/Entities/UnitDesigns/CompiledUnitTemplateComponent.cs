using AscendantEmpire.Domain.Enums.UnitDesigns;

namespace AscendantEmpire.Domain.Entities.UnitDesigns;

/// <summary>
/// A snapshot of one component in a compiled template — the body or a fitted component — captured so a
/// finalized (or enemy) design can be rendered without touching the live, unlock-filtered catalog.
/// </summary>
public class CompiledUnitTemplateComponent
{
    public int Id { get; set; }

    /// <summary>FK to <see cref="CompiledUnitTemplate.UnitTemplateId"/>.</summary>
    public int CompiledUnitTemplateId { get; set; }

    /// <summary>
    /// The parent snapshot row this one nests inside; null for the body and for components fitted directly
    /// into a body slot. Preserves the fitted-component tree so a compiled design can be rendered nested.
    /// </summary>
    public int? ParentId { get; set; }

    /// <summary>The slot this component fills; null for the template's body (which sits in no slot).</summary>
    public int? UnitComponentSlotId { get; set; }

    public int UnitComponentId { get; set; }

    /// <summary>Snapshot of the component's code at compile time.</summary>
    public required string Code { get; set; }

    /// <summary>Snapshot of the component's display name at compile time.</summary>
    public required string DisplayName { get; set; }

    public ComponentType ComponentType { get; set; }

    public CompiledUnitTemplate CompiledUnitTemplate { get; set; } = null!;

    /// <summary>The parent snapshot row; null for the body and root-fitted components.</summary>
    public CompiledUnitTemplateComponent? Parent { get; set; }

    /// <summary>The snapshot rows nested inside this one (0 or more).</summary>
    public ICollection<CompiledUnitTemplateComponent> Children { get; set; } = [];
}
