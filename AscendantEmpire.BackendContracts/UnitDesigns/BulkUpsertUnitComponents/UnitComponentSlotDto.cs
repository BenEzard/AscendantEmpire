namespace AscendantEmpire.BackendContracts.UnitDesigns.BulkUpsertUnitComponents;

/// <summary>
/// A Template Slot defined by a component — a place where another Unit Component can be plugged in.
/// </summary>
public record UnitComponentSlotDto
{
    /// <summary>
    /// The slot's database id — the upsert key when the caller has one.
    /// </summary>
    /// <remarks>
    /// <para>Populated on reads. On writes it names an existing slot to update in place, keeping the row and
    /// with it every fitted template component pointing at it. <c>null</c> falls back to matching on
    /// <see cref="Code"/>, which is what a catalogue imported from another database does — its ids are
    /// meaningless here. An id the target component doesn't own is rejected rather than treated as new.</para>
    /// <para>Sending the id matters for a rename: it is the only way the upsert can tell "this slot's code
    /// changed" apart from "that slot went and a different one arrived".</para>
    /// </remarks>
    public int? Id { get; init; }

    /// <summary>
    /// Stable key for the slot, unique within its owning body (e.g. <c>main-hand</c>).
    /// </summary>
    /// <remarks>
    /// <para>The portable identity: it survives a move between databases, which <see cref="Id"/> does not, and
    /// it is how a template names the slot it fits a component into.</para>
    /// <para>Derived from <see cref="DisplayName"/> by the authoring tool rather than typed by hand, so two
    /// slots on one body may not share a display name — they would derive the same code.</para>
    /// </remarks>
    public required string Code { get; init; }

    /// <summary>
    /// The label shown for the slot, and the source <see cref="Code"/> is derived from.
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// Whether the slot is shown on the UI.
    /// </summary>
    public bool IsVisible { get; init; }

    /// <summary>
    /// Normalized X offset of the slot from the owning component's image center (0 = center, ±1 = edges).
    /// </summary>
    public decimal UIXPosition { get; init; }

    /// <summary>
    /// Normalized Y offset of the slot from the owning component's image center (0 = center, ±1 = edges).
    /// </summary>
    public decimal UIYPosition { get; init; }

    /// <summary>
    /// Acceptance rules that decide which components may be fitted (1 or more).
    /// </summary>
    public IReadOnlyCollection<UnitComponentSlotRuleDto> Rules { get; init; } = [];
}
