using AscendantEmpire.Application.Data;
using AscendantEmpire.Domain.Entities.Maps.CampaignMaps;
using AscendantEmpire.Domain.Enums.Maps;
using AscendantEmpire.FrontendContracts.MapGallery;

using MediatR;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Handlers.Maps.MapGallery;

public record AddCampaignMapRequest(
    string MapName,
    string Description,
    int Width,
    int Height,
    MapCreationEngine CreationEngine,
    Dictionary<string, string>? Parameters
) : IRequest<ServiceResult<int>>, ICampaignMapIndexUpsertRequest
{
    public int? Id => null; // satisfies the interface
}

internal class AddCampaignMapHandler(
    ILogger<AddCampaignMapHandler> logger,
    IDbContextFactory<AscendantEmpireContext> dbFactory
    ) : IRequestHandler<AddCampaignMapRequest, ServiceResult<int>>
{
    public async Task<ServiceResult<int>> Handle(AddCampaignMapRequest request, CancellationToken cancellationToken)
    {
        try
        {
            await using var context = await dbFactory.CreateDbContextAsync(cancellationToken);

            // Normalize inputs
            string mapName = request.MapName.Trim();
            string description = request.Description?.Trim() ?? string.Empty;
            string propsJson = request.Parameters != null
                ? System.Text.Json.JsonSerializer.Serialize(request.Parameters)
                : string.Empty;

                int pendingMapsCount = await context.CampaignMapGalleryIndex.CountAsync(x => x.CreationStatus == MapCreationStatus.Pending, cancellationToken);

                var newIndexRecord = new CampaignMapGalleryIndex
                {
                    MapName = mapName,
                    Description = description,
                    Width = request.Width,
                    Height = request.Height,
                    CreationStatus = MapCreationStatus.Pending,
                    CreationEngine = request.CreationEngine,
                    Status = $"Pending creation (#{pendingMapsCount + 1} in queue)",
                    ErrorMessage = string.Empty,
                    PropertiesJson = propsJson,
                    CampaignMapOffsetId = -1, // default until creation completes
                    HorizontalWrap = request.Parameters != null 
                        && request.Parameters.TryGetValue("HorizontalWrap", out var hWrap) 
                        && bool.TryParse(hWrap, out var hWrapBool) ? hWrapBool : false,
                    VerticalWrap = request.Parameters != null 
                        && request.Parameters.TryGetValue("VerticalWrap", out var vWrap) 
                        && bool.TryParse(vWrap, out var vWrapBool) ? vWrapBool : false,
                };

                context.CampaignMapGalleryIndex.Add(newIndexRecord);
                await context.SaveChangesAsync(cancellationToken);

                return ServiceResult<int>.Created(newIndexRecord.Id);
        }
        catch (Exception ex)
        {
            const string error = "Error creating Campaign Map Gallery Index record.";
            logger.LogError(ex, error);
            return ServiceResult<int>.InternalServerError(error);
        }
    }
}
