namespace AscendantEmpire.BackendContracts.UnitDesigns.GetFullCatalogue;

/// <summary>
/// A buff/bonus tag in the global catalogue (dev god-view).
/// </summary>
public record CatalogueTag
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
