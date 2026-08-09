using AscendantEmpire.BackendContracts.UnitDesigns.BulkUpsertUnitComponents;

namespace AscendantEmpire.BackendContracts.UnitDesigns.UpsertUnitComponent;

/// <summary>
/// The payload for creating or updating a single unit component from the Entity Setup Tool.
/// </summary>
/// <remarks>
/// Reuses <see cref="UnitComponentDto"/> for the component graph so the single-component and catalogue-import
/// paths describe a component identically — the reconciler consumes the same shape either way.
/// </remarks>
public record UpsertUnitComponentDto
{
    /// <summary>
    /// The component itself, with its type-specific extension and child collections.
    /// </summary>
    public required UnitComponentDto Component { get; init; }

    /// <summary>
    /// The newer components that supersede this one (its upgrade paths).
    /// </summary>
    /// <remarks>
    /// Links are authored relative to this component, which is always the predecessor, so only the successor
    /// side is carried. Submitting this list reconciles the component's outgoing links to exactly its
    /// contents — omitted successors are unlinked.
    /// </remarks>
    public IReadOnlyCollection<ComponentSupersessionLinkDto> SupersededBy { get; init; } = [];
}

/// <summary>
/// One directed upgrade link in which the component being saved is the predecessor.
/// </summary>
public record ComponentSupersessionLinkDto
{
    /// <summary>
    /// Code of the newer component that supersedes the one being saved.
    /// </summary>
    public required string SuccessorCode { get; init; }

    /// <summary>
    /// Whether this is the recommended upgrade. At most one successor per predecessor may be flagged.
    /// </summary>
    public bool IsRecommended { get; init; }
}
