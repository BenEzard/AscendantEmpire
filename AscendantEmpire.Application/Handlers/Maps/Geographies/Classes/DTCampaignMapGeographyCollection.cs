using AscendantEmpire.FrontendContracts.Geographies.GetCampaignMapGeographies;

namespace AscendantEmpire.Application.Handlers.Maps.Geographies.Classes;

public class DTCampaignMapGeographyCollection : ICampaignMapGeographyCollection
{
    public required IReadOnlyCollection<ICampaignMapGeography> Geographies { get; set; }
}
