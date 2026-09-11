namespace AscendantEmpire.FrontendContracts.Player;

/// <summary>
/// Every player in the game the caller has entered, whatever their status.
/// </summary>
public interface IGamePlayerCollection
{
    IReadOnlyCollection<IGamePlayer> Players { get; }
}
