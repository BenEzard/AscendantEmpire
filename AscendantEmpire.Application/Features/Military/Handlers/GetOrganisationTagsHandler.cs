using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Data;
using AscendantEmpire.BackendContracts.Military.GetOrganisationTags;
using AscendantEmpire.Domain.Enums.UnitDesigns;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.Military.Handlers;

/// <summary>
/// Returns the <see cref="TagType.MilitaryOrganisationalGroup"/> tags — the feed for the Organisation page's
/// searchable combo box.
/// </summary>
public record GetOrganisationTagsRequest() : IRequest<ServiceResult<GetOrganisationTagsResult>>;

internal class GetOrganisationTagsHandler(
    ILogger<GetOrganisationTagsHandler> logger,
    IDbContextFactory<AscendantEmpireContext> dbFactory
) : IRequestHandler<GetOrganisationTagsRequest, ServiceResult<GetOrganisationTagsResult>>
{
    public async Task<ServiceResult<GetOrganisationTagsResult>> Handle(GetOrganisationTagsRequest request, CancellationToken cancelToken)
    {
        try
        {
            await using AscendantEmpireContext context = await dbFactory.CreateDbContextAsync(cancelToken);

            List<OrganisationTagView> tags = await context.Tags
                .AsNoTracking()
                .Where(t => t.TagType == TagType.MilitaryOrganisationalGroup)
                .OrderBy(t => t.DisplayName)
                .Select(t => new OrganisationTagView
                {
                    Id = t.Id,
                    Code = t.Code,
                    DisplayName = t.DisplayName,
                    MovementDomain = t.MovementDomain,
                    LeadershipModel = t.LeadershipModel,
                    UnledBaseSize = t.UnledBaseSize,
                    UnledDefaultSize = t.UnledDefaultSize,
                    UnledMaxSize = t.UnledMaxSize,
                })
                .ToListAsync(cancelToken);

            return ServiceResult<GetOrganisationTagsResult>.Succeed(new GetOrganisationTagsResult { Tags = tags });
        }
        catch (Exception ex)
        {
            const string error = "An exception occurred while processing " + nameof(GetOrganisationTagsHandler) + ".";
            logger.LogError(ex, error);
            return ServiceResult<GetOrganisationTagsResult>.InternalServerError(ex, error);
        }
    }
}
