using AscendantEmpire.Domain.Entities.Assets;

namespace AscendantEmpire.Domain.Entities.UnitDesigns;

/// <summary>
/// An ongoing need a <see cref="UnitComponent"/> has, as a quantity of one <see cref="SupplySet"/>.
/// </summary>
/// <remarks>
/// <para>
/// What the quantity is measured per comes from the set's <c>SupplyType</c> — per troop per Turn for Food, per Movement Point for Fuel, per point of damage
/// for Repair — so there is no basis of its own to store.
/// </para>
/// <para>
/// The foreign key is on <see cref="UnitComponent"/> rather than on <c>UnitComponentBody</c>, even though only a Body may carry a requirement today. That
/// rule is enforced by <c>UnitComponentReconciler</c>, which makes moving a need onto another Component type a validation change rather than a migration.
/// </para>
/// <para>
/// Rows on one Component are cumulative: a Component naming two Repair sets needs both. The same set twice is refused, since for Food or Fuel it would
/// double the need.
/// </para>
/// </remarks>
public class UnitComponentSupplyRequirement
{
    public int Id { get; set; }

    public int UnitComponentId { get; set; }

    public UnitComponent UnitComponent { get; set; } = null!;

    public int SupplySetId { get; set; }

    public SupplySet SupplySet { get; set; } = null!;

    /// <summary>
    /// How much of the set is needed per unit of whatever its type is measured in, stored scaled by <c>ScaledAmount.Scale</c> (1,000).
    /// </summary>
    /// <remarks>
    /// A stored 1,000 is one food per troop per Turn on a Food set. Must be above zero — a requirement for nothing is not a requirement.
    /// </remarks>
    public long Quantity { get; set; }
}
