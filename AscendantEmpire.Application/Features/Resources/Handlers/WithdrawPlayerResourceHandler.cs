using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Resources.Services;
using AscendantEmpire.Domain.Entities.Resources;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.Resources.Handlers;

/// <summary>
/// Takes the named Resources and ResourceMetaCategories back off a Player.
/// </summary>
/// <param name="GameId">The Game the Player belongs to. Carried for symmetry with the grant and for the log line; no Round is recorded by a withdrawal.</param>
public record WithdrawPlayerResourceRequest(
    int GameId,
    int PlayerId,
    IReadOnlyCollection<string> ResourceCodes,
    IReadOnlyCollection<string> MetaCategoryCodes) : IRequest<ServiceResultBase>;

/// <summary>
/// Development-only: withdraws a Player's resource unlocks by hand, standing in for the Technology system that will eventually own them.
/// </summary>
/// <remarks>
/// <para>
/// Withdrawing <b>deletes</b> the row rather than setting <c>ObsoleteOnRound</c>. The two mean different things: an obsoleted Resource was had and has been
/// retired, while no row means it was never granted. Undoing a grant wants the second; to retire something instead, grant it with an obsoleting countdown.
/// </para>
/// <para>
/// Withdrawing something the Player does not have is not an error — the end state is the one asked for either way.
/// </para>
/// </remarks>
internal class WithdrawPlayerResourceHandler(
    ILogger<WithdrawPlayerResourceHandler> logger,
    IDbContextFactory<AscendantEmpireContext> dbFactory
) : IRequestHandler<WithdrawPlayerResourceRequest, ServiceResultBase>
{
    public async Task<ServiceResultBase> Handle(WithdrawPlayerResourceRequest request, CancellationToken cancellationToken)
    {
        try
        {
            await using AscendantEmpireContext context = await dbFactory.CreateDbContextAsync(cancellationToken);

            ServiceResult<PlayerResourceUnlockTargets> targets = await PlayerResourceUnlockLookup.ResolveAsync(
                context,
                request.ResourceCodes,
                request.MetaCategoryCodes,
                cancellationToken);

            if (targets.HasSucceeded == false)
            {
                return targets;
            }

            int[] resourceIds = targets.SuccessValue!.ResourceIds;
            int[] metaCategoryIds = targets.SuccessValue.MetaCategoryIds;

            List<PlayerUnlockedResource> resourceRows = await context.PlayerUnlockedResources
                .Where(u => u.PlayerId == request.PlayerId && resourceIds.Contains(u.ResourceId))
                .ToListAsync(cancellationToken);

            context.PlayerUnlockedResources.RemoveRange(resourceRows);

            List<PlayerUnlockedResourceMetaCategory> metaCategoryRows = await context.PlayerUnlockedResourceMetaCategories
                .Where(u => u.PlayerId == request.PlayerId && metaCategoryIds.Contains(u.ResourceMetaCategoryId))
                .ToListAsync(cancellationToken);

            context.PlayerUnlockedResourceMetaCategories.RemoveRange(metaCategoryRows);

            await context.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "Withdrew {ResourceCount} resource and {MetaCount} meta-category unlock(s) from player {PlayerId} in game {GameId}.",
                resourceRows.Count,
                metaCategoryRows.Count,
                request.PlayerId,
                request.GameId);

            return SuccessNoContent();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error in {Handler}.", nameof(WithdrawPlayerResourceHandler));
            return InternalServerError(ex.Message);
        }
    }
}
