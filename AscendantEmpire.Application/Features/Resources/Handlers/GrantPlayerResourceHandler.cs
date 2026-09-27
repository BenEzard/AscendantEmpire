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
/// Grants a Player the named Resources and ResourceMetaCategories.
/// </summary>
/// <param name="GameId">The Game the Player belongs to, whose current Round the unlocks are stamped with.</param>
/// <param name="ObsoleteInRounds">
/// Schedules the granted rows to stop being available this many Rounds from now, so the obsoleting countdown can be exercised. Null leaves them current
/// indefinitely.
/// </param>
public record GrantPlayerResourceRequest(
    int GameId,
    int PlayerId,
    IReadOnlyCollection<string> ResourceCodes,
    IReadOnlyCollection<string> MetaCategoryCodes,
    int? ObsoleteInRounds) : IRequest<ServiceResultBase>;

/// <summary>
/// Development-only: grants a Player's resource unlocks by hand, standing in for the Technology system that will eventually own them.
/// </summary>
/// <remarks>
/// <para>
/// Without this the unlock path cannot be exercised at all — a new game grants its starter set and nothing ever changes it, so a meta-category icon appearing on
/// unlock could never be seen. Reached only through <c>DevController</c>, which is dropped from the application model outside Development.
/// </para>
/// <para>
/// Granting something the Player already has re-dates it rather than inserting a second row, so the endpoint can be called repeatedly without tripping the
/// composite primary key.
/// </para>
/// </remarks>
internal class GrantPlayerResourceHandler(
    ILogger<GrantPlayerResourceHandler> logger,
    IDbContextFactory<AscendantEmpireContext> dbFactory
) : IRequestHandler<GrantPlayerResourceRequest, ServiceResultBase>
{
    public async Task<ServiceResultBase> Handle(GrantPlayerResourceRequest request, CancellationToken cancellationToken)
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

            ServiceResult<int> currentRound = await PlayerResourceUnlockLookup.ReadCurrentRoundAsync(context, request.GameId, cancellationToken);

            if (currentRound.HasSucceeded == false)
            {
                return currentRound;
            }

            int round = currentRound.SuccessValue;
            int? obsoleteOnRound = request.ObsoleteInRounds is null ? null : round + request.ObsoleteInRounds;

            await GrantResourcesAsync(context, request.PlayerId, targets.SuccessValue!.ResourceIds, round, obsoleteOnRound, cancellationToken);
            await GrantMetaCategoriesAsync(context, request.PlayerId, targets.SuccessValue.MetaCategoryIds, round, obsoleteOnRound, cancellationToken);

            await context.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "Granted {ResourceCount} resource and {MetaCount} meta-category unlock(s) to player {PlayerId} on round {Round}.",
                targets.SuccessValue.ResourceIds.Length,
                targets.SuccessValue.MetaCategoryIds.Length,
                request.PlayerId,
                round);

            return SuccessNoContent();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error in {Handler}.", nameof(GrantPlayerResourceHandler));
            return InternalServerError(ex.Message);
        }
    }

    private static async Task GrantResourcesAsync(
        AscendantEmpireContext context,
        int playerId,
        int[] resourceIds,
        int round,
        int? obsoleteOnRound,
        CancellationToken cancellationToken)
    {
        Dictionary<int, PlayerUnlockedResource> existing = await context.PlayerUnlockedResources
            .Where(u => u.PlayerId == playerId && resourceIds.Contains(u.ResourceId))
            .ToDictionaryAsync(u => u.ResourceId, cancellationToken);

        foreach (int resourceId in resourceIds)
        {
            if (existing.TryGetValue(resourceId, out PlayerUnlockedResource? already))
            {
                already.ObsoleteOnRound = obsoleteOnRound;
                continue;
            }

            context.PlayerUnlockedResources.Add(new PlayerUnlockedResource
            {
                PlayerId = playerId,
                ResourceId = resourceId,
                UnlockedOnRound = round,
                ObsoleteOnRound = obsoleteOnRound
            });
        }
    }

    private static async Task GrantMetaCategoriesAsync(
        AscendantEmpireContext context,
        int playerId,
        int[] metaCategoryIds,
        int round,
        int? obsoleteOnRound,
        CancellationToken cancellationToken)
    {
        Dictionary<int, PlayerUnlockedResourceMetaCategory> existing = await context.PlayerUnlockedResourceMetaCategories
            .Where(u => u.PlayerId == playerId && metaCategoryIds.Contains(u.ResourceMetaCategoryId))
            .ToDictionaryAsync(u => u.ResourceMetaCategoryId, cancellationToken);

        foreach (int metaCategoryId in metaCategoryIds)
        {
            if (existing.TryGetValue(metaCategoryId, out PlayerUnlockedResourceMetaCategory? already))
            {
                already.ObsoleteOnRound = obsoleteOnRound;
                continue;
            }

            context.PlayerUnlockedResourceMetaCategories.Add(new PlayerUnlockedResourceMetaCategory
            {
                PlayerId = playerId,
                ResourceMetaCategoryId = metaCategoryId,
                UnlockedOnRound = round,
                ObsoleteOnRound = obsoleteOnRound
            });
        }
    }
}
