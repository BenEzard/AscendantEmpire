namespace AscendantEmpire.BackendContracts.UnitDesigns.CatalogueTransfer;

/// <summary>
/// Backend result of a catalogue-template bulk upsert, summarising what was inserted vs updated.
/// </summary>
public record BulkUpsertGlobalUnitTemplatesResult
{
    /// <summary>
    /// Number of templates newly inserted.
    /// </summary>
    public required int AddedCount { get; init; }

    /// <summary>
    /// Number of existing templates updated because their <c>Code</c> already existed.
    /// </summary>
    public required int UpdatedCount { get; init; }

    /// <summary>
    /// The codes of the templates that were updated.
    /// </summary>
    public required IReadOnlyCollection<string> UpdatedCodes { get; init; }
}
