using AscendantEmpire.Domain.Entities.Games;

namespace AscendantEmpire.Domain.Entities.Assets;

/// <summary>
/// A Player's empire-wide intent for one Asset: how much of it to hold in a strategic reserve, and what share of production to divert towards that.
/// </summary>
/// <remarks>
/// <para>
/// <b>The policy is player-level; the reserve it governs is not.</b> A reserved quantity physically sits in <see cref="AssetBankBalance.ReservedQuantity"/>, in
/// the bank of a Settlement holding a Strategic Reserve, which is what leaves it somewhere an enemy can seize. Only the intent is empire-wide, and this is the
/// one row that carries it. Composite primary key on (PlayerId, AssetId), so a Player states one intent per Asset rather than one per Settlement.
/// </para>
/// <para>
/// <b>No row means no intent</b>, which is not the same as a target of zero. A conquest releases a captured reserve in full where the conqueror has no policy
/// for that Asset, and holds it where they have one - so the absence of a row has to stay distinguishable from a row asking for nothing.
/// </para>
/// </remarks>
public sealed class PlayerStrategicReservePolicy
{
    /// <summary>
    /// The largest share of production a policy may divert, as a whole percentage.
    /// </summary>
    public const int MAX_DIVERSION_PERCENT = 100;

    public int PlayerId { get; set; }

    public int AssetId { get; set; }

    /// <summary>
    /// How much of the Asset the Player wants held in reserve across their whole empire, stored scaled by <c>ScaledAmount.Scale</c> (1,000). Never negative.
    /// </summary>
    /// <remarks>
    /// <b>Stored as asked for, not as achievable.</b> Reserve capacity is granted by buildings and changes as they are raised and razed, so the target a
    /// Player states may be beyond what they can currently hold. Readers clamp to <c>MIN(target, capacity)</c> where they use it and leave this column alone,
    /// so that building more reserve later restores the Player's original intent rather than the clamp that applied while they were short of room.
    /// </remarks>
    public long TargetQuantity { get; set; }

    /// <summary>
    /// The share of each Turn's production of this Asset to divert into reserve, as a whole percentage from 0 to <see cref="MAX_DIVERSION_PERCENT"/>.
    /// </summary>
    /// <remarks>
    /// A percentage of <b>production</b>, never of existing stock: diversion is a split taken at the production credit, so it can only ever reach what was
    /// produced this Turn. Putting an existing quantity into reserve is a separate player order (RS-52).
    /// </remarks>
    public int DiversionPercent { get; set; }

    public Player Player { get; set; } = null!;

    public Asset Asset { get; set; } = null!;
}
