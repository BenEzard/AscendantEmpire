using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Data;
using AscendantEmpire.BackendContracts.Resources;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.Resources.Handlers;

public record GetResourcesRequest() : IRequest<ServiceResult<IReadOnlyCollection<ResourceSummary>>>;

/// <summary>
/// Lists every Resource, in meta category order and then by name, for pickers such as the MapResource Type Manager's.
/// </summary>
internal class GetResourcesHandler(
    ILogger<GetResourcesHandler> logger,
    IDbContextFactory<AscendantEmpireContext> dbFactory
) : IRequestHandler<GetResourcesRequest, ServiceResult<IReadOnlyCollection<ResourceSummary>>>
{
    public async Task<ServiceResult<IReadOnlyCollection<ResourceSummary>>> Handle(GetResourcesRequest request, CancellationToken cancellationToken)
    {
        try
        {
            await using AscendantEmpireContext context = await dbFactory.CreateDbContextAsync(cancellationToken);

            List<ResourceSummary> resources = await context.Resources
                .AsNoTracking()
                .OrderBy(r => r.MetaCategory.SortOrder)
                .ThenBy(r => r.DisplayName)
                .Select(r => new ResourceSummary
                {
                    Id = r.Id,
                    Code = r.Code,
                    DisplayName = r.DisplayName,
                    MetaCategoryDisplayName = r.MetaCategory.DisplayName
                })
                .ToListAsync(cancellationToken);

            return ServiceResult<IReadOnlyCollection<ResourceSummary>>.Succeed(resources);
        }
        catch (Exception ex)
        {
            const string error = "An error occurred while retrieving Resources.";
            logger.LogError(ex, error);

            return ServiceResult<IReadOnlyCollection<ResourceSummary>>.Fail(error);
        }
    }
}
