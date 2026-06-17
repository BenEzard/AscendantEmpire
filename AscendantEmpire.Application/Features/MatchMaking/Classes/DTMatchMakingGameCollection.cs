using AscendantEmpire.FrontendContracts.MatchMaking;

namespace AscendantEmpire.Application.Features.MatchMaking.Classes;

public class DTMatchMakingGameCollection : IMatchMakingGameCollection
{
    public required IReadOnlyCollection<IMatchMakingGame> Games { get; set; }
}
