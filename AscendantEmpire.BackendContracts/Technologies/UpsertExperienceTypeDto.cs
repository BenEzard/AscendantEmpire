namespace AscendantEmpire.BackendContracts.Technologies;

/// <summary>
/// One kind of experience to create or update in the catalogue.
/// </summary>
/// <remarks>
/// Matched on <see cref="Code"/>, so a type whose DisplayName changes but whose code does not is renamed rather than duplicated.
/// </remarks>
public record UpsertExperienceTypeDto
{
    /// <summary>
    /// Unique code for the experience type, e.g. "Biome.Jungle" or "Extraction.Mining".
    /// </summary>
    /// <remarks>
    /// Required rather than derived, unlike an Asset's: a producing system resolves its own concept against this string, so it has to be chosen deliberately
    /// rather than fall out of a display name that someone may later reword.
    /// </remarks>
    public required string Code { get; init; }

    /// <summary>
    /// Display name, e.g. "Jungle" or "Mining".
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// Flavour text describing what earns this kind of experience.
    /// </summary>
    /// <remarks>Only overwritten when supplied, so an update that omits it leaves any existing description alone.</remarks>
    public string? Description { get; init; }

    /// <summary>
    /// Path to an icon file, as a root-relative URL the frontend can resolve.
    /// </summary>
    public string? IconPath { get; init; }

    /// <summary>
    /// Display order in the Player's experience summary.
    /// </summary>
    /// <remarks>Derived from the position in the supplied list when omitted, spaced to leave gaps for future insertions.</remarks>
    public int? SortOrder { get; init; }
}
