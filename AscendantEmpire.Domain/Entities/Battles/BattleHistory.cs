using AscendantEmpire.Domain.Enums.Battle;

namespace AscendantEmpire.Domain.Entities.Battles;

/// <summary>
/// One append-only record of an engagement commencing, gaining or losing a side, or concluding, stamped with the Turn and the sequence it happened at.
/// </summary>
/// <remarks>
/// <para>
/// The third of the raw feeds in <c>docs/design/intelligence-and-sightings.md</c>, beside <c>MovementHistory</c> and <c>StructureHistory</c>. It is a fact about the world, not about anybody's
/// perception of it: written whether or not anybody was watching, with the witness question asked later by the fold.
/// </para>
/// <para>
/// <b>Only what a campaign map observer could perceive.</b> The battle map's blow-by-blow is deliberately absent - a watcher on the next hill sees that fighting broke out, that another banner
/// arrived, that a side gave way, and whose banners were left on the ground. They do not see the tactical detail.
/// </para>
/// <para>
/// <b>Written only through <c>IBattleLifecycleWriter</c>.</b> That is the point of the table, exactly as it is for the other two feeds: appending history at each call site instead would eventually
/// miss one, and a missed path fails silently, as intelligence that simply never arrives.
/// </para>
/// <para>
/// <b>Every row belongs to a Turn.</b> A Battle exists in a lane of its own and may run outside any campaign Turn, but a row is only ever written against an open Turn, because a sighting has to
/// be placed in the Turn's total order to be interleaved with the other feeds.
/// </para>
/// <para>
/// <b>No RowVersion.</b> Nothing ever updates a row, so there is no concurrent edit for a token to detect.
/// </para>
/// </remarks>
public class BattleHistory
{
    /// <summary>
    /// A <c>bigint</c>, matching the other two feeds, since nothing ever shrinks them short of deleting the Game.
    /// </summary>
    public long Id { get; set; }

    public int GameId { get; set; }

    /// <summary>
    /// The engagement the row is about.
    /// </summary>
    /// <remarks>
    /// A <b>real foreign key</b>, unlike <c>MovementHistory.UnitInstanceId</c>. Concluding a Battle stamps its <c>ConcludedAtRoundNumber</c> and the row stays; the only hard delete is the game
    /// purge, which removes this feed before the Battles it names.
    /// </remarks>
    public int BattleId { get; set; }

    public BattleHistoryEventType EventType { get; set; }

    /// <summary>
    /// The side the row is about. Set on a <see cref="BattleHistoryEventType.ParticipantJoined"/> or <see cref="BattleHistoryEventType.ParticipantLeft"/> row and on no other, which a check
    /// constraint enforces.
    /// </summary>
    /// <remarks>
    /// Carried on the row so that the feed says which side joined or left without reading the Battle's roster, which only holds the position as it stands now.
    /// </remarks>
    public int? ParticipantPlayerId { get; set; }

    /// <summary>
    /// Why that side left. Set on a <see cref="BattleHistoryEventType.ParticipantLeft"/> row and on no other, which a check constraint enforces.
    /// </summary>
    /// <remarks>
    /// <b>Nullable rather than defaulted,</b> because <see cref="BattleExitReason.NotApplicable"/> is a real member sitting on zero, so the column has no spare value to mean "not set".
    /// <para>
    /// No report column reads it yet. It is recorded because the feed is a fact about the world, and "routed" against "negotiated a ceasefire" is intelligence a richer sentence or the fidelity
    /// curve will want.
    /// </para>
    /// </remarks>
    public BattleExitReason? ExitReason { get; set; }

    /// <summary>
    /// How many troops that side lost over its time in the engagement. Set on a <see cref="BattleHistoryEventType.ParticipantLeft"/> row and on no other, which a check constraint enforces.
    /// </summary>
    /// <remarks>
    /// Per side rather than a battle-wide total on the <see cref="BattleHistoryEventType.Concluded"/> row, so that the fold can sum only the losses a given coalition actually watched happen. A
    /// coalition that saw one side break and not the other reports that side's losses and no more, which is the rule the whole feature turns on.
    /// </remarks>
    public int? TroopsLost { get; set; }

    /// <summary>
    /// Who was left holding the tile once the engagement ended, or null when nobody was. Set on a <see cref="BattleHistoryEventType.Concluded"/> row and on no other.
    /// </summary>
    /// <remarks>
    /// Snapshotted here rather than read from <c>CampaignMap.OwnedByPlayerId</c> at fold time, for the same reason <c>StructureHistory.CompletionPercent</c> is snapshotted: that column is
    /// the position now, and the fold is re-runnable, so a re-run after the tile changed hands again would give a different answer to the same question.
    /// <para>
    /// Deliberately not the victor. <c>Battle.VictorId</c> already holds that, never changes after the conclusion, and is not what a distant observer perceives - whose banners are on the ground
    /// afterwards is.
    /// </para>
    /// </remarks>
    public int? GroundHeldByPlayerId { get; set; }

    /// <summary>
    /// The campaign map tile the engagement is being fought over, copied onto the row so the fold can place a probe without joining through the Battle.
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
    /// The same counter the other two feeds draw from, which is what lets "rode past the village, then the fighting started" be told apart from the reverse.
    /// </remarks>
    public int TurnSequence { get; set; }
}
