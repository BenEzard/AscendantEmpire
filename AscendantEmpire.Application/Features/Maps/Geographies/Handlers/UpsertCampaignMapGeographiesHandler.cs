using AscendantEmpire.Application.Common;

using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Data;
using Microsoft.Extensions.Logging;
using static AscendantEmpire.Application.Common.ServiceResultBase;
using AscendantEmpire.Domain.Enums.Maps;
using AscendantEmpire.Domain.Entities.Maps.CampaignMaps;
using AscendantEmpire.BackendContracts.Geographies.AddCampaignMapGeography;
using AscendantEmpire.Application.Features.Maps.Geographies.Validators;

namespace AscendantEmpire.Application.Features.Maps.Geographies.Handlers;

public record UpsertCampaignMapGeographiesRequest(IReadOnlyCollection<AddOrUpdateCampaignMapGeographyDto> Geographies) : IRequest<ServiceResultBase>;

internal class UpsertCampaignMapGeographiesHandler(
    ILogger<UpsertCampaignMapGeographiesHandler> logger,
    AscendantEmpireContext context,
    IMediator mediator
    ) : IRequestHandler<UpsertCampaignMapGeographiesRequest, ServiceResultBase>
{
    public async Task<ServiceResultBase> Handle(UpsertCampaignMapGeographiesRequest request, CancellationToken cancellationToken)
    {
        // Validate the request objects
        ServiceResultBase validation = CampaignMapGeographyValidator.ValidateGeographies(context, request.Geographies);

        if (validation.HasSucceeded == false)
        {
            logger.LogWarning("Validation failed for " + nameof(UpsertCampaignMapGeographiesRequest) + ": {Errors}", string.Join(Environment.NewLine, validation.Errors));
            return ServiceResult.Fail(validation.Errors);
        }

        CampaignGeography[] existingGeographies = [.. context.CampaignGeographies.Where(b => request.Geographies.Select(rb => rb.CampaignMapGeographyType).Contains(b.CampaignMapGeographyTypeId))];

        CampaignMapGeographyType[] existingGeographyTypeIds = [.. existingGeographies.Select(b => b.CampaignMapGeographyTypeId)];

        // Get any Geographies that aren't in the database
        AddOrUpdateCampaignMapGeographyDto[] missingGeographies = [.. request.Geographies
            .Where(g => existingGeographyTypeIds.Contains(g.CampaignMapGeographyType) == false)
            .Select(g => new AddOrUpdateCampaignMapGeographyDto
            {
                CampaignMapGeographyType = g.CampaignMapGeographyType,
                RelativeElevation = g.RelativeElevation,
                Name = g.Name,
                MovementCost = g.MovementCost,
                IsWater = g.IsWater,
                IsTraversable = g.IsTraversable,
                MapEditorIcon = g.MapEditorIcon,
                IsPaintableOnCampaignMap = g.IsPaintableOnCampaignMap
            })];

        if (missingGeographies.Length != 0)
        {
            ServiceResultBase insertResult = await mediator.Send(new AddCampaignMapGeographyRequest(missingGeographies));
            if (insertResult.HasSucceeded == false)
            {
                logger.LogWarning("Failed to insert missing Geographies during " + nameof(UpsertCampaignMapGeographiesRequest) + ": {Errors}", string.Join(Environment.NewLine, insertResult.Errors));
                return insertResult;
            }
        }

        Dictionary<CampaignMapGeographyType, AddOrUpdateCampaignMapGeographyDto> requestedUpdates = request.Geographies.ToDictionary(b => b.CampaignMapGeographyType);

        foreach (CampaignGeography existingGeography in existingGeographies)
        {
            AddOrUpdateCampaignMapGeographyDto updatedValues = requestedUpdates[existingGeography.CampaignMapGeographyTypeId];

            existingGeography.RelativeElevation = updatedValues.RelativeElevation;
            existingGeography.Name = updatedValues.Name;
            existingGeography.MovementCost = updatedValues.MovementCost;
            existingGeography.IsWater = updatedValues.IsWater;
            existingGeography.IsTraversable = updatedValues.IsTraversable;
            existingGeography.MapEditorIcon = updatedValues.MapEditorIcon;
            existingGeography.IsPaintableOnCampaignMap = updatedValues.IsPaintableOnCampaignMap;
        }

        await context.SaveChangesAsync(CancellationToken.None);

        return SuccessNoContent();
    }
}
