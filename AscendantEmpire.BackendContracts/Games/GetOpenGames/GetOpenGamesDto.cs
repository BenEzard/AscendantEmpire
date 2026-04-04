namespace AscendantEmpire.BackendContracts.Games.GetOpenGames;

public record class GetOpenGamesDto
{
    public int? GameId { get; init; }
    public int? PlayerId { get; init; }
}
