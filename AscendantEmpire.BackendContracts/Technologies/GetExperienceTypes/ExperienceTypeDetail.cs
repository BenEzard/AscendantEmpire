namespace AscendantEmpire.BackendContracts.Technologies.GetExperienceTypes;

/// <summary>
/// One kind of experience an action can earn.
/// </summary>
/// <remarks>
/// Served in its own right as well as nested under a technology, because the editor needs the full list to choose from and the technology tree needs names
/// and icons for the sources it shows.
/// </remarks>
public record ExperienceTypeDetail
{
    public required int Id { get; init; }

    /// <summary>
    /// The unique code each producing system resolves its own concept against, e.g. "Biome.Jungle" or "Extraction.Mining".
    /// </summary>
    public required string Code { get; init; }

    public required string DisplayName { get; init; }

    public string? Description { get; init; }

    public required string IconPath { get; init; }

    public required int SortOrder { get; init; }
}
