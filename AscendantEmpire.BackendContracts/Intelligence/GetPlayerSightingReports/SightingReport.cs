using AscendantEmpire.Domain.Enums;
using AscendantEmpire.Domain.Enums.Intelligence;
using AscendantEmpire.Domain.Enums.Maps;
using AscendantEmpire.FrontendContracts.Intelligence;
using AscendantEmpire.FrontendContracts.Maps;

namespace AscendantEmpire.BackendContracts.Intelligence.GetPlayerSightingReports;

/// <inheritdoc cref="ISightingReport" />
public record SightingReport : ISightingReport
{
    public long ReportId { get; init; }
    public PlayerReportType ReportType { get; init; }
    public int RaisedDuringPlayerTurnId { get; init; }
    public int RoundNumber { get; init; }
    public SightingFidelity Fidelity { get; init; }
    public IReadOnlyList<ICoord> Path { get; init; } = [];
    public MoveDirection? ExitDirection { get; init; }
    public bool StillVisible { get; init; }
    public bool AppearedInView { get; init; }
    public bool DestroyedInView { get; init; }
    public int FirstTurnSequence { get; init; }
    public int LastTurnSequence { get; init; }
    public int FirstWitnessId { get; init; }
    public RecordType FirstWitnessRecordType { get; init; }
    public int FirstWitnessOwnerPlayerId { get; init; }
    public int? UnitTemplateId { get; init; }
    public int? OwnerPlayerId { get; init; }
    public int? UnitInstanceId { get; init; }
    public int? HealthyTroops { get; init; }
    public bool? CompletedInView { get; init; }
    public bool? CapturedInView { get; init; }
    public int? StructureInstanceId { get; init; }
    public int? StructureId { get; init; }
    public int? PreviousOwnerPlayerId { get; init; }
    public double? CompletionPercent { get; init; }
    public bool? SideJoinedInView { get; init; }
    public bool? SideLeftInView { get; init; }
    public int? BattleId { get; init; }
    public int? GroundHeldByPlayerId { get; init; }
    public int? TroopsLost { get; init; }
}
