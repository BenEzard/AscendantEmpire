using AscendantEmpire.FrontendContracts.Games.GetGames;

namespace AscendantEmpire.Application.Features.Game.Classes;

public class DTGameSummaryCollection : IGameSummaryCollection
{
    public required IReadOnlyCollection<IGameSummary> Games { get; set; }
}
