using AscendantEmpire.FrontendContracts.Games.GetOpenGames;

namespace AscendantEmpire.Application.Handlers.Game.Classes;

public class DTOpenGame : IOpenGame
{
    /// <inheritdoc />
    public int GameId { get; set; }
    /// <inheritdoc />
    public required string GameName { get; set; }
    /// <inheritdoc />
    public Guid GameGuid { get; set; }
    /// <inheritdoc />
    public int MapWidth { get; set; }
    /// <inheritdoc />
    public int MapHeight { get; set; }
    /// <inheritdoc />
    public required IReadOnlyCollection<DTOpenGamePlayer> Players { get; set; }

    IReadOnlyCollection<IOpenGamePlayer> IOpenGame.Players => Players;
}
