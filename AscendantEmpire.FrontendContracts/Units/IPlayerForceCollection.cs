namespace AscendantEmpire.FrontendContracts.Units;

public interface IPlayerForceCollection
{
    IReadOnlyCollection<IPlayerForce> Forces { get; }
}
