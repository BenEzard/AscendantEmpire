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
}
