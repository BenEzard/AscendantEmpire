using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Assets.Classes;
using AscendantEmpire.BackendContracts.Assets.GetSupplySets;
using AscendantEmpire.Domain.Entities.Assets;

using Microsoft.EntityFrameworkCore;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.Assets.Handlers;

/// <summary>
/// Reads the whole supply set catalogue for the dev Entity Setup Tool.
/// </summary>
public record GetSupplySetsRequest() : IRequest<ServiceResult<GetSupplySetsResult>>;

internal class GetSupplySetsHandler(AscendantEmpireContext context) : IRequestHandler<GetSupplySetsRequest, ServiceResult<GetSupplySetsResult>>
{
    public async Task<ServiceResult<GetSupplySetsResult>> Handle(GetSupplySetsRequest request, CancellationToken cancelToken)
    {
        List<SupplySet> sets = await context.SupplySets
            .Include(set => set.Assets)
            .ThenInclude(member => member.Asset)
            .OrderBy(set => set.Code)
            .ToListAsync(cancelToken);

        var result = new GetSupplySetsResult
        {
            SupplySets = [.. sets.Select(SupplySetProjections.ToView)]
        };

        return ServiceResult<GetSupplySetsResult>.Succeed(result);
    }
}
