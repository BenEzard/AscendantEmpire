namespace AscendantEmpire.BackendContracts.UnitDesigns.SaveGlobalUnitTemplate;

/// <summary>
/// The request body for the dev design tool's create (POST) / update (PUT) of a global/common unit
/// template in the shared catalog.
/// </summary>
public record SaveGlobalUnitTemplateDto
{
    /// <summary>
    /// Unique, stable key for the template, e.g. "scout_walker_mk1".
    /// </summary>
    public required string Code { get; init; }

    public required string DisplayName { get; init; }

    /// <summary>
    /// Optional free-text description.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Image to show on the Map.
    /// </summary>
    public required string MapImagePath { get; init; }

    /// <summary>
    /// Code of the chosen base Body component.
    /// </summary>
    public required string BaseCode { get; init; }

    /// <summary>
    /// Fitted component code per base slot, positionally indexed to the base body's slots (in slot-id
    /// order). A null entry means that slot is left empty.
    /// </summary>
    public required IReadOnlyList<string?> SlotComponentCodes { get; init; }
}
