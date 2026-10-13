using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Data;
using AscendantEmpire.BackendContracts.MapGallery.SearchCampaignMap;

using Microsoft.EntityFrameworkCore;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.Maps.MapGallery.Handlers;

public record SearchCampaignMapGalleryIndexRequest() : IRequest<ServiceResult<IReadOnlyCollection<CampaignMapIndex>>>;

internal class SearchCampaignMapGalleryIndexHandler(
    AscendantEmpireContext context
) : IRequestHandler<SearchCampaignMapGalleryIndexRequest, ServiceResult<IReadOnlyCollection<CampaignMapIndex>>>
{
    public async Task<ServiceResult<IReadOnlyCollection<CampaignMapIndex>>> Handle(SearchCampaignMapGalleryIndexRequest request, CancellationToken cancellationToken)
    {
        CampaignMapIndex[] indexes = await context.CampaignMapGalleryIndex
            .Select(c => new CampaignMapIndex
            {
                CampaignMapGalleryIndexId = c.Id,
                MapName = c.MapName,
                Description = c.Description,
                Width = c.Width,
                Height = c.Height,
                HorizontalWrap = c.HorizontalWrap,
                VerticalWrap = c.VerticalWrap,
            })
            .ToArrayAsync(cancellationToken);

        return ServiceResult<IReadOnlyCollection<CampaignMapIndex>>.Succeed(indexes);
    }
}
