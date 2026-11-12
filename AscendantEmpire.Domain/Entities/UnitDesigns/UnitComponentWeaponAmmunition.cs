using AscendantEmpire.Domain.Entities.Assets;

namespace AscendantEmpire.Domain.Entities.UnitDesigns;

/// <summary>
/// One kind of ammunition a <see cref="UnitComponentWeapon"/> can fire.
/// </summary>
/// <remarks>
/// <para>
/// Ammunition is not a supply set. Its options are meant to carry damage profiles of their own — an anti-personnel and an anti-tank shell change what the
/// weapon does — rather than being drawn from interchangeably, which is why they hang off the weapon in a table of their own.
/// </para>
/// <para>
/// An attack or a defence spends one unit of whichever kind is loaded, so an option carries no rate: how dear a round is belongs to the Asset, and how many
/// go into one is settled by what the Asset counts — a box of bullets rather than a single round. Until the damage profile lands, an option is nothing but
/// the Asset, and the rows on one weapon are the kinds it accepts.
/// </para>
/// <para>
/// Hanging off <see cref="UnitComponentWeapon"/> rather than off <c>UnitComponent</c> means a Body's inherent attack carries options on the same terms as a
/// Weapon component, with no second path to keep in step.
/// </para>
/// </remarks>
public class UnitComponentWeaponAmmunition
{
    /// <summary>
    /// How many kinds of ammunition one weapon may accept.
    /// </summary>
    /// <remarks>
    /// Enforced by <c>UnitComponentReconciler</c> rather than by the schema: a cap is a design figure, and a unique index can only say that an Asset appears
    /// at most once.
    /// </remarks>
    public const int MAXIMUM_OPTIONS_PER_WEAPON = 3;

    public int Id { get; set; }

    /// <summary>
    /// The weapon (via <see cref="UnitComponentWeapon.UnitComponentId"/>) that fires this ammunition.
    /// </summary>
    public int UnitComponentWeaponId { get; set; }

    public UnitComponentWeapon Weapon { get; set; } = null!;

    /// <summary>
    /// The ammunition itself (an <c>assets.Asset</c>).
    /// </summary>
    public int AssetId { get; set; }

    public Asset Asset { get; set; } = null!;
}
