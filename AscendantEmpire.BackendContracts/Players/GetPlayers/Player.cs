using AscendantEmpire.Domain.Enums.Games;

namespace AscendantEmpire.BackendContracts.Players.GetPlayers;

public record class Player
{
    public int Id { get; init; }
    public int GameId { get; init; }

    public required string RulerTitle { get; init; }
    public required string RulerName { get; init; }
    public required string FactionName { get; init; }

    /// <summary>
    /// The HTML colour the faction is drawn in, or null while the player has not picked one.
    /// </summary>
    public string? FactionColour { get; init; }

    /// <summary>
    /// Root-relative path to the ruler's portrait, e.g. "/portraits/07_northern_queen.png". Empty only on a slot predating the column.
    /// </summary>
    public required string RulerPortraitPath { get; init; }

    public PlayerStatus Status { get; init; }

    public int CampaignOffsetId { get; init; }
}