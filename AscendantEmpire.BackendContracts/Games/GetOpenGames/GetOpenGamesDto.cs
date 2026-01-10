namespace AscendantEmpire.BackendContracts.Games.GetOpenGames;

public record class GetOpenGamesDto
{
    public Guid? GameGuid { get; init; }
    public Guid? PlayerGuid { get; init; }
}
