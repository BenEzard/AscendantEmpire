namespace AscendantEmpire.FrontendContracts.Games.GetMyGames;

/// <summary>
/// The set of the caller's own games returned to the Open Game dialog.
/// </summary>
public interface IMyGameCollection
{
    IReadOnlyCollection<IMyGame> Games { get; }
}
