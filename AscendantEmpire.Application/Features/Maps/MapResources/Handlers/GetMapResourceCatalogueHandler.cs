using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Maps.MapResources.Services;
using AscendantEmpire.BackendContracts.MapResources.GetMapResourceCatalogue;
using AscendantEmpire.Domain.Entities.Maps.MapResources;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.Maps.MapResources.Handlers;

public record GetMapResourceCatalogueRequest() : IRequest<ServiceResult<MapResourceCatalogue>>;

/// <summary>
/// Reads the whole MapResource catalogue, grouped by category, for the MapResource Type Manager and the map editor's palette.
/// </summary>
internal class GetMapResourceCatalogueHandler(
    ILogger<GetMapResourceCatalogueHandler> logger,
    IDbContextFactory<AscendantEmpireContext> dbFactory
) : IRequestHandler<GetMapResourceCatalogueRequest, ServiceResult<MapResourceCatalogue>>
{
    public async Task<ServiceResult<MapResourceCatalogue>> Handle(GetMapResourceCatalogueRequest request, CancellationToken cancellationToken)
    {
        try
        {
            await using AscendantEmpireContext context = await dbFactory.CreateDbContextAsync(cancellationToken);

            // The catalogue is small, so it is read whole. Split queries keep the four child collections (and the scenarios' two) from multiplying into one wide join.
            List<MapResourceCategory> categories = await context.MapResourceCategories
                .AsNoTracking()
                .AsSplitQuery()
                .Include(c => c.MapResourceTypes).ThenInclude(t => t.Yields)
                .Include(c => c.MapResourceTypes).ThenInclude(t => t.Habitats)
                .Include(c => c.MapResourceTypes).ThenInclude(t => t.DistributionScenarios).ThenInclude(s => s.Amounts)
                .Include(c => c.MapResourceTypes).ThenInclude(t => t.DistributionScenarios).ThenInclude(s => s.BiomeWeights)
                .Include(c => c.MapResourceTypes).ThenInclude(t => t.Layers)
                .ToListAsync(cancellationToken);

            var catalogue = new MapResourceCatalogue
            {
                Categories = [.. categories
                    .OrderBy(c => c.SortOrder)
                    .ThenBy(c => c.DisplayName)
                    .Select(c => new MapResourceCategoryDetail
                    {
                        Id = c.Id,
                        Code = c.Code,
                        DisplayName = c.DisplayName,
                        SortOrder = c.SortOrder,
                        Types = [.. c.MapResourceTypes
                            .OrderBy(t => t.SortOrder)
                            .ThenBy(t => t.DisplayName)
                            .Select(MapResourceTypeMapper.ToDetail)]
                    })]
            };

            return ServiceResult<MapResourceCatalogue>.Succeed(catalogue);
        }
        catch (Exception ex)
        {
            const string error = "An error occurred while retrieving the MapResource catalogue.";
            logger.LogError(ex, error);

            return ServiceResult<MapResourceCatalogue>.Fail(error);
        }
    }
}
