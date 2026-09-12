using AscendantEmpire.Domain.Enums.Intelligence;
using AscendantEmpire.Domain.Enums.Maps;
using AscendantEmpire.FrontendContracts.Intelligence;
using AscendantEmpire.FrontendContracts.Maps;

namespace AscendantEmpire.BackendContracts.Intelligence.GetPlayerKnownStructures;

/// <inheritdoc cref="IKnownStructure" />
public record KnownStructure : IKnownStructure
{
    public KnownStructureStatus Status { get; init; }
    public SightingFidelity Fidelity { get; init; }
    public ICoord Location { get; init; } = null!;
    public int LastSeenPlayerTurnId { get; init; }
    public int LastSeenRoundNumber { get; init; }
    public int LastSeenTurnSequence { get; init; }
    public bool StillVisible { get; init; }
    public int? StructureId { get; init; }
    public int? OwnerPlayerId { get; init; }
    public int? StructureInstanceId { get; init; }
    public double? CompletionPercent { get; init; }
}
