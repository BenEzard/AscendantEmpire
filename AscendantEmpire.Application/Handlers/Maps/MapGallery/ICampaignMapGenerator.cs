using AscendantEmpire.Application.Common;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Handlers.Maps.MapGallery;

internal interface ICampaignMapGenerator
{
    ServiceResultBase ValidateRequestProperties(Domain.Entities.Maps.CampaignMaps.CampaignMapGalleryIndex index);
    Task<ServiceResult<int>> GenerateCampaignMapAsync(Domain.Entities.Maps.CampaignMaps.CampaignMapGalleryIndex index);
}
