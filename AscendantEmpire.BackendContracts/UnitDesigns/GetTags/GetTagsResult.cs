namespace AscendantEmpire.BackendContracts.UnitDesigns.GetTags;

/// <summary>
/// The full set of tags in the global catalog, for the dev Unit Designer's tag manager.
/// </summary>
public record GetTagsResult
{
    public required IReadOnlyCollection<TagView> Tags { get; init; }
}
