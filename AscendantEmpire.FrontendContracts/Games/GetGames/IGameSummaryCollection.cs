namespace AscendantEmpire.FrontendContracts.Games.GetGames;

/// <summary>
/// The System panel's administrative games listing.
/// </summary>
public interface IGameSummaryCollection
{
    IReadOnlyCollection<IGameSummary> Games { get; }
}
