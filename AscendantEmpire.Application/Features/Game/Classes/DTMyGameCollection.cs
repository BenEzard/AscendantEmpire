using AscendantEmpire.FrontendContracts.Games.GetMyGames;

namespace AscendantEmpire.Application.Features.Game.Classes;

public class DTMyGameCollection : IMyGameCollection
{
    public required IReadOnlyCollection<IMyGame> Games { get; set; }
}
