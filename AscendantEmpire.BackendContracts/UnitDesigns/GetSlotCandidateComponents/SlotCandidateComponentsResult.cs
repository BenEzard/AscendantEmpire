namespace AscendantEmpire.BackendContracts.UnitDesigns.GetSlotCandidateComponents;

/// <summary>
/// Response envelope for the per-slot candidate-components feed. Wrapping the list (rather than
/// returning a bare array) leaves room to add metadata later without breaking the response shape.
/// </summary>
public record SlotCandidateComponentsResult
{
    public required IReadOnlyCollection<DesignComponent> Components { get; init; }
}
