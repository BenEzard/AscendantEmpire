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
    /// The catalogue row's id. <b>Not unique across the tree</b> - every per-other-player projection shares its template's id, so key on
    /// <see cref="Code"/>.
    /// </summary>
    int Id { get; }

    /// <summary>
    /// The category's unique code, hierarchical and dotted, e.g. "ConstructionMaterials.Wood". A projected category suffixes the subject Player.
    /// </summary>
    string Code { get; }

    string DisplayName { get; }

    /// <summary>
    /// The Player whose people this category counts, or <see langword="null"/> for an ordinary category.
    /// </summary>
    int? SubjectPlayerId { get; }

    /// <summary>
    /// Whether this category holds groups or Assets directly, which is what picks the layout the client draws.
    /// </summary>
    AssetCategoryType CategoryType { get; }

    int SortOrder { get; }

    IReadOnlyCollection<IPlayerAssetGroup> Groups { get; }

    IReadOnlyCollection<IPlayerAsset> Assets { get; }
}
