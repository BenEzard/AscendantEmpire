namespace AscendantEmpire.Domain.Enums.Battle;

/// <summary>
/// What a <c>BattleHistory</c> row records happening to an engagement.
/// </summary>
/// <remarks>
/// <para>
/// Numbered from 1, the same convention as <c>ForceHistoryEventType</c> and <c>StructureHistoryEventType</c>, so a value nobody set reads as no event at all rather than as a real one. The values
/// are persisted, so a member may be renamed but never renumbered.
/// </para>
/// <para>
/// These are the events a <b>campaign map</b> observer could plausibly perceive - fighting broke out here, another side arrived, a side gave way, it is over. The battle map's blow-by-blow is
/// deliberately absent: a distant watcher has no way of seeing it. See the Combat producer in <c>docs/design/intelligence-and-sightings.md</c>.
/// </para>
/// </remarks>
public enum BattleHistoryEventType
{
    /// <summary>
    /// Fighting broke out on the row's tile. Written once per Battle, together with a <see cref="ParticipantJoined"/> row for each founding side.
    /// </summary>
    Commenced = 1,

    /// <summary>
    /// A side joined the engagement. The row's <c>ParticipantPlayerId</c> is who.
    /// </summary>
    ParticipantJoined = 2,

    /// <summary>
    /// A side left it, whether by retreating, being routed or being wiped out. The row carries that side's <c>ExitReason</c> and its <c>TroopsLost</c>.
    /// </summary>
    /// <remarks>
    /// Written for every side still in the Battle when it concludes as well as for one that leaves mid-battle, so that what each side lost is always its own row rather than a battle-wide total
    /// nobody watched accrue.
    /// </remarks>
    ParticipantLeft = 3,

    /// <summary>
    /// The engagement ended. The row's <c>GroundHeldByPlayerId</c> is who was left holding the tile, or null when nobody was.
    /// </summary>
    Concluded = 4
}
