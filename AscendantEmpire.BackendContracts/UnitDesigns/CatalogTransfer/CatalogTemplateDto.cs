namespace AscendantEmpire.BackendContracts.UnitDesigns.CatalogTransfer;

/// <summary>
/// A single catalogue (global/common) unit template, expressed by stable codes so it round-trips across
/// databases.
/// </summary>
/// <remarks>
/// Mirrors the shape the dev tool's save path consumes (base body + positional slot codes). Only the
/// single-level fitting the tool can author is represented; nested fitted-component trees are out of scope.
/// </remarks>
public record CatalogTemplateDto
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
    /// Code of the base Body component this template is built around.
    /// </summary>
    public required string BaseCode { get; init; }

    /// <summary>
    /// Fitted component code per base slot, positionally indexed to the base body's slots (in slot-id
    /// order). A null entry means that slot is left empty.
    /// </summary>
    public required IReadOnlyList<string?> SlotComponentCodes { get; init; }
}
