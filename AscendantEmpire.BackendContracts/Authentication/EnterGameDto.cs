namespace AscendantEmpire.BackendContracts.Authentication;

public record class EnterGameDto
{
    /// <summary>The game the caller wants to enter. Their player in it is derived server-side.</summary>
    public required int GameId { get; init; }
}
