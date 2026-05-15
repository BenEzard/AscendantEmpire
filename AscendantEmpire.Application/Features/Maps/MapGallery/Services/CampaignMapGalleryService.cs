using AscendantEmpire.Application.Data;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.Maps.MapGallery.Services;

internal class CampaignMapGalleryService(
    ILogger<CampaignMapGalleryService> logger,
    IDbContextFactory<AscendantEmpireContext> dbFactory
) : ICampaignMapGalleryService
{
    public async Task<ServiceResult<CampaignMapGalleryProperties>> GetCampaignMapGalleryProperties(int campaignMapGalleryIndexId, CancellationToken cancellationToken)
    {
        try
        {
            if (campaignMapGalleryIndexId <= 0)
            {
                const string error = "Campaign Map Gallery Index Id is required.";
                return ServiceResult<CampaignMapGalleryProperties>.Fail(error);
            }

            await using AscendantEmpireContext context = await dbFactory.CreateDbContextAsync(cancellationToken);

            CampaignMapGalleryProperties? properties = await context.CampaignMapGalleryIndex
                .Where(m => m.Id == campaignMapGalleryIndexId)
                .Select(m => new CampaignMapGalleryProperties
                {
                    Id = m.Id,
                    MapName = m.MapName,
                    Width = m.Width,
                    Height = m.Height,
                    CreationStatus = m.CreationStatus,
                    CampaignMapOffsetId = m.CampaignMapOffsetId,
                    HorizontalWrap = m.HorizontalWrap,
                    VerticalWrap = m.VerticalWrap
                })
                .SingleOrDefaultAsync(cancellationToken);

            if (properties is null)
            {
                string error = $"Campaign Map Gallery Index Id {campaignMapGalleryIndexId} was not found.";
                logger.LogWarning("{Error}", error);
                return ServiceResult<CampaignMapGalleryProperties>.NotFound(error);
            }

            return ServiceResult<CampaignMapGalleryProperties>.Succeed(properties);
        }
        catch (Exception ex)
        {
            const string error = "Unhandled exception getting Campaign Map Gallery properties.";
            logger.LogError(ex, error);
            return ServiceResult<CampaignMapGalleryProperties>.Fail([error, ex.Message]);
        }
    }
}
