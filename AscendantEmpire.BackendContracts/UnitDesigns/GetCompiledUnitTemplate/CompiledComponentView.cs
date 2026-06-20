using AscendantEmpire.Domain.Enums.UnitDesigns;

namespace AscendantEmpire.BackendContracts.UnitDesigns.GetCompiledUnitTemplate;

/// <summary>
/// A resolved component in a compiled design — the body (null slot) or a fitted component (with slot).
/// </summary>
public record CompiledComponentView
{
    /// <summary>The slot this component fills; null for the template's body.</summary>
    public int? UnitComponentSlotId { get; init; }

    public required int UnitComponentId { get; init; }
    public required string Code { get; init; }
    public required string DisplayName { get; init; }
    public required ComponentType ComponentType { get; init; }
}
