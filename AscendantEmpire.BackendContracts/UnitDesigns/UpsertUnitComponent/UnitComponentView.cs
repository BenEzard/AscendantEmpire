using AscendantEmpire.Domain.Enums.UnitDesigns;

namespace AscendantEmpire.BackendContracts.UnitDesigns.UpsertUnitComponent;

/// <summary>
/// The saved component returned by the single-component create and update endpoints.
/// </summary>
/// <remarks>
/// Deliberately a summary rather than the full editable graph: the Entity Setup Tool closes its dialog on
/// success and re-fetches through <c>GET /unit-design-catalogue/{code}</c> when the component is next opened. What it does
/// need back is anything the save destroyed, so a cascaded slot deletion can be reported rather than
/// discovered later.
/// </remarks>
public record UnitComponentView
{
    public required int Id { get; init; }

    public required string Code { get; init; }

    public required string DisplayName { get; init; }

    public required ComponentType ComponentType { get; init; }

    /// <summary>
    /// The component's slot ids after the save, in the canonical slot order (ascending id).
    /// </summary>
    /// <remarks>
    /// Returned so a client that keeps the dialog open can carry the ids into its next save without a
    /// re-fetch. Empty for anything that isn't a body, since only bodies expose slots.
    /// </remarks>
    public IReadOnlyCollection<int> SlotIds { get; init; } = [];

    /// <summary>
    /// Number of body slots deleted because the payload no longer listed them.
    /// </summary>
    public int RemovedSlotCount { get; init; }

    /// <summary>
    /// Display names of the saved templates that lost a fitted component because the slot holding it was removed.
    /// </summary>
    /// <remarks>
    /// Deleting a slot takes its fittings with it — the fitted row cannot outlive the slot it names. That is
    /// intended, but it is not something to discover later in the designer, so the affected templates are
    /// reported here for the caller to surface.
    /// </remarks>
    public IReadOnlyCollection<string> UnfittedTemplateNames { get; init; } = [];
}
