using AscendantEmpire.Domain.Enums.Games;
using AscendantEmpire.FrontendContracts.Games.GetGames;

namespace AscendantEmpire.Application.Features.Game.Classes;

public class DTGameSummary : IGameSummary
{
    /// <inheritdoc />
    public int GameId { get; set; }

    /// <inheritdoc />
    public required string GameName { get; set; }

    /// <inheritdoc />
    public int NumberOfPlayers { get; set; }

    /// <inheritdoc />
    public int ClaimedPlayerCount { get; set; }

    /// <inheritdoc />
    public GameCreationStatus CreationStatus { get; set; }

    /// <inheritdoc />
    public GamePlayStatus PlayStatus { get; set; }

    /// <inheritdoc />
    public DateTimeOffset CreatedAt { get; set; }
}
