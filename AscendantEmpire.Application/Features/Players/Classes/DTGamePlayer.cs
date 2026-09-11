using AscendantEmpire.Domain.Enums.Games;
using AscendantEmpire.FrontendContracts.Player;

namespace AscendantEmpire.Application.Features.Players.Classes;

/// <inheritdoc cref="IGamePlayer" />
public class DTGamePlayer : IGamePlayer
{
    public required int PlayerId { get; set; }
    public required string RulerLabel { get; set; }
    public required string FactionName { get; set; }
    public string? FactionColour { get; set; }
    public required PlayerStatus Status { get; set; }
}
