using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Domain.Entities.Maps.CampaignMaps;
using AscendantEmpire.Domain.Enums.Maps;
using AscendantEmpire.FrontendContracts.MapGallery;

using MediatR;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.Maps.MapGallery.Handlers;

public record UpdateCampaignMapIndexRequest(
    int? Id,
    string MapName,
    string Description,
    int Width,
    int Height,
    MapCreationEngine CreationEngine,
    Dictionary<string, string>? Parameters
) : IRequest<ServiceResultBase>, ICampaignMapIndexUpsertRequest;

internal class UpdateCampaignMapIndexHandler(
    ILogger<UpdateCampaignMapIndexHandler> logger,
    AscendantEmpireContext context
    ) : IRequestHandler<UpdateCampaignMapIndexRequest, ServiceResultBase>
{
    public async Task<ServiceResultBase> Handle(UpdateCampaignMapIndexRequest request, CancellationToken cancellationToken)
    {
        try
        {
            // Normalize inputs
            string mapName = request.MapName.Trim();
            string description = request.Description?.Trim() ?? string.Empty;
            string propsJson = request.Parameters != null
                ? System.Text.Json.JsonSerializer.Serialize(request.Parameters)
                : string.Empty;

            CampaignMapGalleryIndex? existing = await context.CampaignMapGalleryIndex.FirstOrDefaultAsync(x => x.Id == request.Id.Value, cancellationToken);

            if (existing is null)
            {
                return NotFound($"Campaign Map with Id {request.Id.Value} was not found.");
            }
            else if (existing.CreationStatus != MapCreationStatus.Pending)
            {
                return Fail("Only maps with 'Pending' status can be updated.");
            }

            // Only update mutable fields; preserve status-related fields
            existing.MapName = mapName;
            existing.Description = description;
            existing.Width = request.Width;
            existing.Height = request.Height;
            existing.CreationEngine = request.CreationEngine;
            existing.PropertiesJson = propsJson;

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
