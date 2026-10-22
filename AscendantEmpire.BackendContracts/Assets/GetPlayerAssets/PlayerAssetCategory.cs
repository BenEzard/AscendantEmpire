using AscendantEmpire.Domain.Enums.Assets;

namespace AscendantEmpire.BackendContracts.Assets.GetPlayerAssets;

/// <summary>
/// One category visible to a Player, holding either groups of Assets or Assets directly.
/// </summary>
/// <remarks>
/// <para>
/// Categories carry no unlock rows of their own. One is here because at least one Asset beneath it is available to the Player, and it disappears
/// with the last of them, so its visibility can never disagree with its contents.
/// </para>
/// <para>
/// Exactly one of <see cref="Groups"/> and <see cref="Assets"/> is populated, chosen by <see cref="CategoryType"/>. The other is empty rather than
/// null, so a client can render whichever arm it recognises without a null check.
/// </para>
/// </remarks>
public record PlayerAssetCategory
{
    /// <summary>
    /// The catalogue row's id, unique across the returned tree. The per-other-player fan-out happens a level down, so no two categories share one -
    /// see <see cref="PlayerAssetGroup"/>.
    /// </summary>
    public required int Id { get; init; }

    /// <summary>
    /// The category's unique code, hierarchical and dotted, e.g. "ConstructionMaterials.Wood".
    /// </summary>
    public required string Code { get; init; }

    public required string DisplayName { get; init; }

    /// <summary>
    /// Whether this category holds groups or Assets directly, which is what picks the layout the client draws.
    /// </summary>
    public required AssetCategoryType CategoryType { get; init; }

    public required int SortOrder { get; init; }

    /// <summary>
    /// The visible groups beneath this category, in display order. Empty unless <see cref="CategoryType"/> is GroupedAssets.
    /// </summary>
    public required IReadOnlyCollection<PlayerAssetGroup> Groups { get; init; }

    /// <summary>
    /// The visible Assets hanging directly off this category, in display order. Empty unless <see cref="CategoryType"/> is StandaloneAssets.
    /// </summary>
    public required IReadOnlyCollection<PlayerAsset> Assets { get; init; }
}
