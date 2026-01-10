using AscendantEmpire.FrontendContracts.Games.GetOpenGames;

namespace AscendantEmpire.Application.Handlers.Game.Classes;

public class DTOpenGameCollection : IOpenGameCollection
{
    public required IReadOnlyCollection<IOpenGame> Games { get; set; }
}
