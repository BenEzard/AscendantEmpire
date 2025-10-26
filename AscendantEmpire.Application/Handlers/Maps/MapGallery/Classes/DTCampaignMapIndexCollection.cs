using AscendantEmpire.FrontendContracts.MapGallery.SearchCampaignMapIndex;

namespace AscendantEmpire.Application.Handlers.Maps.MapGallery.Classes;

public class DTCampaignMapIndexCollection : ICampaignMapIndexCollection
{
    public IReadOnlyCollection<ICampaignMapIndex> Indexes { get; set; } = [];
}
