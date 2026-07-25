using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.BackendContracts.UnitDesigns.GetTags;

using MediatR;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.UnitComponents.Handlers;

/// <summary>
/// Returns every tag in the global catalog (unfiltered) for the dev Unit Designer's tag manager.
/// </summary>
public record GetTagsRequest() : IRequest<ServiceResult<GetTagsResult>>;

internal class GetTagsHandler(
    ILogger<GetTagsHandler> logger,
    IDbContextFactory<AscendantEmpireContext> dbFactory
) : IRequestHandler<GetTagsRequest, ServiceResult<GetTagsResult>>
{
    public async Task<ServiceResult<GetTagsResult>> Handle(GetTagsRequest request, CancellationToken cancelToken)
    {
        try
        {
            await using AscendantEmpireContext context = await dbFactory.CreateDbContextAsync(cancelToken);

            List<TagView> tags = await context.Tags
                .AsNoTracking()
                .OrderBy(t => t.Code)
                .Select(t => new TagView
                {
                    Id = t.Id,
                    Code = t.Code,
                    DisplayName = t.DisplayName,
                    Description = t.Description,
                    TagType = t.TagType,
                    MovementDomain = t.MovementDomain,
                })
                .ToListAsync(cancelToken);

            var result = new GetTagsResult
            {
                Tags = tags,
            };

            return ServiceResult<GetTagsResult>.Succeed(result);
        }
        catch (Exception ex)
        {
            const string error = "An exception occurred while processing " + nameof(GetTagsHandler) + ".";
            logger.LogError(ex, error);
            return ServiceResult<GetTagsResult>.InternalServerError(ex, error);
        }
    }
}
