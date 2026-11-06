using AscendantEmpire.Domain.Enums.Assets;

namespace AscendantEmpire.FrontendContracts.Assets.GetPlayerAssets;

/// <summary>
/// One Asset available to the Player, as the ticker panel needs it.
/// </summary>
/// <remarks>
/// The fields from <see cref="IsImported"/> onwards are placeholders that no table sources yet - see the banner on the backend contract.
/// <see cref="AmountAddedPerTurn"/> is one of them; <see cref="HasStrategicReserve"/> is not, and is measured.
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
    /// How much of this Asset the Player holds, as a whole number, rounded down.
    /// </summary>
    /// <remarks>
    /// Summed across every bank the Player owns for a banked Asset, which is what an Empire-wide expense spends. A banked Asset the Player holds none of is
    /// left out of the tree rather than sent as zero, so a panel lists only what they hold. Culture and every population figure do come back at zero,
    /// because nothing records them yet.
    /// </remarks>
    int Amount { get; }

    /// <summary>
    /// The net change in the Player's holding each Turn. <b>Placeholder - always zero.</b>
    /// </summary>
    int AmountAddedPerTurn { get; }

    /// <summary>
    /// Whether any bank of the Player's holds a strategic reserve of this Asset. A flag rather than a figure: the amount is out of circulation and absent
    /// from <see cref="Amount"/> until it is released.
    /// </summary>
    bool HasStrategicReserve { get; }

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
