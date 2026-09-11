using AscendantEmpire.Domain.Enums.Forces;

namespace AscendantEmpire.Domain.Entities.Units;

/// <summary>
/// One append-only record of a Force appearing, moving or being destroyed, stamped with the Turn and the sequence it happened at.
/// </summary>
/// <remarks>
/// <para>
/// The first of the raw feeds in <c>docs/design/intelligence-and-sightings.md</c>. It is a fact about the world, not about anybody's perception of it: written whether or not anybody was
/// watching, with the witness question asked later by the end-of-turn fold.
/// </para>
/// <para>
/// <b>Written only through <c>IForceLifecycleWriter</c>.</b> That is the point of the table. Appending history at each call site instead would eventually miss one, and a missed path fails
/// silently, as intelligence that simply never arrives.
/// </para>
/// <para>
/// <b>Every row belongs to a Turn.</b> A Force placed outside any Turn - the starting armies at game setup, or a developer spawn into a Game with no open Turn - gets no row at all. Nobody could
/// have watched it appear, and there is no Turn to stamp it with. The first move such a Force makes still says where it started, through <see cref="FromCampaignMapTileId"/>.
/// </para>
/// <para>
/// <b>Not deduplicated by tile.</b> Collapsing repeated tiles would destroy the ordering an exit direction is computed from, and a Force may legitimately re-enter a tile it has already left.
/// Collapsing is a presentation concern for the track layer.
/// </para>
/// <para>
/// <b>No health or strength snapshot.</b> What an observer perceived belongs on the sighting, at the fidelity that earned it, rather than bloating the highest-volume table in the design.
/// </para>
/// <para>
/// <b>No RowVersion.</b> Nothing ever updates a row, so there is no concurrent edit for a token to detect.
/// </para>
/// </remarks>
public class MovementHistory
{
    /// <summary>
    /// A <c>bigint</c>, unlike the <c>int</c> keys around it, because this is the one table in the schema that grows with every step every Force takes.
    /// </summary>
    public long Id { get; set; }

    public int GameId { get; set; }

    /// <summary>
    /// The Force the row is about.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Deliberately <b>not</b> a foreign key. A destroyed Force is hard-deleted, and the end-of-turn fold runs after attrition, so a Force that was watched and then died in the same Turn reaches
    /// the fold with its <c>UnitInstances</c> row already gone. A foreign key would either refuse that delete or clear this column, and a cleared column would leave the fold unable to tell one
    /// dead Force's path from another's.
    /// </para>
    /// <para>
    /// Safe to hold without the constraint because a Unit id is never reused while any row here survives: the identity counter is only reseeded once the last Game - and with it every row in
    /// this table - has been deleted.
    /// </para>
    /// </remarks>
    public int UnitInstanceId { get; set; }

    /// <summary>
    /// Who owned the Force when the event happened.
    /// </summary>
    /// <remarks>
    /// Copied onto the row rather than joined through the Force, so the row still says whose Force it was after the Force itself has been deleted.
    /// </remarks>
    public int PlayerId { get; set; }

    /// <summary>
    /// The template the Force was built from, copied for the same reason as <see cref="PlayerId"/>.
    /// </summary>
    /// <remarks>
    /// Identity rather than condition: it lets a later fold say what kind of thing was seen, and how far a destroyed observer could see, without needing the Force to still exist.
    /// </remarks>
    public int UnitTemplateId { get; set; }

    public ForceHistoryEventType EventType { get; set; }

    /// <summary>
    /// The tile the Force stepped off. Set on a <see cref="ForceHistoryEventType.Moved"/> row and on no other, which a check constraint enforces.
    /// </summary>
    /// <remarks>
    /// Carried on the row rather than read off the Force's previous one, so the fold only ever has to read the current Turn's rows. Without it, the first step a Force takes in a Turn would need
    /// its last row from some earlier Turn - possibly many Turns back, and absent altogether for a starting Force that has never moved before.
    /// </remarks>
    public int? FromCampaignMapTileId { get; set; }

    /// <summary>
    /// The tile the Force entered, was created on, or was destroyed on.
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
    /// <para>
    /// Every raw feed draws from the same counter, which is what lets "rode past the village, then razed it" be told apart from the reverse.
    /// </para>
    /// <para>
    /// <b>Not unique within a Turn.</b> Forces created together in one batch share a sequence, because they appeared at the same moment - and the members of a Command will too, once Commands
    /// can move.
    /// </para>
    /// </remarks>
    public int TurnSequence { get; set; }
}
