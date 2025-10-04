using AscendantEmpire.FrontendContracts.Biomes.GetCampaignMapBiomes;

namespace AscendantEmpire.Application.Handlers.Maps.Biomes.Classes;

public class DTCampaignMapBiomeCollection : ICampaignMapBiomeCollection
{
    public required IReadOnlyCollection<ICampaignMapBiome> Biomes { get; set; }
}
