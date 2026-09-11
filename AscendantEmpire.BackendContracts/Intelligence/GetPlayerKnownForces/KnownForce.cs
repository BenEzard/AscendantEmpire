using AscendantEmpire.Domain.Enums.Intelligence;
using AscendantEmpire.Domain.Enums.Maps;
using AscendantEmpire.FrontendContracts.Intelligence;
using AscendantEmpire.FrontendContracts.Maps;

namespace AscendantEmpire.BackendContracts.Intelligence.GetPlayerKnownForces;

/// <inheritdoc cref="IKnownForce" />
public record KnownForce : IKnownForce
{
    public KnownForceStatus Status { get; init; }
    public SightingFidelity Fidelity { get; init; }
    public ICoord LastSeenLocation { get; init; } = null!;
    public int LastSeenPlayerTurnId { get; init; }
    public int LastSeenRoundNumber { get; init; }
    public int LastSeenTurnSequence { get; init; }
    public MoveDirection? ExitDirection { get; init; }
    public bool StillVisible { get; init; }
    public int? UnitTemplateId { get; init; }
    public int? OwnerPlayerId { get; init; }
    public int? UnitInstanceId { get; init; }
    public int? HealthyTroops { get; init; }
}
