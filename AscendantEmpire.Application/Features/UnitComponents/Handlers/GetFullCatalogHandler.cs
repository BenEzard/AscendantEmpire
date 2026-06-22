using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.UnitComponents.Services;
using AscendantEmpire.BackendContracts.UnitDesigns.GetFullCatalog;
using AscendantEmpire.BackendContracts.UnitDesigns.GetPlayerTemplates;
using AscendantEmpire.BackendContracts.UnitDesigns.GetSlotCandidateComponents;
using AscendantEmpire.BackendContracts.UnitDesigns.GetUnlockedBodies;
using AscendantEmpire.Domain.Enums.UnitDesigns;

using MediatR;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.UnitComponents.Handlers;

/// <summary>
/// Returns the entire unit-design catalog unfiltered (every body, component, tag and template) for a
/// developer design tool. Unlike the player endpoints, this applies no game/player or unlock filtering.
/// </summary>
public record GetFullCatalogRequest() : IRequest<ServiceResult<FullCatalogResult>>;

internal class GetFullCatalogHandler(
    ILogger<GetFullCatalogHandler> logger,
    IDbContextFactory<AscendantEmpireContext> dbFactory
) : IRequestHandler<GetFullCatalogRequest, ServiceResult<FullCatalogResult>>
{
    public async Task<ServiceResult<FullCatalogResult>> Handle(GetFullCatalogRequest request, CancellationToken cancelToken)
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

            List<CatalogTag> tags = await context.Tags
                .AsNoTracking()
                .Select(t => new CatalogTag
                {
                    Id = t.Id,
                    Code = t.Code,
                    DisplayName = t.DisplayName,
                    Description = t.Description,
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
                            ParentTemplateComponentId = tc.ParentTemplateComponentId,
                            UnitComponentSlotId = tc.UnitComponentSlotId,
                            UnitComponentId = tc.UnitComponentId,
                            ComponentCode = tc.UnitComponent.Code,
                            ComponentDisplayName = tc.UnitComponent.DisplayName,
                            ComponentType = tc.UnitComponent.ComponentType,
                        })
                        .ToList(),
                })
                .ToListAsync(cancelToken);

            var result = new FullCatalogResult
            {
                Bodies = bodies,
                Components = components,
                Tags = tags,
                Templates = templates,
            };

            return ServiceResult<FullCatalogResult>.Succeed(result);
        }
        catch (Exception ex)
        {
            const string error = "An exception occurred while processing " + nameof(GetFullCatalogHandler) + ".";
            logger.LogError(ex, error);
            return ServiceResult<FullCatalogResult>.InternalServerError(ex, error);
        }
    }
}
