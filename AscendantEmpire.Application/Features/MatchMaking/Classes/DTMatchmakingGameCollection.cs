using AscendantEmpire.FrontendContracts.MatchMaking;

namespace AscendantEmpire.Application.Features.MatchMaking.Classes;

public class DTMatchmakingGameCollection : IMatchmakingGameCollection
{
    public required IReadOnlyCollection<IMatchmakingGame> Games { get; set; }
}
