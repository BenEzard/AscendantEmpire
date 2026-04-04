using AscendantEmpire.FrontendContracts.Turns.EndPlayerTurn;

namespace AscendantEmpire.BackendContracts.Turns.EndPlayerTurn;

public record class EndPlayerTurnDto : IEndPlayerTurnDto
{
    public required int GameId { get; init; }
    public required int PlayerId { get; init; }
}
