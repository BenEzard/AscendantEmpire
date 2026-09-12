namespace AscendantEmpire.Domain.Enums.Battle;

/// <summary>
/// Why a side is no longer in a Battle.
/// </summary>
/// <remarks>
/// <para>
/// Unusually, this enum's zero member is a real one: <see cref="NotApplicable"/> is what a side still in the fight carries. The values were implicit until F-26 made them explicit, and they are
/// already persisted on <c>BattleParticipant</c>, so a member may be renamed but never renumbered.
/// </para>
/// <para>
/// The consequence is that a column cannot use <c>0</c> to mean "not set" - <c>BattleHistory.ExitReason</c> is nullable with a check constraint instead.
/// </para>
/// </remarks>
public enum BattleExitReason
{
    /// <summary>
    /// The side is still in the Battle, so there is no exit to give a reason for.
    /// </summary>
    NotApplicable = 0,

    /// <summary>
    /// The side withdrew in good order, of its own choosing.
    /// </summary>
    Retreat = 1,

    /// <summary>
    /// The side broke and fled.
    /// </summary>
    Routed = 2,

    /// <summary>
    /// The side was wiped out.
    /// </summary>
    Destroyed = 3,

    /// <summary>
    /// The side left under a negotiated end to the fighting.
    /// </summary>
    CeaseFire = 4,

    /// <summary>
    /// The side was left holding the field when the Battle concluded.
    /// </summary>
    Victory = 5
}
