using AscendantEmpire.Domain.Enums.Assets;

namespace AscendantEmpire.BackendContracts.Assets;

public record UpsertAssetDto
{
    /// <summary>
    /// Unique code for the Asset, e.g. "ConstructionMaterials.Wood.Logs.OakLog".
    /// </summary>
    /// <remarks>
    /// <para>If not supplied, code is derived automatically by normalising DisplayName with non-alpha characters removed and ProperCase applied.</para>
    /// <para>
    /// A graded Asset must supply one, because every grade of it shares a DisplayName and so would derive the same code. Suffix the grade to tell them
    /// apart - "Weapons.MeleeWeapons.BoneKnife.Low".
    /// </para>
    /// </remarks>
    public string? Code { get; init; }

    /// <summary>
    /// Display name, e.g. "Oak Log".
    /// </summary>
    /// <remarks>Shared by every grade of a graded Asset - the grade is not part of the name.</remarks>
    public required string DisplayName { get; init; }

    /// <summary>
    /// Flavour text describing what this Asset is and what it is used for.
    /// </summary>
    /// <remarks>Only overwritten when supplied, so an update that omits it leaves any existing description alone.</remarks>
    public string? Description { get; init; }

    /// <summary>
    /// Path to an icon file, as a URL the frontend can resolve.
    /// </summary>
    /// <remarks>
    /// Served from the frontend's <c>public</c> folder rather than bundled, so it is a root-relative URL with forward slashes -
    /// "/resources/wood/oak-log_40px.png". Omit it for an Asset with no icon.
    /// </remarks>
    public string? IconPath { get; init; }

    /// <summary>
    /// Optional sort order for display within the Asset's Group or Category.
    /// </summary>
    /// <remarks>If not provided, it will be calculated automatically based on the order of the assets in the input list, with gaps for future insertions.</remarks>
    public int? SortOrder { get; init; }

    /// <summary>
    /// Whether this Asset comes in grades, in which case one row is expected per grade.
    /// </summary>
    public bool IsGradedAsset { get; init; } = false;

    /// <summary>
    /// The grade of this Asset, or <c>None</c> when <see cref="IsGradedAsset"/> is false.
    /// </summary>
    public AssetGrade AssetGrade { get; init; } = AssetGrade.None;

    /// <summary>
    /// Whether this Asset can never be treated as a luxury, however scarce it becomes.
    /// </summary>
    public bool NeverLuxury { get; init; } = false;

    /// <summary>
    /// Where the quantity behind this Asset is held - a settlement's bank, the Player, or nowhere at all.
    /// </summary>
    /// <remarks>
    /// Defaults to <see cref="AssetStorageScope.SettlementBank"/>, which covers nearly the whole catalogue, so only the experience and population Assets
    /// need state one.
    /// </remarks>
    public AssetStorageScope StorageScope { get; init; } = AssetStorageScope.SettlementBank;
}
