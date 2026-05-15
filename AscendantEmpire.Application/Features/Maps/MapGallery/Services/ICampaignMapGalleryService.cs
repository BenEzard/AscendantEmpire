using AscendantEmpire.Application.Common;

namespace AscendantEmpire.Application.Features.Maps.MapGallery.Services;

public interface ICampaignMapGalleryService
{
    Task<ServiceResultBase.ServiceResult<CampaignMapGalleryProperties>> GetCampaignMapGalleryProperties(int campaignMapGalleryIndexId, CancellationToken cancellationToken);
}
