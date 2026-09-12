using AscendantEmpire.Domain.Enums;
using AscendantEmpire.Domain.Enums.Intelligence;
using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.Domain.Entities.Games;

/// <summary>
/// One thing a Player learned about the world while they were not looking at it — a single entry in their intelligence feed.
/// </summary>
/// <remarks>
/// <para>
/// Raised by the end-of-turn sightings step for every Player who watched the acting Player do something. See "Reports" in <c>docs/design/intelligence-and-sightings.md</c>.
/// </para>
/// <para>
/// <b>Typed and structured, with no pre-rendered text.</b> A sentence in a column could not be put on a map, filtered, translated, or re-labelled when "Foreigner" becomes "Ally". The client
/// composes the sentence from these fields.
/// </para>
/// <para>
/// <b>Stores the truth, whatever fidelity was earned.</b> <see cref="SubjectUnitInstanceId"/>, the owner, the template and the troops are always the real values. The read projection strips them
/// back to <see cref="Fidelity"/>, which is a disclosure rule rather than a storage rule: anonymous low-fidelity rows could never be joined up with a later, better sighting of the same Force.
/// </para>
/// <para>
/// <b>No read flag.</b> "New since I last looked" is derived from Turn identity: the feed is the reports raised since the reader's last Turn ended.
/// </para>
/// </remarks>
public class PlayerReport
{
    /// <summary>
    /// A <c>bigint</c>, because one Turn can raise a report per watching Player per track per Force.
    /// </summary>
    public long Id { get; set; }

    public int GameId { get; set; }

    /// <summary>
    /// The Player the report is addressed to.
    /// </summary>
    /// <remarks>
    /// One coalition-level track fans out into one report per member Player, so allies sharing vision each get their own row.
    /// </remarks>
    public int RecipientPlayerId { get; set; }

    /// <summary>
    /// The Turn whose events the report describes — the acting Player's Turn, never the recipient's.
    /// </summary>
    /// <remarks>
    /// The feed window compares against this, and the Round the report belongs to is read through it.
    /// </remarks>
    public int RaisedDuringPlayerTurnId { get; set; }

    public PlayerReportType ReportType { get; set; }

    /// <summary>
    /// The Force the report is about, or null for a report with no Force subject — a Structure report or a combat report.
    /// </summary>
    /// <remarks>
    /// Deliberately <b>not</b> a foreign key, for the same reason as <c>MovementHistory.UnitInstanceId</c>: a destroyed Force is hard-deleted, and a foreign key would either refuse that delete or
    /// clear the one column that says which Force this was.
    /// </remarks>
    public int? SubjectUnitInstanceId { get; set; }

    /// <summary>
    /// Whose Force it was — the observed Player's id, never a relationship word.
    /// </summary>
    /// <remarks>
    /// Whether that Player is an enemy, an ally or a stranger is decided at read time, so a report written before an alliance still reads correctly after it.
    /// </remarks>
    public int? SubjectOwnerPlayerId { get; set; }

    /// <summary>
    /// The template the Force was built from, copied off its history row so it survives the Force.
    /// </summary>
    public int? SubjectUnitTemplateId { get; set; }

    /// <summary>
    /// The Force's unwounded soldiers when the sightings step ran, or null when it no longer existed by then.
    /// </summary>
    public int? SubjectHealthyTroops { get; set; }

    /// <summary>
    /// The most any witness of the recipient's coalition made out during the track, which is what the read projection discloses up to.
    /// </summary>
    public SightingFidelity Fidelity { get; set; }

    /// <summary>
    /// Where the coalition picked the Force up. The same as the first entry of <see cref="PathTiles"/>, kept as a column so reports can be queried by place.
    /// </summary>
    public int FirstCampaignMapTileId { get; set; }

    /// <summary>
    /// The last tile the Force was seen on. The same as the last entry of <see cref="PathTiles"/>.
    /// </summary>
    public int LastCampaignMapTileId { get; set; }

    /// <summary>
    /// The Turn sequence of the first step seen, or 0 when the track began with where the Force stood as the Turn opened.
    /// </summary>
    public int FirstTurnSequence { get; set; }

    /// <summary>
    /// The Turn sequence of the last step seen.
    /// </summary>
    public int LastTurnSequence { get; set; }

    /// <summary>
    /// The direction of the step that took the Force out of view, or null when it never left — still in view at the end of the Turn, or destroyed.
    /// </summary>
    /// <remarks>
    /// Earned, not leaked: the watcher genuinely saw it walk off the edge of their vision. Nothing it did afterwards is recorded, so a Force that turned round straight away is still reported as
    /// heading the way it left.
    /// </remarks>
    public MoveDirection? ExitDirection { get; set; }

    /// <summary>
    /// Whether the subject was still in view when the Turn ended - for a Structure, still standing as well; for an engagement, still being fought.
    /// </summary>
    public bool StillVisible { get; set; }

    /// <summary>
    /// Whether the track began with the Force being created in view. On a Structure report, whether the Structure was seen being <b>founded</b>; on a combat report, whether the engagement was
    /// seen <b>commencing</b>.
    /// </summary>
    public bool AppearedInView { get; set; }

