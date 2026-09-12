namespace AscendantEmpire.Domain.Enums.Structures;

/// <summary>
/// What a <c>StructureHistory</c> row records happening to a Structure.
/// </summary>
/// <remarks>
/// <para>
/// Numbered from 1, the same convention as <c>ForceHistoryEventType</c>, so a value nobody set reads as no event at all rather than as a real one. The values are persisted, so a member may be
/// renamed but never renumbered.
/// </para>
/// <para>
/// <b>No <c>Damaged</c> member yet.</b> <c>docs/design/intelligence-and-sightings.md</c> lists damage among the structure events, but a Structure has no health to lose until it gains a column
/// for it. The member is added then, with the next free value.
/// </para>
/// </remarks>
public enum StructureHistoryEventType
{
    /// <summary>
    /// The Structure was founded on the row's tile, and construction on it began.
    /// </summary>
    Founded = 1,

    /// <summary>
    /// Construction advanced without finishing. The row's <c>CompletionPercent</c> is the progress after the step.
    /// </summary>
    Progressed = 2,

    /// <summary>
    /// Construction reached 100%. Written in place of <see cref="Progressed"/> for the step that finishes it, so a completed Structure always has exactly one row that says so.
    /// </summary>
    Completed = 3,

    /// <summary>
    /// The Structure changed hands. The row's <c>PlayerId</c> is the new owner and <c>PreviousOwnerPlayerId</c> the old one.
    /// </summary>
    Captured = 4,

    /// <summary>
    /// The Structure was destroyed. The instance is soft-deleted, by stamping its <c>RemovedOnTurnId</c>, rather than removed.
    /// </summary>
    Razed = 5
}
