namespace AscendantEmpire.FrontendContracts.Games.GetOpenGames;

public interface IOpenGameCollection
{
    IReadOnlyCollection<IOpenGame> Games { get; }
}
