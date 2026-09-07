using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.UnitComponents.Services;
using AscendantEmpire.BackendContracts.UnitDesigns.GetFullCatalogue;
using AscendantEmpire.BackendContracts.UnitDesigns.GetPlayerTemplates;
using AscendantEmpire.BackendContracts.UnitDesigns.GetSlotCandidateComponents;
using AscendantEmpire.BackendContracts.UnitDesigns.GetUnlockedBodies;
using AscendantEmpire.Domain.Enums.UnitDesigns;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.UnitComponents.Handlers;

/// <summary>
/// Returns the entire unit-design catalogue unfiltered (every body, component, tag and template) for a
/// developer design tool. Unlike the player endpoints, this applies no game/player or unlock filtering.
/// </summary>
public record GetFullCatalogueRequest() : IRequest<ServiceResult<FullCatalogueResult>>;

internal class GetFullCatalogueHandler(
    ILogger<GetFullCatalogueHandler> logger,
    IDbContextFactory<AscendantEmpireContext> dbFactory
) : IRequestHandler<GetFullCatalogueRequest, ServiceResult<FullCatalogueResult>>
{
    public async Task<ServiceResult<FullCatalogueResult>> Handle(GetFullCatalogueRequest request, CancellationToken cancelToken)
    {
        try
        {
            await using AscendantEmpireContext context = await dbFactory.CreateDbContextAsync(cancelToken);

            List<DesignBody> bodies = await context.UnitComponents
                .AsNoTracking()
                .Where(c => c.ComponentType == ComponentType.Body && c.Body != null)
                .Select(DesignComponentProjections.ToDesignBody)
                .ToListAsync(cancelToken);

            List<DesignComponent> components = await context.UnitComponents
                .AsNoTracking()
                .Where(c => c.ComponentType != ComponentType.Body)
                .Select(DesignComponentProjections.ToDesignComponent)
                .ToListAsync(cancelToken);

            List<CatalogueTag> tags = await context.Tags
                .AsNoTracking()
                .Select(t => new CatalogueTag
                {
                    Id = t.Id,
                    Code = t.Code,
                    DisplayName = t.DisplayName,
                    Description = t.Description,
                    SystemCreated = t.SystemCreated,
                })
                .ToListAsync(cancelToken);

            List<DesignTemplate> templates = await context.UnitTemplates
                .AsNoTracking()
                .Select(t => new DesignTemplate
                {
                    Id = t.Id,
                    Code = t.Code,
                    DisplayName = t.DisplayName,
                    Description = t.Description,
                    MapImagePath = t.MapImagePath,
                    IsCustom = t.PlayerId != null,

                    // The god-view has no player context, so prototyped state isn't meaningful here.
                    HasBeenPrototyped = false,
                    BodyComponentId = t.BodyComponentId,
                    BodyComponentCode = t.BodyComponent.Code,
                    BodyComponentDisplayName = t.BodyComponent.DisplayName,
                    Components = t.Components
                        .Select(tc => new DesignTemplateComponent
                        {
                            Id = tc.Id,
                            UnitComponentSlotId = tc.UnitComponentSlotId,
                            UnitComponentId = tc.UnitComponentId,
                            ComponentCode = tc.UnitComponent.Code,
                            ComponentDisplayName = tc.UnitComponent.DisplayName,
                            ComponentType = tc.UnitComponent.ComponentType,
                        })
                        .ToList(),
                })
                .ToListAsync(cancelToken);

            var result = new FullCatalogueResult
            {
                Bodies = bodies,
                Components = components,
                Tags = tags,
                Templates = templates,
            };

            return ServiceResult<FullCatalogueResult>.Succeed(result);
        }
        catch (Exception ex)
        {
            const string error = "An exception occurred while processing " + nameof(GetFullCatalogueHandler) + ".";
            logger.LogError(ex, error);
            return ServiceResult<FullCatalogueResult>.InternalServerError(ex, error);
        }
    }
}
