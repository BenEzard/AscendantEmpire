using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Data;
using AscendantEmpire.BackendContracts.Technologies.GetExperienceTypes;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.Technologies.Handlers;

public record GetExperienceTypesRequest() : IRequest<ServiceResult<IReadOnlyCollection<ExperienceTypeDetail>>>;

/// <summary>
/// Lists every kind of experience an action can earn, in display order.
/// </summary>
/// <remarks>
/// Served separately as well as nested under each technology: the editor needs the whole list to choose from, and the figure is small enough that a client
/// may as well hold all of it.
/// </remarks>
internal class GetExperienceTypesHandler(
    ILogger<GetExperienceTypesHandler> logger,
    IDbContextFactory<AscendantEmpireContext> dbFactory
) : IRequestHandler<GetExperienceTypesRequest, ServiceResult<IReadOnlyCollection<ExperienceTypeDetail>>>
{
    public async Task<ServiceResult<IReadOnlyCollection<ExperienceTypeDetail>>> Handle(
        GetExperienceTypesRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            await using AscendantEmpireContext context = await dbFactory.CreateDbContextAsync(cancellationToken);

            List<ExperienceTypeDetail> experienceTypes = await context.ExperienceTypes
                .AsNoTracking()
                .OrderBy(experienceType => experienceType.SortOrder)
                .ThenBy(experienceType => experienceType.DisplayName)
                .Select(experienceType => new ExperienceTypeDetail
                {
                    Id = experienceType.Id,
                    Code = experienceType.Code,
                    DisplayName = experienceType.DisplayName,
                    Description = experienceType.Description,
                    IconPath = experienceType.IconPath,
                    SortOrder = experienceType.SortOrder
                })
                .ToListAsync(cancellationToken);

            return ServiceResult<IReadOnlyCollection<ExperienceTypeDetail>>.Succeed(experienceTypes);
        }
        catch (Exception ex)
        {
            const string error = "An error occurred while retrieving experience types.";
            logger.LogError(ex, error);

            return ServiceResult<IReadOnlyCollection<ExperienceTypeDetail>>.Fail(error);
        }
    }
}
