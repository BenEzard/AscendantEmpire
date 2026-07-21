using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.BackendContracts.UnitDesigns.GetTagTypes;

using MediatR;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.UnitComponents.Handlers;

/// <summary>
/// Returns every tag type's editable metadata for the dev Unit Designer's tag type manager.
/// </summary>
public record GetTagTypesRequest() : IRequest<ServiceResult<GetTagTypesResult>>;

internal class GetTagTypesHandler(
    ILogger<GetTagTypesHandler> logger,
    IDbContextFactory<AscendantEmpireContext> dbFactory
) : IRequestHandler<GetTagTypesRequest, ServiceResult<GetTagTypesResult>>
{
    public async Task<ServiceResult<GetTagTypesResult>> Handle(GetTagTypesRequest request, CancellationToken cancelToken)
    {
        try
        {
            await using AscendantEmpireContext context = await dbFactory.CreateDbContextAsync(cancelToken);

            List<TagTypeView> tagTypes = await context.TagTypeInfos
                .AsNoTracking()
                .OrderBy(t => t.TagTypeId)
                .Select(t => new TagTypeView
                {
                    TagTypeId = t.TagTypeId,
                    DisplayName = t.DisplayName,
                    Description = t.Description,
                    IsInherited = t.IsInherited,
                    Colour = t.Colour,
                    Icon = t.Icon,
                })
                .ToListAsync(cancelToken);

            var result = new GetTagTypesResult
            {
                TagTypes = tagTypes,
            };

            return ServiceResult<GetTagTypesResult>.Succeed(result);
        }
        catch (Exception ex)
        {
            const string error = "An exception occurred while processing " + nameof(GetTagTypesHandler) + ".";
            logger.LogError(ex, error);
            return ServiceResult<GetTagTypesResult>.InternalServerError(ex, error);
        }
    }
}
