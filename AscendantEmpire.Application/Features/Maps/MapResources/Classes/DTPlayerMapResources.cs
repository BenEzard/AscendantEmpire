using AscendantEmpire.FrontendContracts.MapResources.GetPlayerMapResources;

namespace AscendantEmpire.Application.Features.Maps.MapResources.Classes;

public class DTPlayerMapResources : IPlayerMapResources
{
    public IReadOnlyCollection<IPlayerMapResourceEntry> Resources { get; set; } = [];
}
