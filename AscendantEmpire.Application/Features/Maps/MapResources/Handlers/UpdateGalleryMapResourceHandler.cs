using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Maps.MapResources.Services;
using AscendantEmpire.Application.Helpers.ScaledNumerics;
using AscendantEmpire.BackendContracts.MapResources.GalleryPlacements;
using AscendantEmpire.Domain.Entities.Maps.CampaignMaps;
using AscendantEmpire.FrontendContracts.MapResources.GalleryPlacements;

using Microsoft.EntityFrameworkCore;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.Maps.MapResources.Handlers;

public record UpdateGalleryMapResourceRequest(int CampaignMapGalleryIndexId, int PlacementId, IUpdateGalleryMapResourceDto Specifics) : IRequest<ServiceResult<GalleryMapResource>>;

/// <summary>
/// Sets the specifics of a placed MapResource: its starting quantity, or the Distribution scenario it starts from. Either left empty is decided when a game
/// is created.
/// </summary>
/// <remarks>
/// Each only applies to one kind: a starting quantity to a type holding a single quantity, and a scenario to a Distribution. A stratified type has neither,
/// since its layers are always rolled.
/// </remarks>
internal class UpdateGalleryMapResourceHandler(
    AscendantEmpireContext context
) : IRequestHandler<UpdateGalleryMapResourceRequest, ServiceResult<GalleryMapResource>>
{
    public async Task<ServiceResult<GalleryMapResource>> Handle(UpdateGalleryMapResourceRequest request, CancellationToken cancellationToken)
    {
        IUpdateGalleryMapResourceDto specifics = request.Specifics;

        // The placement must be on the map named in the route, so one map's editor cannot change another map.
        CampaignMapGalleryResource? placement = await (
            from p in context.CampaignMapGalleryResources
            join tile in context.CampaignMapGallery on p.CampaignMapGalleryId equals tile.CampaignMapGalleryId
            where p.Id == request.PlacementId && tile.CampaignMapGalleryIndexId == request.CampaignMapGalleryIndexId
            select p)
            .SingleOrDefaultAsync(cancellationToken);

        if (placement == null)
        {
            return ServiceResult<GalleryMapResource>.NotFound("That MapResource is not on this map.");
        }

        var type = await context.MapResourceTypes
            .AsNoTracking()
            .Where(t => t.Id == placement.MapResourceTypeId)
            .Select(t => new { t.DisplayName, t.IsDistribution, t.IsStratified })
            .SingleAsync(cancellationToken);

        List<string> errors = [];
        int? scenarioId = null;

        if (specifics.InitialQuantity != null)
        {
            if (type.IsDistribution || type.IsStratified)
            {
                errors.Add($"{type.DisplayName} does not start from a single quantity, so it cannot be given one.");
            }
            else if (specifics.InitialQuantity < 0)
            {
                errors.Add("The starting quantity cannot be negative.");
            }
        }

        if (specifics.DistributionScenarioNumber != null)
        {
            if (type.IsDistribution == false)
            {
                errors.Add($"{type.DisplayName} is not a Distribution, so it has no scenarios.");
            }
            else
            {
                scenarioId = await context.MapResourceDistributionScenarios
                    .Where(s => s.MapResourceTypeId == placement.MapResourceTypeId && s.ScenarioNumber == specifics.DistributionScenarioNumber)
                    .Select(s => (int?)s.Id)
                    .SingleOrDefaultAsync(cancellationToken);

                if (scenarioId == null)
                {
                    errors.Add($"{type.DisplayName} has no scenario {specifics.DistributionScenarioNumber}.");
                }
            }
        }

        if (errors.Count != 0)
        {
            return ServiceResult<GalleryMapResource>.Fail(errors);
        }

        placement.InitialQuantity = ScaledResourceValue.ToRaw(specifics.InitialQuantity);
        placement.MapResourceDistributionScenarioId = scenarioId;

        await context.SaveChangesAsync(CancellationToken.None);

        List<GalleryMapResource> saved = await GalleryMapResourceQueries.ReadAsync(context, request.CampaignMapGalleryIndexId, placement.Id, cancellationToken);

        return ServiceResult<GalleryMapResource>.Succeed(saved.Single());
    }
}
