using AscendantEmpire.FrontendContracts.MapGallery.SearchCampaignMapIndex;

namespace AscendantEmpire.Application.Features.Maps.MapGallery.Classes;

public class DTCampaignMapIndexCollection : ICampaignMapIndexCollection
{
    public IReadOnlyCollection<ICampaignMapIndex> Indexes { get; set; } = [];
}
