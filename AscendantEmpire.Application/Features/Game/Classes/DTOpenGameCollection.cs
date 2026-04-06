using AscendantEmpire.FrontendContracts.Games.GetOpenGames;

namespace AscendantEmpire.Application.Features.Game.Classes;

public class DTOpenGameCollection : IOpenGameCollection
{
    public required IReadOnlyCollection<IOpenGame> Games { get; set; }
}
