using AscendantEmpire.FrontendContracts.Player;

namespace AscendantEmpire.Application.Features.Players.Classes;

public class DTPlayerStructureCollection : IPlayerStructureCollection
{
    public required IReadOnlyCollection<IPlayerStructure> Structures { get; set; }
}
