using AscendantEmpire.Domain.Enums.Games;

namespace AscendantEmpire.BackendContracts.Players.GetPlayers;

public record class Player
{
    public int Id { get; init; }
    public int GameId { get; init; }

    public required string RulerTitle { get; init; }
    public required string RulerName { get; init; }
    public required string FactionName { get; init; }

    public PlayerStatus Status { get; init; }

    public int CampaignOffsetId { get; init; }
}