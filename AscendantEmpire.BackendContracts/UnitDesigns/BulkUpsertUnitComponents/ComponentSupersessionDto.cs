namespace AscendantEmpire.BackendContracts.UnitDesigns.BulkUpsertUnitComponents;

/// <summary>
/// A directed "successor supersedes predecessor" upgrade link between two components, by code.
/// </summary>
/// <remarks>
/// Both codes must resolve to a component in the payload's <c>Components</c> list or an existing
/// component in the database. The list is authoritative per predecessor: a predecessor's existing
/// links are replaced by exactly those submitted for it.
/// </remarks>
public record ComponentSupersessionDto
{
    /// <summary>
    /// Code of the older component being superseded.
    /// </summary>
    public required string PredecessorCode { get; init; }

    /// <summary>
    /// Code of the newer component that supersedes the predecessor.
    /// </summary>
    public required string SuccessorCode { get; init; }

    /// <summary>
    /// Whether this is the recommended/default upgrade among the predecessor's successors. At most one
    /// successor per predecessor may be flagged.
    /// </summary>
    public bool IsRecommended { get; init; }
}
