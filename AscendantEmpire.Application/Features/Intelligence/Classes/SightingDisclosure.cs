using AscendantEmpire.BackendContracts.CampaignMaps;
using AscendantEmpire.BackendContracts.Intelligence.GetPlayerKnownForces;
using AscendantEmpire.BackendContracts.Intelligence.GetPlayerKnownStructures;
using AscendantEmpire.BackendContracts.Intelligence.GetPlayerSightingReports;
using AscendantEmpire.Domain.Enums;
using AscendantEmpire.Domain.Enums.Intelligence;
using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.Application.Features.Intelligence.Classes;

/// <summary>
/// The truth about a sighting's subject, as stored — or, once disclosed, as much of it as the fidelity earned.
/// </summary>
/// <remarks>
/// One shape for all three kinds of subject. A Force sighting fills the Unit fields, a Structure sighting the Structure fields and a combat sighting the Battle fields;
/// <see cref="OwnerPlayerId"/> is shared between the first two, and whatever does not apply stays null.
/// </remarks>
internal sealed record SightingSubject
{
    public int? UnitInstanceId { get; init; }

    public int? OwnerPlayerId { get; init; }

    public int? UnitTemplateId { get; init; }

    public int? HealthyTroops { get; init; }

    public int? StructureInstanceId { get; init; }

    /// <summary>
    /// What kind of Structure it is.
    /// </summary>
    public int? StructureId { get; init; }

    /// <summary>
    /// Who held the Structure before a capture that was seen.
    /// </summary>
    public int? PreviousOwnerPlayerId { get; init; }

    /// <summary>
    /// How far built the Structure was.
    /// </summary>
    public double? CompletionPercent { get; init; }

    /// <summary>
    /// Which engagement it was.
    /// </summary>
    public int? BattleId { get; init; }

    /// <summary>
    /// Who was left holding the ground after a conclusion that was seen.
    /// </summary>
    public int? GroundHeldByPlayerId { get; init; }

    /// <summary>
    /// What was lost across the departures that were seen.
    /// </summary>
    public int? TroopsLost { get; init; }
}

/// <summary>
/// A <c>PlayerReport</c> read back in full, before anything is stripped from it. Never leaves the application layer.
/// </summary>
internal sealed record StoredSightingReport
{
    public required long ReportId { get; init; }

    public required PlayerReportType ReportType { get; init; }

    public required int RaisedDuringPlayerTurnId { get; init; }

    public required int RoundNumber { get; init; }

    public required SightingFidelity Fidelity { get; init; }

    public required IReadOnlyList<Coord> Path { get; init; }

    public required MoveDirection? ExitDirection { get; init; }

    public required bool StillVisible { get; init; }

    public required bool AppearedInView { get; init; }

    public required bool DestroyedInView { get; init; }

    /// <summary>
    /// Whether a Structure was seen being finished; null on a report with no Structure subject.
    /// </summary>
    public bool? CompletedInView { get; init; }

    /// <summary>
    /// Whether a Structure was seen changing hands; null on a report with no Structure subject.
    /// </summary>
    public bool? CapturedInView { get; init; }

    /// <summary>
    /// Whether a side was seen joining an engagement; null on a report with no combat subject.
    /// </summary>
    public bool? SideJoinedInView { get; init; }

    /// <summary>
    /// Whether a side was seen leaving one; null on a report with no combat subject.
    /// </summary>
    public bool? SideLeftInView { get; init; }

    public required int FirstTurnSequence { get; init; }

    public required int LastTurnSequence { get; init; }

    public required int FirstWitnessId { get; init; }

    public required RecordType FirstWitnessRecordType { get; init; }

    public required int FirstWitnessOwnerPlayerId { get; init; }

    public required SightingSubject Subject { get; init; }
}

/// <summary>
/// A <c>PlayerKnownForce</c> read back in full, before anything is stripped from it. Never leaves the application layer.
/// </summary>
internal sealed record StoredKnownForce
{
    public required KnownForceStatus Status { get; init; }

    public required SightingFidelity Fidelity { get; init; }

    public required Coord LastSeenLocation { get; init; }

    public required int LastSeenPlayerTurnId { get; init; }

    public required int LastSeenRoundNumber { get; init; }

    public required int LastSeenTurnSequence { get; init; }

    public required MoveDirection? ExitDirection { get; init; }

    public required bool StillVisible { get; init; }

    public required SightingSubject Subject { get; init; }
}

