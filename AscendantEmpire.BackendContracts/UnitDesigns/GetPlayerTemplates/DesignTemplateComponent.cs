using AscendantEmpire.Domain.Enums.UnitDesigns;

namespace AscendantEmpire.BackendContracts.UnitDesigns.GetPlayerTemplates;

/// <summary>
/// A component fitted into one of a template's body slots.
/// </summary>
public record DesignTemplateComponent
{
    public required int UnitComponentSlotId { get; init; }
    public required int UnitComponentId { get; init; }
    public required string ComponentCode { get; init; }
    public required string ComponentDisplayName { get; init; }
    public required ComponentType ComponentType { get; init; }
}
