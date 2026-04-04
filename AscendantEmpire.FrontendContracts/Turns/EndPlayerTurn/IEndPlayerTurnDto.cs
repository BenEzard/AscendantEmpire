namespace AscendantEmpire.FrontendContracts.Turns.EndPlayerTurn;

public interface IEndPlayerTurnDto
{
    int GameId { get; }
    int PlayerId { get; }
}
