namespace AscendantEmpire.FrontendContracts.Biomes.GetCampaignMapBiomes;

public interface ICampaignMapBiomeCollection
{
    IReadOnlyCollection<ICampaignMapBiome> Biomes { get; }
}
