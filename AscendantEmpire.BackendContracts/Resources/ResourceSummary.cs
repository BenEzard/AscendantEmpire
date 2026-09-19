namespace AscendantEmpire.BackendContracts.Resources;

/// <summary>
/// Enough of a Resource to choose it from a list.
/// </summary>
public record ResourceSummary
{
    public required int Id { get; init; }

    public required string Code { get; init; }

    public required string DisplayName { get; init; }

    /// <summary>
    /// The display name of the meta category the Resource belongs to, for grouping the list.
    /// </summary>
    public required string MetaCategoryDisplayName { get; init; }
}
