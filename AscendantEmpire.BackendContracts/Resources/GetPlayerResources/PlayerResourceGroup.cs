namespace AscendantEmpire.BackendContracts.Resources.GetPlayerResources;

/// <summary>
/// One group of Resources within a grouped category, rendered as a labelled ticker row.
/// </summary>
/// <remarks>
/// Groups carry no unlock rows of their own, on the same terms as <see cref="PlayerResourceCategory"/>: a group is here because at least one Resource
/// in it is available to the Player, and it goes away with the last of them rather than showing an empty row.
/// </remarks>
public record PlayerResourceGroup
{
    public required int Id { get; init; }

    /// <summary>
    /// The group's unique code, hierarchical and dotted, e.g. "ConstructionMaterials.Wood.Logs".
    /// </summary>
    public required string Code { get; init; }

    public required string DisplayName { get; init; }

    /// <summary>
    /// Null where the group carries no explicit order, in which case it sorts after every numbered group and then by name.
    /// </summary>
    public required int? SortOrder { get; init; }

    /// <summary>
    /// The visible Resources in this group, in display order. Never empty - an emptied group is not returned at all.
    /// </summary>
    public required IReadOnlyCollection<PlayerResource> Resources { get; init; }
}
