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
/// Takes the named Assets and AssetMetaCategories back off a Player.
/// </summary>
/// <param name="GameId">The Game the Player belongs to. Carried for symmetry with the grant and for the log line; no Round is recorded by a withdrawal.</param>
public record WithdrawPlayerAssetRequest(
    int GameId,
    int PlayerId,
    IReadOnlyCollection<string> AssetCodes,
    IReadOnlyCollection<string> MetaCategoryCodes) : IRequest<ServiceResultBase>;

/// <summary>
/// Development-only: withdraws a Player's asset unlocks by hand, standing in for the Technology system that will eventually own them.
/// </summary>
/// <remarks>
/// <para>
/// Withdrawing <b>deletes</b> the row rather than setting <c>ObsoleteOnRound</c>. The two mean different things: an obsoleted Asset was had and has been
/// retired, while no row means it was never granted. Undoing a grant wants the second; to retire something instead, grant it with an obsoleting countdown.
/// </para>
/// <para>
/// Withdrawing something the Player does not have is not an error — the end state is the one asked for either way.
/// </para>
/// </remarks>
internal class WithdrawPlayerAssetHandler(
    ILogger<WithdrawPlayerAssetHandler> logger,
    IDbContextFactory<AscendantEmpireContext> dbFactory
) : IRequestHandler<WithdrawPlayerAssetRequest, ServiceResultBase>
{
    public async Task<ServiceResultBase> Handle(WithdrawPlayerAssetRequest request, CancellationToken cancellationToken)
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

        int[] assetIds = targets.SuccessValue!.AssetIds;
        int[] metaCategoryIds = targets.SuccessValue.MetaCategoryIds;

        List<PlayerUnlockedAsset> assetRows = await context.PlayerUnlockedAssets
            .Where(u => u.PlayerId == request.PlayerId && assetIds.Contains(u.AssetId))
            .ToListAsync(cancellationToken);

        context.PlayerUnlockedAssets.RemoveRange(assetRows);

        List<PlayerUnlockedAssetMetaCategory> metaCategoryRows = await context.PlayerUnlockedAssetMetaCategories
            .Where(u => u.PlayerId == request.PlayerId && metaCategoryIds.Contains(u.AssetMetaCategoryId))
            .ToListAsync(cancellationToken);

        context.PlayerUnlockedAssetMetaCategories.RemoveRange(metaCategoryRows);

        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Withdrew {AssetCount} asset and {MetaCount} meta-category unlock(s) from player {PlayerId} in game {GameId}.",
            assetRows.Count,
            metaCategoryRows.Count,
            request.PlayerId,
            request.GameId);

        return SuccessNoContent();
    }
}
