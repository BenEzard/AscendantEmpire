using AscendantEmpire.Domain.Enums.Assets;

namespace AscendantEmpire.FrontendContracts.Assets.GetPlayerAssets;

/// <summary>
/// One Asset available to the Player, as the ticker panel needs it.
/// </summary>
/// <remarks>
/// The seven fields from <see cref="Amount"/> onwards are placeholders that no table sources yet - see the banner on the backend contract.
/// </remarks>
public interface IPlayerAsset
{
    int Id { get; }

    /// <summary>
    /// The Asset's unique code, hierarchical and dotted, e.g. "Weapons.MeleeWeapons.BoneKnife.Low".
    /// </summary>
    string Code { get; }

    string DisplayName { get; }

    string? Description { get; }

    /// <summary>
    /// Root-relative URL of the Asset's icon, rendered straight into an image source.
    /// </summary>
    string IconPath { get; }

    /// <summary>
    /// Whether this Asset comes in grades, in which case every grade of it shares this display name.
    /// </summary>
    bool IsGradedAsset { get; }

    /// <summary>
    /// This row's grade, or None when <see cref="IsGradedAsset"/> is false.
    /// </summary>
    AssetGrade AssetGrade { get; }

    int SortOrder { get; }

    /// <summary>
    /// Whether this Asset is never treated as a luxury, however scarce it becomes. An input to the classification behind <see cref="IsLuxury"/>.
    /// </summary>
    bool NeverLuxury { get; }

    /// <summary>
    /// How many Rounds until this Asset stops being available, or null while none is scheduled.
    /// </summary>
    /// <remarks>
    /// Rounds, not Turns: the game clock is the Round number. Always positive when set, and null rather than zero when nothing is scheduled.
    /// </remarks>
    int? ObsoleteInRounds { get; }

    /// <summary>
    /// How much of this Asset the Player holds. <b>Placeholder - always zero.</b>
    /// </summary>
    int Amount { get; }

    /// <summary>
    /// The net change in the Player's holding each Turn. <b>Placeholder - always zero.</b>
    /// </summary>
    int AmountAddedPerTurn { get; }

    /// <summary>
    /// Whether part of this Asset is held back in a strategic reserve. <b>Placeholder - always false.</b>
    /// </summary>
    bool HasStrategicStockpile { get; }

    /// <summary>
    /// Whether the Player is importing this Asset. <b>Placeholder - always false.</b>
    /// </summary>
    bool IsImported { get; }

    /// <summary>
    /// Whether the Player is exporting this Asset. <b>Placeholder - always false.</b>
    /// </summary>
    bool IsExported { get; }

    /// <summary>
    /// Whether demand for this Asset is going unmet. <b>Placeholder - always false.</b>
    /// </summary>
    bool DemandNotMet { get; }

    /// <summary>
    /// Whether this Asset currently counts as a luxury. <b>Placeholder - always false.</b>
    /// </summary>
    bool IsLuxury { get; }
}
