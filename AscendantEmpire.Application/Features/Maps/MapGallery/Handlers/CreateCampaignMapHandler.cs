using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Domain.Entities.Maps.CampaignMaps;
using AscendantEmpire.Domain.Enums.Maps;

using MediatR;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.Maps.MapGallery.Handlers;

public record CreateCampaignMapRequest() : IRequest<ServiceResultBase>;

internal class CreateCampaignMapHandler(
    ILogger<CreateCampaignMapHandler> logger,
    AscendantEmpireContext context,
    IServiceScopeFactory scopeFactory
) : IRequestHandler<CreateCampaignMapRequest, ServiceResultBase>
{
    public async Task<ServiceResultBase> Handle(CreateCampaignMapRequest request, CancellationToken cancellationToken)
    {
        try
        {
            // Get all Gallery Campaign Maps waiting for generation
            List<CampaignMapGalleryIndex> pendingOrCreatingMaps = [.. context.CampaignMapGalleryIndex
                .Where(cmi => cmi.CreationStatus == MapCreationStatus.Pending
                    || cmi.CreationStatus == MapCreationStatus.Creating)
                .OrderBy(cmi => cmi.Id)];

            if (pendingOrCreatingMaps.FirstOrDefault(cmi => cmi.CreationStatus == MapCreationStatus.Creating) != null)
            {
                return ServiceResultBase.SuccessNoContent(); // A map is already being created
            }

            // No maps currently being created, so we can start the next one
            CampaignMapGalleryIndex? nextMapToCreate = pendingOrCreatingMaps
                .FirstOrDefault(cmi => cmi.CreationStatus == MapCreationStatus.Pending);
                
            if (nextMapToCreate != null)
            {
                // Update the status message of all items in the queue
                await UpdateMapStatusQueueAsync(pendingOrCreatingMaps);

                if (nextMapToCreate.CreationEngine == MapCreationEngine.BlankMap)
                {
                    GenerateBlankCampaignMap generator = new(scopeFactory);
                    var validationResult = generator.ValidateRequestProperties(nextMapToCreate);

                    if (validationResult.HasSucceeded == false)
                    {
                        nextMapToCreate.CreationStatus = MapCreationStatus.InError;
                        nextMapToCreate.Status = "Error during validation";
                        nextMapToCreate.ErrorMessage = string.Join(Environment.NewLine, validationResult.Errors);

                        await context.SaveChangesAsync(cancellationToken);
                        
                        return ServiceResultBase.Fail("Validation failed for map creation.");
                    }

                    ServiceResult<int> createResult = await generator.GenerateCampaignMapAsync(nextMapToCreate);

                    if (createResult.HasSucceeded == false)
                    {
                        nextMapToCreate.CreationStatus = MapCreationStatus.InError;
                        nextMapToCreate.Status = "Error during creation";
                        nextMapToCreate.ErrorMessage = string.Join(Environment.NewLine, createResult.Errors);
                        await context.SaveChangesAsync(cancellationToken);
                        
                        return ServiceResultBase.Fail("Map creation failed.");
                    }
                    else
                    {
                        nextMapToCreate.CreationStatus = MapCreationStatus.Complete;
                        nextMapToCreate.Status = "Creation complete";
                        nextMapToCreate.ErrorMessage = null;
                        nextMapToCreate.CampaignMapOffsetId = createResult.SuccessValue;
                        await context.SaveChangesAsync(cancellationToken);
                    }
                }
                else if (nextMapToCreate.CreationEngine == MapCreationEngine.PerlinNoiseV1)
                {
                    GeneratePerlinNoiseCampaignMap generator = new(scopeFactory);
                    var validationResult = generator.ValidateRequestProperties(nextMapToCreate);

                    if (validationResult.HasSucceeded == false)
                    {
                        nextMapToCreate.CreationStatus = MapCreationStatus.InError;
                        nextMapToCreate.Status         = "Error during validation";
                        nextMapToCreate.ErrorMessage   = string.Join(Environment.NewLine, validationResult.Errors);
                        await context.SaveChangesAsync(cancellationToken);
                        return ServiceResultBase.Fail("Validation failed for map creation.");
                    }

                    ServiceResult<int> createResult = await generator.GenerateCampaignMapAsync(nextMapToCreate);

                    if (createResult.HasSucceeded == false)
                    {
                        nextMapToCreate.CreationStatus = MapCreationStatus.InError;
                        nextMapToCreate.Status         = "Error during creation";
                        nextMapToCreate.ErrorMessage   = string.Join(Environment.NewLine, createResult.Errors);
                        await context.SaveChangesAsync(cancellationToken);
                        return ServiceResultBase.Fail("Map creation failed.");
                    }
                    else
                    {
                        nextMapToCreate.CreationStatus    = MapCreationStatus.Complete;
                        nextMapToCreate.Status            = "Creation complete";
                        nextMapToCreate.ErrorMessage      = null;
                        nextMapToCreate.CampaignMapOffsetId = createResult.SuccessValue;
                        await context.SaveChangesAsync(cancellationToken);
                    }
                }
            }
            else
            {
                logger.LogInformation("No pending Campaign Maps found for creation.");
                return ServiceResultBase.Succeed(); // Nothing to do
            }

            return ServiceResultBase.Succeed();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error occurred while creating campaign map.");
            return ServiceResultBase.Fail("An error occurred while creating the campaign map.");
        }
    }

    private async Task UpdateMapStatusQueueAsync(List<CampaignMapGalleryIndex> pendingOrCreatingMaps)
    {
        int counter = 1;
        foreach (CampaignMapGalleryIndex? pendingMap in pendingOrCreatingMaps
            .Where(cmi => cmi.CreationStatus == MapCreationStatus.Pending))
        {
            if (counter == 1)
            {
                pendingMap.CreationStatus = MapCreationStatus.Creating;
                pendingMap.Status = "Creation in progress";
            }
            else
            {
                pendingMap.Status = $"Pending creation (#{counter} in queue)";
            }

            counter++;
        }

        await context.SaveChangesAsync(CancellationToken.None);
    }
}