/// <summary>
/// A <c>PlayerKnownStructure</c> read back in full, before anything is stripped from it. Never leaves the application layer.
/// </summary>
internal sealed record StoredKnownStructure
{
    public required KnownStructureStatus Status { get; init; }

    public required SightingFidelity Fidelity { get; init; }

    public required Coord Location { get; init; }

    public required int LastSeenPlayerTurnId { get; init; }

    public required int LastSeenRoundNumber { get; init; }

    public required int LastSeenTurnSequence { get; init; }

    public required bool StillVisible { get; init; }

    public required SightingSubject Subject { get; init; }
}

/// <summary>
/// The one place a stored sighting is stripped back to what its fidelity earned, for the report feed and both map layers.
/// </summary>
/// <remarks>
/// <para>
/// <b>Security-relevant.</b> Sightings store the truth, so a slip here hands a Player the identity of a Force they only glimpsed, or the owner of a Structure they could barely make out. Every read
/// that shows a sighting to a Player goes through this class, and its rules are tested directly rather than through a handler. See "Fidelity is a projection rule, not a storage rule" in
/// <c>docs/design/intelligence-and-sightings.md</c>.
/// </para>
/// <para>
/// What each level adds to the one below:
/// <list type="table">
///   <item><term><see cref="SightingFidelity.Movement"/></term><description>where and when: the path or tile, the exit direction, still-visible, and which events were seen.</description></item>
///   <item><term><see cref="SightingFidelity.Type"/></term><description>what kind: a Force's template, or a Structure's kind.</description></item>
///   <item><term><see cref="SightingFidelity.Owner"/></term><description>whose it was and which one it was - for a Structure, who held it before a capture; for an engagement, which
///   Battle it was and who was left holding the ground.</description></item>
///   <item><term><see cref="SightingFidelity.Detail"/></term><description>its condition: a Force's strength, how far a Structure is built, or what an engagement cost.</description></item>
/// </list>
/// </para>
/// <para>
/// A Structure's kind is disclosed at Type without the owner risk a Force's template carries: Structure kinds are a global catalogue, not something one Player owns.
/// </para>
/// <para>
/// <b>Type adds nothing to a combat report,</b> and that is deliberate rather than an omission: an engagement has no kind. A Force has a template and a Structure a catalogue entry; there is no
/// third thing here to identify. A glimpse of a battle is "fighting happened at (X, Y)", and stays that until Owner names the ground-holder.
/// </para>
/// <para>
/// <b>Who fought is not disclosed at all.</b> The campaign-map-visible list in the design is began, ended, who held the ground, and at Detail the losses - the roster is not on it, and a flat
/// report row could not hold an N-sided one without lying about a three-way battle.
/// </para>
/// <para>
/// <b>Fails closed.</b> A fidelity that is not one of the defined levels discloses nothing about the subject at all.
/// </para>
/// </remarks>
internal static class SightingDisclosure
{
    /// <summary>
    /// Strips a subject back to what <paramref name="fidelity"/> earned. Anything not earned comes back null.
    /// </summary>
    internal static SightingSubject DiscloseSubject(SightingFidelity fidelity, SightingSubject truth)
    {
        ArgumentNullException.ThrowIfNull(truth);

        // Checked first and on its own, because the comparisons below are ordinal: an out-of-range value above Detail would otherwise pass every one of them.
        if (Enum.IsDefined(fidelity) == false)
        {
            return new SightingSubject();
        }

        bool earnsType = fidelity >= SightingFidelity.Type;
        bool earnsOwner = fidelity >= SightingFidelity.Owner;
        bool earnsDetail = fidelity >= SightingFidelity.Detail;

        return new SightingSubject
        {
            UnitTemplateId = earnsType ? truth.UnitTemplateId : null,
            StructureId = earnsType ? truth.StructureId : null,
            OwnerPlayerId = earnsOwner ? truth.OwnerPlayerId : null,
            // Which Force or Structure it was is identity, and ties this sighting to every other of the same one, so it is disclosed no earlier than whose it was.
            UnitInstanceId = earnsOwner ? truth.UnitInstanceId : null,
            StructureInstanceId = earnsOwner ? truth.StructureInstanceId : null,
            // Who it was taken from is ownership just as much as who holds it now.
            PreviousOwnerPlayerId = earnsOwner ? truth.PreviousOwnerPlayerId : null,
            BattleId = earnsOwner ? truth.BattleId : null,
            // Who holds the ground afterwards names a Player, which is ownership disclosure whatever else it is.
            GroundHeldByPlayerId = earnsOwner ? truth.GroundHeldByPlayerId : null,
            HealthyTroops = earnsDetail ? truth.HealthyTroops : null,
            CompletionPercent = earnsDetail ? truth.CompletionPercent : null,
            TroopsLost = earnsDetail ? truth.TroopsLost : null
        };
    }

