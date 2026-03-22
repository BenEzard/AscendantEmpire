using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.BackendContracts.CampaignMaps.GetPlayerCampaignMap;

public record class ObservedCampaignTile
{
    public int XCoord { get; init; }
    public int YCoord { get; init; }
    public BiomeType BiomeTypeId { get; init; }
    public CampaignMapGeographyType CampaignMapGeographyTypeId { get; init; }
    public IReadOnlyCollection<ObservedStructure> Structures { get; init; }
}
