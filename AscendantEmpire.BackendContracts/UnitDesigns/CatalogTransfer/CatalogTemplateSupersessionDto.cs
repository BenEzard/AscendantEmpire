namespace AscendantEmpire.BackendContracts.UnitDesigns.CatalogTransfer;

/// <summary>
/// A directed "successor supersedes predecessor" upgrade link between two catalogue templates, by code.
/// </summary>
/// <remarks>
/// Both codes must resolve to a template in the export's <c>Templates</c> list (or an existing template in
/// the database) on import; links whose ends do not resolve are skipped.
/// </remarks>
public record CatalogTemplateSupersessionDto
{
    /// <summary>
    /// Code of the older template being superseded.
    /// </summary>
    public required string PredecessorCode { get; init; }

    /// <summary>
    /// Code of the newer template that supersedes the predecessor.
    /// </summary>
    public required string SuccessorCode { get; init; }

    /// <summary>
    /// Whether this is the recommended/default upgrade among the predecessor's successors. At most one
    /// successor per predecessor may be flagged.
    /// </summary>
    public bool IsRecommended { get; init; }
}
