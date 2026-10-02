namespace AscendantEmpire.FrontendContracts.Assets.GetPlayerAssets;

/// <summary>
/// One meta-category the Player has unlocked, and the categories visible beneath it.
/// </summary>
/// <remarks>
/// <see cref="Categories"/> may be empty: an unlocked meta-category with nothing available under it still shows its toolbar icon, over an empty panel.
/// </remarks>
public interface IPlayerAssetMetaCategory
{
    int Id { get; }

    /// <summary>
    /// The meta-category's unique code, e.g. "ConstructionMaterials".
    /// </summary>
    string Code { get; }

    string DisplayName { get; }

    /// <summary>
    /// Root-relative URL of the icon shown for this meta-category in the Assets toolbar group.
    /// </summary>
    string IconPath { get; }

    int SortOrder { get; }

    IReadOnlyCollection<IPlayerAssetCategory> Categories { get; }
}
