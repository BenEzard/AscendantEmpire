using AscendantEmpire.Domain.Enums;
using AscendantEmpire.Domain.Enums.Intelligence;
using AscendantEmpire.Domain.Enums.Maps;
using AscendantEmpire.FrontendContracts.Maps;

namespace AscendantEmpire.FrontendContracts.Intelligence;

/// <summary>
/// One entry in a Player's intelligence feed, already stripped down to the fidelity that was earned.
/// </summary>
/// <remarks>
/// <para>
/// Carries no text: the client composes the sentence. The subject fields are null whenever <see cref="Fidelity"/> did not earn them, so a null means "not made out", never "not applicable".
/// </para>
/// <para>
/// See "Reports" and "Fidelity" in <c>docs/design/intelligence-and-sightings.md</c>.
/// </para>
/// </remarks>
public interface ISightingReport
{
    long ReportId { get; }

    PlayerReportType ReportType { get; }

    /// <summary>
    /// The Turn the watched events happened in — the acting Player's, not the reader's.
    /// </summary>
    int RaisedDuringPlayerTurnId { get; }

    int RoundNumber { get; }

    SightingFidelity Fidelity { get; }

    /// <summary>
    /// The tiles the subject was seen on, in order, with consecutive repeats collapsed. Never empty: the first is where it was picked up and the last where it was last seen.
    /// </summary>
    IReadOnlyList<ICoord> Path { get; }

    /// <summary>
    /// The direction the subject was seen leaving in, or null when it never left view.
    /// </summary>
    MoveDirection? ExitDirection { get; }

    bool StillVisible { get; }

    bool AppearedInView { get; }

    bool DestroyedInView { get; }

    int FirstTurnSequence { get; }

    int LastTurnSequence { get; }

    /// <summary>
    /// The reader's (or an ally's) Observer that picked the subject up, read together with <see cref="FirstWitnessRecordType"/>.
    /// </summary>
    int FirstWitnessId { get; }

    RecordType FirstWitnessRecordType { get; }

    int FirstWitnessOwnerPlayerId { get; }

    /// <summary>
    /// What kind of Force it was. Disclosed from <see cref="SightingFidelity.Type"/>.
    /// </summary>
    int? UnitTemplateId { get; }

    /// <summary>
    /// Whose Force it was. Disclosed from <see cref="SightingFidelity.Owner"/>.
    /// </summary>
    int? OwnerPlayerId { get; }

    /// <summary>
    /// Which Force it was, stable across sightings. Disclosed from <see cref="SightingFidelity.Owner"/>.
    /// </summary>
    int? UnitInstanceId { get; }

    /// <summary>
    /// Its unwounded soldiers. Disclosed only at <see cref="SightingFidelity.Detail"/>.
    /// </summary>
    int? HealthyTroops { get; }

    /// <summary>
    /// On a Structure report, whether the Structure was seen being finished; null on any other report.
    /// </summary>
    /// <remarks>
    /// On a Structure report <see cref="AppearedInView"/> means it was seen being founded and <see cref="DestroyedInView"/> that it was seen being razed; on a combat report they mean the
    /// engagement was seen commencing and concluding.
    /// </remarks>
    bool? CompletedInView { get; }

    /// <summary>
    /// On a Structure report, whether the Structure was seen changing hands; null on any other report.
    /// </summary>
    bool? CapturedInView { get; }

    /// <summary>
    /// Which Structure it was, stable across sightings. Disclosed from <see cref="SightingFidelity.Owner"/>.
    /// </summary>
    int? StructureInstanceId { get; }

    /// <summary>
    /// What kind of Structure it was. Disclosed from <see cref="SightingFidelity.Type"/>.
    /// </summary>
    int? StructureId { get; }

    /// <summary>
    /// Who held the Structure before the capture that was seen. Disclosed from <see cref="SightingFidelity.Owner"/>.
    /// </summary>
    int? PreviousOwnerPlayerId { get; }

    /// <summary>
    /// How far built the Structure was, from 0 to 100. Disclosed only at <see cref="SightingFidelity.Detail"/>.
    /// </summary>
    double? CompletionPercent { get; }

    /// <summary>
    /// On a combat report, whether a side was seen joining the engagement; null on any other report.
    /// </summary>
    bool? SideJoinedInView { get; }

    /// <summary>
    /// On a combat report, whether a side was seen leaving it; null on any other report.
    /// </summary>
    bool? SideLeftInView { get; }

    /// <summary>
    /// Which engagement it was, stable across sightings. Disclosed from <see cref="SightingFidelity.Owner"/>.
    /// </summary>
    /// <remarks>
    /// There is deliberately nothing disclosed at <see cref="SightingFidelity.Type"/> for an engagement: unlike a Force or a Structure it has no kind to make out.
    /// </remarks>
    int? BattleId { get; }

    /// <summary>
    /// Who was left holding the ground once the fighting ended, or null when the reader did not see it end - or nobody held it. Disclosed from <see cref="SightingFidelity.Owner"/>.
    /// </summary>
    int? GroundHeldByPlayerId { get; }

    /// <summary>
    /// Troops lost across the departures the reader witnessed. Disclosed only at <see cref="SightingFidelity.Detail"/>.
    /// </summary>
    int? TroopsLost { get; }
}
