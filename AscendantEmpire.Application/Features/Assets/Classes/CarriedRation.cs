using AscendantEmpire.Application.Helpers.ScaledNumerics;

namespace AscendantEmpire.Application.Features.Assets.Classes;

/// <summary>
/// One edible Asset a Force is carrying: how much of it is there, and what it is worth as food.
/// </summary>
/// <remarks>
/// <para>
/// The counterpart of a tile's <c>FoodSource</c>, for the other place a Force's food can come from. It is a flatter thing: a load has no biome rate behind it
/// and no patch to deplete, so what it holds is a quantity and nothing else.
/// </para>
/// <param name="HeldRaw">How much of the Asset the Force's banks hold between them, raw fixed-point.</param>
/// <param name="AmountPerUnit">
/// What one ration costs in this Asset, raw fixed-point from the food <c>SupplySet</c>'s member row. Something which satisfies less requires more.
/// </param>
/// </remarks>
internal readonly record struct CarriedRation(int AssetId, long HeldRaw, long AmountPerUnit)
{
    /// <summary>
    /// What the holding is worth in rations - individuals fed for one Turn - which is the measure the carried Assets are levelled against each other in.
    /// </summary>
    /// <remarks>
    /// Int128 because an Asset amount already in thousandths is scaled by a thousand again before the division. An <see cref="AmountPerUnit"/> of zero or less
    /// is a catalogue defect rather than a free meal, and is worth nothing here.
    /// </remarks>
    public ScaledAmount InRations => AmountPerUnit <= 0
        ? ScaledAmount.Zero
        : ScaledAmount.FromRaw((long)((Int128)HeldRaw * ScaledAmount.Scale / AmountPerUnit));
}
