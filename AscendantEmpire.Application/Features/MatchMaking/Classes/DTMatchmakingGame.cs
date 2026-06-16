using AscendantEmpire.FrontendContracts.MatchMaking;

namespace AscendantEmpire.Application.Features.MatchMaking.Classes;

public class DTMatchmakingGame : IMatchmakingGame
{
    /// <inheritdoc />
    public int GameId { get; set; }

    /// <inheritdoc />
    public required string GameName { get; set; }

    /// <inheritdoc />
    public int NumberOfPlayers { get; set; }

    /// <inheritdoc />
    public int MapWidth { get; set; }

    /// <inheritdoc />
    public int MapHeight { get; set; }

    /// <inheritdoc />
    public required IReadOnlyCollection<DTMatchmakingSlot> Slots { get; set; }

    IReadOnlyCollection<IMatchmakingSlot> IMatchmakingGame.Slots => Slots;
}
