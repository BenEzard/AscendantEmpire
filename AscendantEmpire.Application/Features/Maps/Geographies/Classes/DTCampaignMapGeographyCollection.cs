using AscendantEmpire.FrontendContracts.Geographies.GetCampaignMapGeographies;

namespace AscendantEmpire.Application.Features.Maps.Geographies.Classes;

public class DTCampaignMapGeographyCollection : ICampaignMapGeographyCollection
{
    public required IReadOnlyCollection<ICampaignMapGeography> Geographies { get; set; }
}
