namespace AscendantEmpire.FrontendContracts.Player;

public interface IPlayerStructureCollection
{
    IReadOnlyCollection<IPlayerStructure> Structures { get; }
}
