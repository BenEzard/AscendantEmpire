using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Domain.Entities.Maps.CampaignMaps;

using MediatR;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.Maps.MapGallery.Handlers;

public record UpdateCampaignMapIndexRequest(
    int? Id,
    string MapName,
    string? Description
) : IRequest<ServiceResultBase>;

internal class UpdateCampaignMapIndexHandler(
    ILogger<UpdateCampaignMapIndexHandler> logger,
    AscendantEmpireContext context
    ) : IRequestHandler<UpdateCampaignMapIndexRequest, ServiceResultBase>
{
    public async Task<ServiceResultBase> Handle(UpdateCampaignMapIndexRequest request, CancellationToken cancellationToken)
    {
        try
        {
            // Normalize inputs — description stored as empty string when null/blank
            string mapName = request.MapName.Trim();
            string description = request.Description?.Trim() ?? string.Empty;

            CampaignMapGalleryIndex? existing = await context.CampaignMapGalleryIndex.FirstOrDefaultAsync(x => x.Id == request.Id!.Value, cancellationToken);

            if (existing is null)
            {
                return NotFound($"Campaign Map with Id {request.Id!.Value} was not found.");
            }

            // Only update name/description; all other fields (dimensions, engine, etc.) are immutable here.
            existing.MapName = mapName;
            existing.Description = description;

            await context.SaveChangesAsync(cancellationToken);

            return Succeed();
        }
        catch (Exception ex)
        {
            const string error = "Error updating Campaign Map Gallery Index record.";
            logger.LogError(ex, error);
            return ServiceResultBase.InternalServerError(error);
        }
    }
}
