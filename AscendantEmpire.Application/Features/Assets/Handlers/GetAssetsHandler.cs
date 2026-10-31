using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Data;
using AscendantEmpire.BackendContracts.Assets;

using Microsoft.EntityFrameworkCore;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.Assets.Handlers;

public record GetAssetsRequest() : IRequest<ServiceResult<IReadOnlyCollection<AssetSummary>>>;

/// <summary>
/// Lists every Asset, in meta category order and then by name, for pickers such as the MapResource Type Manager's.
/// </summary>
internal class GetAssetsHandler(
    IDbContextFactory<AscendantEmpireContext> dbFactory
) : IRequestHandler<GetAssetsRequest, ServiceResult<IReadOnlyCollection<AssetSummary>>>
{
    public async Task<ServiceResult<IReadOnlyCollection<AssetSummary>>> Handle(GetAssetsRequest request, CancellationToken cancellationToken)
    {
        await using AscendantEmpireContext context = await dbFactory.CreateDbContextAsync(cancellationToken);

        List<AssetSummary> assets = await context.Assets
            .AsNoTracking()
            .OrderBy(r => r.MetaCategory.SortOrder)
            .ThenBy(r => r.SortOrder)
            .ThenBy(r => r.DisplayName)
            .Select(r => new AssetSummary
            {
                Id = r.Id,
                Code = r.Code,
                DisplayName = r.DisplayName,
                MetaCategoryDisplayName = r.MetaCategory.DisplayName,
                IsNaturallyOccurring = r.IsNaturallyOccurring
            })
            .ToListAsync(cancellationToken);

        return ServiceResult<IReadOnlyCollection<AssetSummary>>.Succeed(assets);
    }
}
