namespace AscendantEmpire.BackendContracts.UnitDesigns.GetFullCatalog;

/// <summary>
/// A buff/bonus tag in the global catalog (dev god-view).
/// </summary>
public record CatalogTag
{
    public required int Id { get; init; }
    public required string Code { get; init; }
    public required string DisplayName { get; init; }
    public string? Description { get; init; }

    /// <summary>
    /// Whether the tag is system-created (seeded/built-in) rather than user-authored.
    /// </summary>
    public required bool SystemCreated { get; init; }
}
