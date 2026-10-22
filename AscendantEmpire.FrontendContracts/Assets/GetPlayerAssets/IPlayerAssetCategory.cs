using AscendantEmpire.Domain.Enums.Assets;

namespace AscendantEmpire.FrontendContracts.Assets.GetPlayerAssets;

/// <summary>
/// One category visible to the Player, holding either groups of Assets or Assets directly.
/// </summary>
/// <remarks>
/// Exactly one of <see cref="Groups"/> and <see cref="Assets"/> is populated, chosen by <see cref="CategoryType"/>; the other is empty rather than
/// null.
/// </remarks>
public interface IPlayerAssetCategory
{
    /// <summary>
    /// The catalogue row's id, unique across the tree - the per-other-player fan-out happens a level down, on the groups.
    /// </summary>
    int Id { get; }

    /// <summary>
    /// The category's unique code, hierarchical and dotted, e.g. "ConstructionMaterials.Wood".
    /// </summary>
    string Code { get; }

    string DisplayName { get; }

    /// <summary>
    /// Whether this category holds groups or Assets directly, which is what picks the layout the client draws.
    /// </summary>
    AssetCategoryType CategoryType { get; }

    int SortOrder { get; }

    IReadOnlyCollection<IPlayerAssetGroup> Groups { get; }

    IReadOnlyCollection<IPlayerAsset> Assets { get; }
}
