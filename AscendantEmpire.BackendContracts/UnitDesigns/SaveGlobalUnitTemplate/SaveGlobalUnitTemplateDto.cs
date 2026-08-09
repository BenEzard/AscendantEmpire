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
    /// The component fitted into each filled slot, keyed by the base body's slot code.
    /// </summary>
    /// <remarks>
    /// Only filled slots appear — an absent slot code means that slot is left empty. Keying by slot code
    /// rather than by position means adding, removing or reordering the base body's slots cannot silently
    /// re-map which component sits where.
    /// </remarks>
    public required IReadOnlyDictionary<string, string> SlotComponentCodes { get; init; }
}
