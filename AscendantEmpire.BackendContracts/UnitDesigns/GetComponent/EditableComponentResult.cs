using AscendantEmpire.BackendContracts.UnitDesigns.BulkUpsertUnitComponents;

namespace AscendantEmpire.BackendContracts.UnitDesigns.GetComponent;

/// <summary>
/// A single component fetched in full for the dev Unit Designer's edit flow — the lossless read
/// counterpart of the bulk-upsert write side.
/// </summary>
/// <remarks>
/// Unlike the catalogue's <c>DesignComponent</c> (which omits body stats and supersession, and excludes
/// bodies), this carries everything the Component Setup dialog needs to re-open a component for editing.
/// The component graph reuses <see cref="UnitComponentDto"/> so the shape matches the upsert payload
/// exactly; <see cref="SupersededBy"/> lists the upgrade links where this component is the predecessor.
/// </remarks>
public record EditableComponentResult
{
    /// <summary>
    /// The component's server-assigned id.
    /// </summary>
    public required int Id { get; init; }

    /// <summary>
    /// The full component graph (general fields plus its body/weapon/armour extension, slots and tags),
    /// in the same shape the bulk-upsert endpoint accepts.
    /// </summary>
    public required UnitComponentDto Component { get; init; }

    /// <summary>
    /// Upgrade links where this component is the predecessor — the newer components that supersede it.
    /// Each link's <c>PredecessorCode</c> is this component's code.
    /// </summary>
    public required IReadOnlyCollection<ComponentSupersessionDto> SupersededBy { get; init; }
}
