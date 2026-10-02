namespace AscendantEmpire.BackendContracts.Assets;

public record UpsertAssetGroupDto
{
    /// <summary>
    /// Unique code for the Asset Group, e.g. "Logs" (within the <c>AssetCategory</c>).
    /// </summary>
    /// <remarks>If not supplied, code is derived automatically by normalising DisplayName with non-alpha characters removed and ProperCase applied.</remarks>
    public string? Code { get; init; }

    /// <summary>
    /// Display name, e.g. "Logs".
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// Optional sort order for display.
    /// </summary>
    /// <remarks>If not provided, it will be calculated automatically based on the order of the groups in the input list, with gaps for future insertions.</remarks>
    public int? SortOrder { get; init; }

    /// <summary>
    /// The assets that belong to this group.
    /// </summary>
    public required IReadOnlyCollection<UpsertAssetDto> Assets { get; init; } = [];
}
