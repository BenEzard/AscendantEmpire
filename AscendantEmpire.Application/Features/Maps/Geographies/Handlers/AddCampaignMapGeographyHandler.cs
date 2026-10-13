using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Maps.Geographies.Validators;
using AscendantEmpire.BackendContracts.Geographies.AddCampaignMapGeography;
using AscendantEmpire.Domain.Entities.Maps.CampaignMaps;
using AscendantEmpire.Domain.Enums.Maps;

using Microsoft.EntityFrameworkCore;
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
        // Validate the request objects
        ServiceResultBase validation = CampaignMapGeographyValidator.ValidateGeographies(context, request.Geographies);

        if (validation.HasSucceeded == false)
        {
            logger.LogWarning("Validation failed for " + nameof(AddCampaignMapGeographyRequest) + ": {Errors}", string.Join(Environment.NewLine, validation.Errors));
            return ServiceResultBase.Fail(validation.Errors);
        }

        // CampaignMapGeographyTypeId is the key, so a geography that is already stored cannot be added a second time. This is checked here rather than in
        // CampaignMapGeographyValidator because UpsertCampaignMapGeographiesHandler validates a request that deliberately names geographies that already
        // exist, and then updates them — the rule belongs to adding, not to the shape of the request.
        CampaignMapGeographyType[] requestedTypes = [.. request.Geographies.Select(geography => geography.CampaignMapGeographyType)];

        CampaignMapGeographyType[] alreadyStored = await context.CampaignGeographies
            .Where(geography => requestedTypes.Contains(geography.CampaignMapGeographyTypeId))
            .Select(geography => geography.CampaignMapGeographyTypeId)
            .ToArrayAsync(cancellationToken);

        if (alreadyStored.Length != 0)
        {
            string[] errors = [.. alreadyStored.Select(type => $"{type} is already stored, so it cannot be added again.")];

            logger.LogWarning(
                "Refused " + nameof(AddCampaignMapGeographyRequest) + " for {GeographyCount} Geographies already stored: {Geographies}.",
                alreadyStored.Length,
                string.Join(", ", alreadyStored));

            return ServiceResultBase.Fail(errors);
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

        return ServiceResultBase.Created();
    }

   
}

