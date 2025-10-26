namespace AscendantEmpire.FrontendContracts.MapGallery.SearchCampaignMapIndex;

public interface ICampaignMapIndexCollection
{
    IReadOnlyCollection<ICampaignMapIndex> Indexes { get; }
}
