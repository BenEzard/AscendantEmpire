namespace AscendantEmpire.Domain.Entities.Assets;

/// <summary>
/// One Asset in a <see cref="SupplySet"/>, with what it costs and how heavily it is weighted.
/// </summary>
/// <remarks>
/// Keyed on the set and the Asset, so an Asset appears in a set at most once.
/// </remarks>
public class SupplySetAsset
{
    public int SupplySetId { get; set; }

    public SupplySet SupplySet { get; set; } = null!;

    /// <summary>
    /// The Asset in the set (a <c>assets.Asset</c>).
    /// </summary>
    public int AssetId { get; set; }

    public Asset Asset { get; set; } = null!;

    /// <summary>
    /// How much of this Asset one unit of the need costs, stored scaled by <c>ScaledAmount.Scale</c> (1,000).
    /// </summary>
    /// <remarks>
    /// <para>
    /// A cost rather than an efficiency, so the figure reads the same way whatever the set's type: Berries at 1,670 are 1.67 per troop, diesel at 800 goes
    /// further than petrol at 1,000, and steel plate at 2,000 is 2 per point of damage.
    /// </para>
    /// <para>
    /// Named to match <c>MapResourceTypeYield.AmountPerUnit</c>, the other per-unit amount in the catalogue. Must be above zero: it is the divisor wherever
    /// a need is turned into an amount of an Asset.
    /// </para>
    /// </remarks>
    public long AmountPerUnit { get; set; }

    /// <summary>
    /// How heavily this Asset is weighted when one member of the set is drawn at random.
    /// </summary>
    /// <remarks>
    /// Read only for a <c>SupplyType.Repair</c> set, which is the only kind drawn from; Food and Fuel are satisfied from whichever member is available and
    /// ignore it. Zero excludes a member from the draw without removing it from the set, so a Repair set must carry at least one member above zero.
    /// Defaults to 1, which makes an evenly weighted set data rather than a schema decision.
    /// </remarks>
    public int SelectionWeight { get; set; } = 1;
}
