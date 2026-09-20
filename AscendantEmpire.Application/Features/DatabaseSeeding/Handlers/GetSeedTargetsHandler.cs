using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Features.DatabaseSeeding.Classes;
using AscendantEmpire.Application.Features.DatabaseSeeding.Steps;

using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.DatabaseSeeding.Handlers;

/// <summary>
/// Lists the parts of the database that can be seeded, with the dependencies between them.
/// </summary>
/// <remarks>
/// Reads nothing but the registered seed steps, so the catalogue the selection dialog draws and the steps the executor runs are the same collection. 
/// Adding a seed step is therefore the whole job — the dialog picks it up with no frontend change.
/// </remarks>
public record GetSeedTargetsRequest() : IRequest<ServiceResult<IReadOnlyCollection<DTSeedTarget>>>;

internal class GetSeedTargetsHandler(
    ILogger<GetSeedTargetsHandler> logger,
    IEnumerable<ISeedStep> seedSteps
) : IRequestHandler<GetSeedTargetsRequest, ServiceResult<IReadOnlyCollection<DTSeedTarget>>>
{
    public Task<ServiceResult<IReadOnlyCollection<DTSeedTarget>>> Handle(GetSeedTargetsRequest request, CancellationToken cancellationToken)
    {
        try
        {
            // Ordered the way the dialog lists them, which is also the order a full seed runs in.
            IReadOnlyCollection<DTSeedTarget> targets =
            [
                .. seedSteps
                    .OrderBy(step => (int)step.Group)
                    .ThenBy(step => step.SortOrder)
                    .Select(step => new DTSeedTarget
                    {
                        Target = step.Target,
                        Group = step.Group,
                        DisplayName = step.DisplayName,
                        Description = step.Description,
                        DependsOn = step.DependsOn,
                        SortOrder = step.SortOrder,
                        ItemCount = step.ItemCount,
                    })
            ];

            return Task.FromResult(ServiceResult<IReadOnlyCollection<DTSeedTarget>>.Succeed(targets));
        }
        catch (Exception ex)
        {
            const string error = "An error occurred while listing the database seed targets.";
            logger.LogError(ex, error);
            return Task.FromResult(ServiceResult<IReadOnlyCollection<DTSeedTarget>>.InternalServerError(error));
        }
    }
}
