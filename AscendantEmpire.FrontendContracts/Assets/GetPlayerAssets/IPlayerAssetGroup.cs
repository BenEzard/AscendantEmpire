namespace AscendantEmpire.FrontendContracts.Assets.GetPlayerAssets;

/// <summary>
/// One group of Assets within a grouped category, rendered as a labelled ticker row.
/// </summary>
public interface IPlayerAssetGroup
{
    int Id { get; }

    /// <summary>
    /// The group's unique code, hierarchical and dotted, e.g. "ConstructionMaterials.Wood.Logs".
    /// </summary>
    string Code { get; }

    string DisplayName { get; }

    /// <summary>
    /// Null where the group carries no explicit order, in which case it sorts after every numbered group and then by name.
    /// </summary>
    int? SortOrder { get; }

    IReadOnlyCollection<IPlayerAsset> Assets { get; }
}
