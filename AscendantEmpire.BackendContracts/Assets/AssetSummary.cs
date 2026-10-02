namespace AscendantEmpire.BackendContracts.Assets;

/// <summary>
/// Enough of an Asset to choose it from a list.
/// </summary>
public record AssetSummary
{
    public required int Id { get; init; }

    public required string Code { get; init; }

    public required string DisplayName { get; init; }

    /// <summary>
    /// The display name of the meta category the Asset belongs to, for grouping the list.
    /// </summary>
    public required string MetaCategoryDisplayName { get; init; }
}
