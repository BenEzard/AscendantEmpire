using AscendantEmpire.Domain.Entities.Assets;

namespace AscendantEmpire.Domain.Entities.Technologies;

/// <summary>
/// One Asset, and how much of it, that a Player must pay to unlock a Technology.
/// </summary>
/// <remarks>
/// <para>
/// The design doc's "initial (acquisition) cost" is zero or more Assets with a quantity, so it is a child table rather than a column. <b>No rows means the
/// Technology is free to unlock</b>, which is the common case for the early tree. Composite primary key on (TechnologyId, AssetId).
/// </para>
/// <para>
/// The cost is <b>not</b> multiplied by the game's technology speed multiplier and <b>not</b> randomised per Player: the doc scopes the multiplier to the
/// research required and fuzziness to the three buckets, and a material price in Oak and Marble is not research. That is why there is no per-Player copy of
/// this table, and why it is the one catalogue table a gameplay query may read live.
/// </para>
/// <para>
/// <b>Nothing pays this yet.</b> Both halves now exist - <see cref="Assets.AssetBank"/> and <see cref="Assets.AssetBankBalance"/> are mapped, and the
/// drawdown service spends from them - but no unlock path calls it with these rows, and nothing credits a balance in the first place, so a Technology with
/// cost rows still cannot be unlocked and the seeded early tree should carry none. The cost is paid out of the bank of whichever of the Player's settlements
/// the purchase is made at; an Asset whose <c>StorageScope</c> is not <c>SettlementBank</c> cannot be a cost, since there is no balance to debit.
/// </para>
/// </remarks>
public class TechnologyAcquisitionCost
{
    public int TechnologyId { get; set; }

    /// <summary>
    /// The Asset to be paid.
    /// </summary>
    /// <remarks>
    /// A real foreign key, unlike <see cref="TechnologyGrant.TargetId"/>, because this one always points at the same table.
    /// </remarks>
    public int AssetId { get; set; }

    /// <summary>
    /// How much of the Asset is required. Always greater than zero - a free Technology has no row rather than a row of nothing.
    /// </summary>
    public int Quantity { get; set; }

    public Technology Technology { get; set; } = null!;

    public Asset Asset { get; set; } = null!;
}
