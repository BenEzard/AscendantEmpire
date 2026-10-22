namespace AscendantEmpire.FrontendContracts.Assets.GetPlayerAssets;

/// <summary>
/// One group of Assets within a grouped category, rendered as a labelled ticker row.
/// </summary>
public interface IPlayerAssetGroup
{
    /// <summary>
    /// The catalogue row's id. <b>Not unique across the tree</b> - every per-other-player projection shares its template's id, so key on
    /// <see cref="Code"/>.
    /// </summary>
    int Id { get; }

    /// <summary>
    /// The group's unique code, hierarchical and dotted, e.g. "ConstructionMaterials.Wood.Logs". A projected group suffixes the subject Player.
    /// </summary>
    string Code { get; }

    string DisplayName { get; }

    /// <summary>
    /// The Player whose people this group counts, or <see langword="null"/> for an ordinary group.
    /// </summary>
    int? SubjectPlayerId { get; }

    /// <summary>
    /// Null where the group carries no explicit order, in which case it sorts after every numbered group and then by name.
    /// </summary>
    int? SortOrder { get; }

    IReadOnlyCollection<IPlayerAsset> Assets { get; }
}
