using AscendantEmpire.Domain.Enums.Structures;

namespace AscendantEmpire.Domain.Entities.Structures;

/// <summary>
/// One append-only record of a Structure being founded, built, captured or razed, stamped with the Turn and the sequence it happened at.
/// </summary>
/// <remarks>
/// <para>
/// The second of the raw feeds in <c>docs/design/intelligence-and-sightings.md</c>, beside <c>MovementHistory</c>. It is a fact about the world, not about anybody's perception of it: written
/// whether or not anybody was watching, with the witness question asked later by the end-of-turn fold.
/// </para>
/// <para>
/// <b>Written only through <c>IStructureLifecycleWriter</c>.</b> That is the point of the table. Appending history at each call site instead would eventually miss one, and a missed path fails
/// silently, as intelligence that simply never arrives.
/// </para>
/// <para>
/// <b>Every row belongs to a Turn.</b> A Structure placed outside any Turn gets no row at all: nobody could have watched it appear, and there is no Turn to stamp it with.
/// </para>
/// <para>
/// <b>Campaign map Structures only.</b> Battle map Structures are not something a campaign map observer can see, and the writer refuses them.
/// </para>
/// <para>
/// <b>No RowVersion.</b> Nothing ever updates a row, so there is no concurrent edit for a token to detect.
/// </para>
/// </remarks>
public class StructureHistory
{
    /// <summary>
    /// A <c>bigint</c>, matching <c>MovementHistory</c>: the feed grows with every construction step of every Structure, and nothing ever shrinks it short of deleting the Game.
    /// </summary>
    public long Id { get; set; }

    public int GameId { get; set; }

    /// <summary>
    /// The Structure the row is about.
    /// </summary>
    /// <remarks>
    /// A <b>real foreign key</b>, unlike <c>MovementHistory.UnitInstanceId</c>. A razed Structure is soft-deleted - its <c>RemovedOnTurnId</c> is stamped and the row stays - so the constraint
    /// never has a delete to refuse.
    /// </remarks>
    public int StructureInstanceId { get; set; }

    /// <summary>
    /// What kind of Structure it is, copied onto the row so the fold can say what was seen, and how far it could see, without joining through the instance.
    /// </summary>
    public int StructureId { get; set; }

    /// <summary>
    /// Who owned the Structure once the event had happened - the new owner, for a <see cref="StructureHistoryEventType.Captured"/> row.
    /// </summary>
    public int PlayerId { get; set; }

    /// <summary>
    /// Who owned the Structure before it was captured. Set on a <see cref="StructureHistoryEventType.Captured"/> row and on no other, which a check constraint enforces.
    /// </summary>
    /// <remarks>
    /// Carried on the row so the fold, and the vision seam, can tell whose observer the Structure was before the capture without reading the Structure's earlier history.
    /// </remarks>
    public int? PreviousOwnerPlayerId { get; set; }

    public StructureHistoryEventType EventType { get; set; }

    /// <summary>
    /// How far built the Structure was once the event had happened, from 0 to 100.
    /// </summary>
    /// <remarks>
    /// Snapshotted per row because the instance only holds the value at the end of the Turn, and a watcher who saw the fifth step of ten saw 50%, not whatever it reached afterwards.
    /// </remarks>
    public double CompletionPercent { get; set; }

    /// <summary>
    /// The tile the Structure stands on, copied for the same reason as <see cref="StructureId"/>.
    /// </summary>
    public int CampaignMapTileId { get; set; }

    /// <summary>
    /// The Turn the event happened during.
    /// </summary>
    public int PlayerTurnId { get; set; }

    /// <summary>
    /// Where the event falls in the Turn's total order, drawn from <c>PlayerTurn.LastAllocatedSequence</c>.
    /// </summary>
    /// <remarks>
    /// The same counter <c>MovementHistory</c> draws from, which is what lets "rode past the village, then razed it" be told apart from the reverse.
    /// </remarks>
    public int TurnSequence { get; set; }
}
