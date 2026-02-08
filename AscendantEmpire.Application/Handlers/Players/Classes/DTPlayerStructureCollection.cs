using AscendantEmpire.FrontendContracts.Player;

namespace AscendantEmpire.Application.Handlers.Players.Classes;

public class DTPlayerStructureCollection : IPlayerStructuresCollection
{
    public required IReadOnlyCollection<IPlayerStructure> Structures { get; set; }
}
