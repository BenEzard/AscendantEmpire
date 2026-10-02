namespace AscendantEmpire.BackendContracts.Assets.GetPlayerAssets;

/// <summary>
/// One meta-category a Player has unlocked, and the categories visible beneath it.
/// </summary>
/// <remarks>
/// <para>
/// A meta-category is here because the Player holds an unlock row for it that has not yet lapsed, and for no other reason. Its
/// <see cref="Categories"/> may be empty - an unlocked meta-category with nothing available under it still shows its toolbar icon, over an empty
/// panel, because hiding something the Player has explicitly been granted would be stranger than showing it empty.
/// </para>
/// </remarks>
public record PlayerAssetMetaCategory
{
    public required int Id { get; init; }

    /// <summary>
    /// The meta-category's unique code, e.g. "ConstructionMaterials".
    /// </summary>
    public required string Code { get; init; }

    public required string DisplayName { get; init; }

    /// <summary>
    /// Root-relative URL of the icon shown for this meta-category in the Assets toolbar group.
    /// </summary>
    public required string IconPath { get; init; }

    public required int SortOrder { get; init; }

    /// <summary>
    /// The visible categories beneath this meta-category, in display order. May be empty.
    /// </summary>
    public required IReadOnlyCollection<PlayerAssetCategory> Categories { get; init; }
}