    /// <summary>
    /// The report as its recipient may see it.
    /// </summary>
    internal static SightingReport Disclose(StoredSightingReport stored)
    {
        ArgumentNullException.ThrowIfNull(stored);

        SightingSubject disclosed = DiscloseSubject(stored.Fidelity, stored.Subject);

        return new SightingReport
        {
            ReportId = stored.ReportId,
            ReportType = stored.ReportType,
            RaisedDuringPlayerTurnId = stored.RaisedDuringPlayerTurnId,
            RoundNumber = stored.RoundNumber,
            Fidelity = stored.Fidelity,
            Path = [.. stored.Path],
            ExitDirection = stored.ExitDirection,
            StillVisible = stored.StillVisible,
            // Which events were seen is what a glimpse genuinely saw happen, like appearing or being destroyed, so the flags pass through at every level.
            AppearedInView = stored.AppearedInView,
            DestroyedInView = stored.DestroyedInView,
            CompletedInView = stored.CompletedInView,
            CapturedInView = stored.CapturedInView,
            SideJoinedInView = stored.SideJoinedInView,
            SideLeftInView = stored.SideLeftInView,
            FirstTurnSequence = stored.FirstTurnSequence,
            LastTurnSequence = stored.LastTurnSequence,
            // The witness is the reader's own Observer, or an ally's they share vision with, so there is nothing about it to withhold.
            FirstWitnessId = stored.FirstWitnessId,
            FirstWitnessRecordType = stored.FirstWitnessRecordType,
            FirstWitnessOwnerPlayerId = stored.FirstWitnessOwnerPlayerId,
            UnitTemplateId = disclosed.UnitTemplateId,
            OwnerPlayerId = disclosed.OwnerPlayerId,
            UnitInstanceId = disclosed.UnitInstanceId,
            HealthyTroops = disclosed.HealthyTroops,
            StructureInstanceId = disclosed.StructureInstanceId,
            StructureId = disclosed.StructureId,
            PreviousOwnerPlayerId = disclosed.PreviousOwnerPlayerId,
            CompletionPercent = disclosed.CompletionPercent,
            BattleId = disclosed.BattleId,
            GroundHeldByPlayerId = disclosed.GroundHeldByPlayerId,
            TroopsLost = disclosed.TroopsLost
        };
    }

    /// <summary>
    /// The known Force as the Player who knows it may see it.
    /// </summary>
    internal static KnownForce Disclose(StoredKnownForce stored)
    {
        ArgumentNullException.ThrowIfNull(stored);

        SightingSubject disclosed = DiscloseSubject(stored.Fidelity, stored.Subject);

        return new KnownForce
        {
            Status = stored.Status,
            Fidelity = stored.Fidelity,
            LastSeenLocation = stored.LastSeenLocation,
            LastSeenPlayerTurnId = stored.LastSeenPlayerTurnId,
            LastSeenRoundNumber = stored.LastSeenRoundNumber,
            LastSeenTurnSequence = stored.LastSeenTurnSequence,
            ExitDirection = stored.ExitDirection,
            StillVisible = stored.StillVisible,
            UnitTemplateId = disclosed.UnitTemplateId,
            OwnerPlayerId = disclosed.OwnerPlayerId,
            UnitInstanceId = disclosed.UnitInstanceId,
            HealthyTroops = disclosed.HealthyTroops
        };
    }

    /// <summary>
    /// The known Structure as the Player who knows it may see it.
    /// </summary>
    internal static KnownStructure Disclose(StoredKnownStructure stored)
    {
        ArgumentNullException.ThrowIfNull(stored);

        SightingSubject disclosed = DiscloseSubject(stored.Fidelity, stored.Subject);

        return new KnownStructure
        {
            Status = stored.Status,
            Fidelity = stored.Fidelity,
            Location = stored.Location,
            LastSeenPlayerTurnId = stored.LastSeenPlayerTurnId,
            LastSeenRoundNumber = stored.LastSeenRoundNumber,
            LastSeenTurnSequence = stored.LastSeenTurnSequence,
            StillVisible = stored.StillVisible,
            StructureId = disclosed.StructureId,
            OwnerPlayerId = disclosed.OwnerPlayerId,
            StructureInstanceId = disclosed.StructureInstanceId,
            CompletionPercent = disclosed.CompletionPercent
        };
    }
}
