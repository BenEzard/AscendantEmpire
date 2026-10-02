using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Assets.Services;
using AscendantEmpire.Domain.Entities.Assets;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.Assets.Handlers;

/// <summary>
/// Grants a Player the named Assets and AssetMetaCategories.
/// </summary>
/// <param name="GameId">The Game the Player belongs to, whose current Round the unlocks are stamped with.</param>
/// <param name="ObsoleteInRounds">
/// Schedules the granted rows to stop being available this many Rounds from now, so the obsoleting countdown can be exercised. Null leaves them current
/// indefinitely.
/// </param>
public record GrantPlayerAssetRequest(
    int GameId,
    int PlayerId,
    IReadOnlyCollection<string> AssetCodes,
    IReadOnlyCollection<string> MetaCategoryCodes,
    int? ObsoleteInRounds) : IRequest<ServiceResultBase>;

/// <summary>
/// Development-only: grants a Player's asset unlocks by hand, standing in for the Technology system that will eventually own them.
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
internal class GrantPlayerAssetHandler(
    ILogger<GrantPlayerAssetHandler> logger,
    IDbContextFactory<AscendantEmpireContext> dbFactory
) : IRequestHandler<GrantPlayerAssetRequest, ServiceResultBase>
{
    public async Task<ServiceResultBase> Handle(GrantPlayerAssetRequest request, CancellationToken cancellationToken)
    {
        try
        {
            await using AscendantEmpireContext context = await dbFactory.CreateDbContextAsync(cancellationToken);

            ServiceResult<PlayerAssetUnlockTargets> targets = await PlayerAssetUnlockLookup.ResolveAsync(
                context,
                request.AssetCodes,
                request.MetaCategoryCodes,
                cancellationToken);

            if (targets.HasSucceeded == false)
            {
                return targets;
            }

            ServiceResult<int> currentRound = await PlayerAssetUnlockLookup.ReadCurrentRoundAsync(context, request.GameId, cancellationToken);

            if (currentRound.HasSucceeded == false)
            {
                return currentRound;
            }

            int round = currentRound.SuccessValue;
            int? obsoleteOnRound = request.ObsoleteInRounds is null ? null : round + request.ObsoleteInRounds;

            await GrantAssetsAsync(context, request.PlayerId, targets.SuccessValue!.AssetIds, round, obsoleteOnRound, cancellationToken);
            await GrantMetaCategoriesAsync(context, request.PlayerId, targets.SuccessValue.MetaCategoryIds, round, obsoleteOnRound, cancellationToken);

            await context.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "Granted {AssetCount} asset and {MetaCount} meta-category unlock(s) to player {PlayerId} on round {Round}.",
                targets.SuccessValue.AssetIds.Length,
                targets.SuccessValue.MetaCategoryIds.Length,
                request.PlayerId,
                round);

            return SuccessNoContent();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error in {Handler}.", nameof(GrantPlayerAssetHandler));
            return InternalServerError(ex.Message);
        }
    }

    private static async Task GrantAssetsAsync(
        AscendantEmpireContext context,
        int playerId,
        int[] assetIds,
        int round,
        int? obsoleteOnRound,
        CancellationToken cancellationToken)
    {
        Dictionary<int, PlayerUnlockedAsset> existing = await context.PlayerUnlockedAssets
            .Where(u => u.PlayerId == playerId && assetIds.Contains(u.AssetId))
            .ToDictionaryAsync(u => u.AssetId, cancellationToken);

        foreach (int assetId in assetIds)
        {
            if (existing.TryGetValue(assetId, out PlayerUnlockedAsset? already))
            {
                already.ObsoleteOnRound = obsoleteOnRound;
                continue;
            }

            context.PlayerUnlockedAssets.Add(new PlayerUnlockedAsset
            {
                PlayerId = playerId,
                AssetId = assetId,
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
        Dictionary<int, PlayerUnlockedAssetMetaCategory> existing = await context.PlayerUnlockedAssetMetaCategories
            .Where(u => u.PlayerId == playerId && metaCategoryIds.Contains(u.AssetMetaCategoryId))
            .ToDictionaryAsync(u => u.AssetMetaCategoryId, cancellationToken);

        foreach (int metaCategoryId in metaCategoryIds)
        {
            if (existing.TryGetValue(metaCategoryId, out PlayerUnlockedAssetMetaCategory? already))
            {
                already.ObsoleteOnRound = obsoleteOnRound;
                continue;
            }

            context.PlayerUnlockedAssetMetaCategories.Add(new PlayerUnlockedAssetMetaCategory
            {
                PlayerId = playerId,
                AssetMetaCategoryId = metaCategoryId,
                UnlockedOnRound = round,
                ObsoleteOnRound = obsoleteOnRound
            });
        }
    }
}
