using AscendantEmpire.FrontendContracts.Turns.EndPlayerTurn;

namespace AscendantEmpire.BackendContracts.Turns.EndPlayerTurn;

public record class EndPlayerTurnDto : IEndPlayerTurnDto
{
    public required Guid GameGuid { get; init; }
    public required Guid PlayerGuid { get; init; }
}
