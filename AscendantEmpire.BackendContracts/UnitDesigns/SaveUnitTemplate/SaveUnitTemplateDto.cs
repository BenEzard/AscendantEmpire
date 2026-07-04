namespace AscendantEmpire.BackendContracts.UnitDesigns.SaveUnitTemplate;

/// <summary>
/// The request body for creating (POST) or updating (PUT) a custom (player-owned) unit template.
/// </summary>
public record SaveUnitTemplateDto
{
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
