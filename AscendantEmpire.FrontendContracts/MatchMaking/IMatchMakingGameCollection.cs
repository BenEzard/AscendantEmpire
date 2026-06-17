namespace AscendantEmpire.FrontendContracts.MatchMaking;

/// <summary>
/// The set of joinable games returned to the matchmaker UI.
/// </summary>
public interface IMatchMakingGameCollection
{
    IReadOnlyCollection<IMatchMakingGame> Games { get; }
}
