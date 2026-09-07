using AscendantEmpire.FrontendContracts.Games.GetMyGames;

namespace AscendantEmpire.Application.Features.Game.Classes;

public class DTMyGame : IMyGame
{
    /// <inheritdoc />
    public int GameId { get; set; }

    /// <inheritdoc />
    public required string GameName { get; set; }

    /// <inheritdoc />
    public int MapWidth { get; set; }

    /// <inheritdoc />
    public int MapHeight { get; set; }

    /// <inheritdoc />
    public int YourPlayerId { get; set; }

    /// <inheritdoc />
    public required string YourRulerLabel { get; set; }

    /// <inheritdoc />
    public int YourCampaignOffsetId { get; set; }

    /// <inheritdoc />
    public int? CurrentTurnPlayerId { get; set; }

    /// <inheritdoc />
    public string? CurrentTurnRulerLabel { get; set; }

    /// <inheritdoc />
    public bool IsYourTurn { get; set; }
}
