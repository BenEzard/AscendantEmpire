using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.FrontendContracts.CampaignMaps;

public interface IObservedCampaignTile
{
    int XCoord { get; }
    int YCoord { get; }
    BiomeType BiomeTypeId { get; }
    CampaignMapGeographyType CampaignMapGeographyTypeId { get; }
    IReadOnlyCollection<IStructureStub>? Structures { get; }
    IReadOnlyCollection<IForceStub>? Forces { get; }
}
