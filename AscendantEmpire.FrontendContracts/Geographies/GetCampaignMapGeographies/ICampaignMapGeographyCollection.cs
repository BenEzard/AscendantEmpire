namespace AscendantEmpire.FrontendContracts.Geographies.GetCampaignMapGeographies;
public interface ICampaignMapGeographyCollection
{
    IReadOnlyCollection<ICampaignMapGeography> Geographies { get; }
}
