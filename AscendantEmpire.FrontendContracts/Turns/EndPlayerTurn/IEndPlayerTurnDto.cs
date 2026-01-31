namespace AscendantEmpire.FrontendContracts.Turns.EndPlayerTurn;

public interface IEndPlayerTurnDto
{
    Guid GameGuid { get; }
    Guid PlayerGuid { get; }
}