    /// <summary>
    /// Whether the track ended with the Force being destroyed in view. On a Structure report, whether the Structure was seen being <b>razed</b>; on a combat report, whether the engagement was
    /// seen <b>concluding</b>.
    /// </summary>
    public bool DestroyedInView { get; set; }

    /// <summary>
    /// The Structure a <see cref="PlayerReportType.StructureSighted"/> report is about, or null on any other report.
    /// </summary>
    /// <remarks>
    /// A real foreign key, unlike <see cref="SubjectUnitInstanceId"/>: a razed Structure is soft-deleted, so the row outlives it. A check constraint allows at most one of the three subjects.
    /// </remarks>
    public int? SubjectStructureInstanceId { get; set; }

    /// <summary>
    /// What kind of Structure it was, or null on a report with no Structure subject.
    /// </summary>
    public int? SubjectStructureId { get; set; }

    /// <summary>
    /// How far built the Structure was at the last event the recipient's coalition witnessed, or null on a report with no Structure subject.
    /// </summary>
    public double? SubjectCompletionPercent { get; set; }

    /// <summary>
    /// Who held the Structure before the first capture the coalition witnessed, or null when no capture was witnessed.
    /// </summary>
    /// <remarks>
    /// <see cref="SubjectOwnerPlayerId"/> is the owner at the last witnessed event, so on a captured Structure the pair says who took it from whom.
    /// </remarks>
    public int? SubjectPreviousOwnerPlayerId { get; set; }

    /// <summary>
    /// Whether the Structure was seen being finished, or null on a report with no Structure subject.
    /// </summary>
    /// <remarks>
    /// Nullable rather than defaulting to false, so adding it to a table of Force reports needed no default constraint - and a Force report has no construction to have seen finish.
    /// </remarks>
    public bool? CompletedInView { get; set; }

    /// <summary>
    /// Whether the Structure was seen changing hands, or null on a report with no Structure subject. Nullable for the same reason as <see cref="CompletedInView"/>.
    /// </summary>
    public bool? CapturedInView { get; set; }

    /// <summary>
    /// The engagement a <see cref="PlayerReportType.CombatSighted"/> report is about, or null on any other report.
    /// </summary>
    /// <remarks>
    /// A real foreign key, for the same reason as <see cref="SubjectStructureInstanceId"/>: a Battle is never hard-deleted, so the constraint never has a delete to refuse. Disclosed no earlier
    /// than <see cref="SightingFidelity.Owner"/>, because it is identity - it is what ties this sighting to every other sighting of the same engagement.
    /// </remarks>
    public int? SubjectBattleId { get; set; }

    /// <summary>
    /// Who was left holding the ground after the conclusion the coalition witnessed, or null when they did not see it end - or saw it end with nobody holding it.
    /// </summary>
    public int? SubjectGroundHeldByPlayerId { get; set; }

    /// <summary>
    /// How many troops were lost across the departures the coalition witnessed, or null when it witnessed none.
    /// </summary>
    /// <remarks>
    /// Summed over only the sides the coalition actually watched leave, so a watcher who saw one side break and then lost sight of the field is told about that side's losses and no more.
    /// Disclosed only at <see cref="SightingFidelity.Detail"/>.
    /// </remarks>
    public int? SubjectTroopsLost { get; set; }

    /// <summary>
    /// Whether a side was seen joining the engagement, or null on a report with no combat subject.
    /// </summary>
    /// <remarks>
    /// Nullable for the same reason as <see cref="CompletedInView"/>: adding it to a table of Force and Structure reports needed no default constraint, and neither of those has an engagement for a
    /// side to have joined.
    /// </remarks>
    public bool? SideJoinedInView { get; set; }

    /// <summary>
    /// Whether a side was seen leaving the engagement, or null on a report with no combat subject. Nullable for the same reason as <see cref="SideJoinedInView"/>.
    /// </summary>
    public bool? SideLeftInView { get; set; }

    /// <summary>
    /// Which of the Force's tracks this is, for this recipient in this Turn, counting from 0 after the track cap has been applied.
    /// </summary>
    /// <remarks>
    /// Part of the unique key that stops a re-run of the sightings step raising the same report twice.
    /// </remarks>
    public int TrackOrdinal { get; set; }

    /// <summary>
    /// The id of the nearest Observer that picked the Force up, read together with <see cref="FirstWitnessRecordType"/>.
    /// </summary>
    /// <remarks>
    /// "Your sentry at Redhill reports". Under shared vision the witness may belong to an ally, which is why its owner is recorded as well.
    /// </remarks>
    public int FirstWitnessId { get; set; }

    public RecordType FirstWitnessRecordType { get; set; }

    public int FirstWitnessOwnerPlayerId { get; set; }

    /// <summary>
    /// The tiles the Force was seen on, in order, with consecutive repeats collapsed.
    /// </summary>
    public List<PlayerReportPathTile> PathTiles { get; set; } = [];
}
