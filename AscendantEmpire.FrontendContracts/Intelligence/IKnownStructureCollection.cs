namespace AscendantEmpire.FrontendContracts.Intelligence;

/// <summary>
/// Every Structure a Player has watched change, as they last saw it.
/// </summary>
public interface IKnownStructureCollection
{
    IReadOnlyCollection<IKnownStructure> KnownStructures { get; }
}
