using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Maps.Geographies.Validators;
using AscendantEmpire.BackendContracts.Geographies.AddCampaignMapGeography;
using AscendantEmpire.Domain.Entities.Maps.CampaignMaps;

using MediatR;

using Microsoft.Extensions.Logging;

namespace AscendantEmpire.Application.Features.Maps.Geographies.Handlers;

public record AddCampaignMapGeographyRequest(IReadOnlyCollection<AddOrUpdateCampaignMapGeographyDto> Geographies) : IRequest<ServiceResultBase>;


internal class AddCampaignMapGeographyHandler(
    ILogger<AddCampaignMapGeographyHandler> logger,
    AscendantEmpireContext context
    ) : IRequestHandler<AddCampaignMapGeographyRequest, ServiceResultBase>
{
    public async Task<ServiceResultBase> Handle(AddCampaignMapGeographyRequest request, CancellationToken cancellationToken)
    {
        try
        {
            // Validate the request objects
            ServiceResultBase validation = CampaignMapGeographyValidator.ValidateGeographies(context, request.Geographies);

            if (validation.HasSucceeded == false)
            {
                logger.LogWarning("Validation failed for " + nameof(AddCampaignMapGeographyRequest) + ": {Errors}", string.Join(Environment.NewLine, validation.Errors));
                return ServiceResultBase.Fail(validation.Errors);
            }

            // Map the request DTOs to domain entities
            var campaignGeographies = request.Geographies.Select(biome => new CampaignGeography
            {
                CampaignMapGeographyTypeId = biome.CampaignMapGeographyType,
                RelativeElevation = biome.RelativeElevation,
                Name = biome.Name,
                MovementCost = biome.MovementCost,
                MapEditorIcon = biome.MapEditorIcon,
                IsPaintableOnCampaignMap = biome.IsPaintableOnCampaignMap,
            }).ToArray();

            context.CampaignGeographies.AddRange(campaignGeographies);

            await context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            const string error = "Error updating Campaign Map Geographies.";
            logger.LogError(ex, error);
            return ServiceResultBase.InternalServerError(error);
        }

        return ServiceResultBase.Created();
    }

   
}

