namespace AscendantEmpire.FrontendContracts.Intelligence;

/// <summary>
/// Every Force a Player has watched, as they last saw it.
/// </summary>
public interface IKnownForceCollection
{
    IReadOnlyCollection<IKnownForce> KnownForces { get; }
}
