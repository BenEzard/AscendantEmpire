using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.BackendContracts.UnitDesigns.GetPlayerTemplates;

using MediatR;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.UnitComponents.Handlers;

/// <summary>
/// Returns the templates available to a player: global/common templates they have unlocked plus their
/// own custom templates. Game and player are supplied from the caller's token.
/// </summary>
public record GetPlayerTemplatesRequest(int GameId, int PlayerId) : IRequest<ServiceResult<PlayerTemplatesResult>>;

internal class GetPlayerTemplatesHandler(
    ILogger<GetPlayerTemplatesHandler> logger,
    IDbContextFactory<AscendantEmpireContext> dbFactory
) : IRequestHandler<GetPlayerTemplatesRequest, ServiceResult<PlayerTemplatesResult>>
{
    public async Task<ServiceResult<PlayerTemplatesResult>> Handle(GetPlayerTemplatesRequest request, CancellationToken cancelToken)
    {
        try
        {
            if (request.GameId <= 0 || request.PlayerId <= 0)
            {
                return ServiceResult<PlayerTemplatesResult>.Fail("Game and player are required.");
            }

            await using AscendantEmpireContext context = await dbFactory.CreateDbContextAsync(cancelToken);

            // Global/common templates this player has unlocked (and not yet obsoleted).
            IQueryable<int> unlockedTemplateIds = context.PlayerUnlockedTemplates
                .Where(u => u.PlayerId == request.PlayerId && u.ObsoleteOnTurn == null)
                .Select(u => u.UnitTemplateId);

            List<DesignTemplate> templates = await context.UnitTemplates
                .AsNoTracking()
                .Where(t => unlockedTemplateIds.Contains(t.Id)
                    || (t.GameId == request.GameId && t.PlayerId == request.PlayerId))
                .Select(t => new DesignTemplate
                {
                    Id = t.Id,
                    Code = t.Code,
                    DisplayName = t.DisplayName,
                    Description = t.Description,
                    MapImagePath = t.MapImagePath,
                    IsCustom = t.PlayerId != null,
                    HasBeenPrototyped = t.PlayerStates
                        .Where(s => s.PlayerId == request.PlayerId)
                        .Select(s => s.HasBeenPrototyped)
                        .FirstOrDefault(),
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

            return ServiceResult<PlayerTemplatesResult>.Succeed(new PlayerTemplatesResult { Templates = templates });
        }
        catch (Exception ex)
        {
            const string error = "An exception occurred while processing " + nameof(GetPlayerTemplatesHandler) + ".";
            logger.LogError(ex, error);
            return ServiceResult<PlayerTemplatesResult>.InternalServerError(ex, error);
        }
    }
}
