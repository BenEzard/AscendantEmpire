using AscendantEmpire.Domain.Enums.Maps;
using AscendantEmpire.FrontendContracts.CampaignMaps;

namespace AscendantEmpire.Application.Handlers.Maps.CampaignMaps.Classes;

public class DTObservedCampaignTile : IObservedCampaignTile
{
    public int XCoord { get; set; }
    public int YCoord { get; set; }
    public BiomeType BiomeTypeId { get; set; }
    public CampaignMapGeographyType CampaignMapGeographyTypeId { get; set; }

    public IReadOnlyCollection<DTStructureStub>? Structures { get; set; }

    public IReadOnlyCollection<DTTroopStub>? Troops { get; set; }

    IReadOnlyCollection<IStructureStub>? IObservedCampaignTile.Structures => Structures;

    IReadOnlyCollection<ITroopStub>? IObservedCampaignTile.Troops => Troops;
}
