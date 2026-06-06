namespace AscendantEmpire.BackendContracts.Resources;

public record UpsertResourceGroupDto
{
    /// <summary>
    /// Unique code for the Resource Group, e.g. "Logs" (within the <c>ResourceCategory</c>).
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
    /// The resources that belong to this group.
    /// </summary>
    public required IReadOnlyCollection<UpsertResourceDto> Resources { get; init; } = [];
}
