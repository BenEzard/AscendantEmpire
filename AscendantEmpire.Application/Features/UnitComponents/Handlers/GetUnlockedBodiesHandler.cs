using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.UnitComponents.Services;
using AscendantEmpire.BackendContracts.UnitDesigns.GetUnlockedBodies;
using AscendantEmpire.Domain.Enums.UnitDesigns;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.UnitComponents.Handlers;

/// <summary>
/// Returns the Body components a player has unlocked in their game, each with its slots and slot rules —
/// the canvas the unit designer is built on. Game and player are supplied from the caller's token.
/// </summary>
public record GetUnlockedBodiesRequest(int GameId, int PlayerId) : IRequest<ServiceResult<UnlockedBodiesResult>>;

internal class GetUnlockedBodiesHandler(
    ILogger<GetUnlockedBodiesHandler> logger,
    IDbContextFactory<AscendantEmpireContext> dbFactory
) : IRequestHandler<GetUnlockedBodiesRequest, ServiceResult<UnlockedBodiesResult>>
{
    public async Task<ServiceResult<UnlockedBodiesResult>> Handle(GetUnlockedBodiesRequest request, CancellationToken cancelToken)
    {
        try
        {
            // GameId/PlayerId come from signed claims, so a lightweight guard is enough.
            if (request.GameId <= 0 || request.PlayerId <= 0)
            {
                return ServiceResult<UnlockedBodiesResult>.Fail("Game and player are required.");
            }

            await using AscendantEmpireContext context = await dbFactory.CreateDbContextAsync(cancelToken);

            // A Player row is already game-scoped, so filtering unlocks by PlayerId implies the game.
            IQueryable<int> unlockedComponentIds = context.PlayerUnlockedComponents
                .Where(u => u.PlayerId == request.PlayerId && u.ObsoleteOnTurn == null)
                .Select(u => u.UnitComponentId);

            List<DesignBody> bodies = await context.UnitComponents
                .AsNoTracking()
                .Where(c => c.ComponentType == ComponentType.Body
                    && c.Body != null
                    && unlockedComponentIds.Contains(c.Id))
                .Select(DesignComponentProjections.ToDesignBody)
                .ToListAsync(cancelToken);

            return ServiceResult<UnlockedBodiesResult>.Succeed(new UnlockedBodiesResult { Bodies = bodies });
        }
        catch (Exception ex)
        {
            const string error = "An exception occurred while processing " + nameof(GetUnlockedBodiesHandler) + ".";
            logger.LogError(ex, error);
            return ServiceResult<UnlockedBodiesResult>.InternalServerError(ex, error);
        }
    }
}
