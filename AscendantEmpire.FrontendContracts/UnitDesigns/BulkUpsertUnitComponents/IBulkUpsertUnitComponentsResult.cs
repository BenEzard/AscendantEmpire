namespace AscendantEmpire.FrontendContracts.UnitDesigns.BulkUpsertUnitComponents;

/// <summary>
/// Response summarising a bulk unit-component upsert: how many were added vs updated.
/// </summary>
public interface IBulkUpsertUnitComponentsResult
{
    /// <summary>
    /// Number of components newly inserted.
    /// </summary>
    int AddedCount { get; }

    /// <summary>
    /// Number of existing components updated because their code already existed.
    /// </summary>
    int UpdatedCount { get; }

    /// <summary>
    /// The codes of the components that were updated.
    /// </summary>
    IReadOnlyCollection<string> UpdatedCodes { get; }
}
