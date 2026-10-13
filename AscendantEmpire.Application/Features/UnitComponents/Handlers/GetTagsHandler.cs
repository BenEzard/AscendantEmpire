using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Data;
using AscendantEmpire.BackendContracts.UnitDesigns.GetTags;

using Microsoft.EntityFrameworkCore;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.UnitComponents.Handlers;

/// <summary>
/// Returns every tag in the global catalogue (unfiltered) for the dev Unit Designer's tag manager.
/// </summary>
public record GetTagsRequest() : IRequest<ServiceResult<GetTagsResult>>;

internal class GetTagsHandler(
    IDbContextFactory<AscendantEmpireContext> dbFactory
) : IRequestHandler<GetTagsRequest, ServiceResult<GetTagsResult>>
{
    public async Task<ServiceResult<GetTagsResult>> Handle(GetTagsRequest request, CancellationToken cancelToken)
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
                LeadershipModel = t.LeadershipModel,
                UnledBaseSize = t.UnledBaseSize,
                UnledDefaultSize = t.UnledDefaultSize,
                UnledMaxSize = t.UnledMaxSize,
                SystemCreated = t.SystemCreated,
            })
            .ToListAsync(cancelToken);

        var result = new GetTagsResult
        {
            Tags = tags,
        };

        return ServiceResult<GetTagsResult>.Succeed(result);
    }
}
