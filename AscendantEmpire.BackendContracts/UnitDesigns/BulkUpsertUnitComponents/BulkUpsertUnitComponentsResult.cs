namespace AscendantEmpire.BackendContracts.UnitDesigns.BulkUpsertUnitComponents;

/// <summary>
/// Backend result of a bulk component upsert, summarising what was inserted vs updated.
/// </summary>
public record BulkUpsertUnitComponentsResult
{
    /// <summary>
    /// Number of components newly inserted.
    /// </summary>
    public required int AddedCount { get; init; }

    /// <summary>
    /// Number of existing components updated because their <c>Code</c> already existed.
    /// </summary>
    public required int UpdatedCount { get; init; }

    /// <summary>
    /// The codes of the components that were updated.
    /// </summary>
    public required IReadOnlyCollection<string> UpdatedCodes { get; init; }

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
