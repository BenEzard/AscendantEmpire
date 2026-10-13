using AscendantEmpire.FrontendContracts.MatchMaking;

namespace AscendantEmpire.Application.Features.MatchMaking.Classes;

public class DTMatchMakingGame : IMatchMakingGame
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
    public required IReadOnlyCollection<DTMatchMakingSlot> Slots { get; set; }

    /// <inheritdoc />
    public bool HasStarted { get; set; }

    IReadOnlyCollection<IMatchMakingSlot> IMatchMakingGame.Slots => Slots;
}
