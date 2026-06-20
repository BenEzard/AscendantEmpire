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
}
