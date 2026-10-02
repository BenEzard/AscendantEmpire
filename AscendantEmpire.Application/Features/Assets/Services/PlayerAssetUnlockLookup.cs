using AscendantEmpire.Application.Data;
using AscendantEmpire.Domain.Enums.Turns;

using Microsoft.EntityFrameworkCore;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.Assets.Services;

/// <summary>
/// The catalogue rows a grant or withdrawal names, resolved from their codes.
/// </summary>
internal record PlayerAssetUnlockTargets(int[] AssetIds, int[] MetaCategoryIds);

/// <summary>
/// Resolves the codes an asset unlock request carries into catalogue ids, and reads the Round a Game is on.
/// </summary>
/// <remarks>
/// Shared by the grant and withdraw handlers so the code-matching and its error message live in one place. Both take codes rather than ids because a caller
/// naming <c>Weapons.MeleeWeapons.MedievalSword.Low</c> can be read at a glance, where an id cannot.
/// </remarks>
internal static class PlayerAssetUnlockLookup
{
    /// <summary>
    /// Resolves every supplied code to its catalogue id, failing when any of them matches nothing.
    /// </summary>
    /// <remarks>
    /// An unknown code fails the whole request rather than being skipped: a typo would otherwise look identical to a call that worked and changed nothing.
    /// </remarks>
    public static async Task<ServiceResult<PlayerAssetUnlockTargets>> ResolveAsync(
        AscendantEmpireContext context,
        IReadOnlyCollection<string> assetCodes,
        IReadOnlyCollection<string> metaCategoryCodes,
        CancellationToken cancellationToken)
    {
        if (assetCodes.Count == 0 && metaCategoryCodes.Count == 0)
        {
            return ServiceResult<PlayerAssetUnlockTargets>.Fail("At least one asset or meta-category code must be supplied.");
        }

        Dictionary<string, int> assetIdsByCode = await context.Assets
            .Where(r => assetCodes.Contains(r.Code))
            .ToDictionaryAsync(r => r.Code, r => r.Id, cancellationToken);

        Dictionary<string, int> metaCategoryIdsByCode = await context.AssetMetaCategories
            .Where(m => metaCategoryCodes.Contains(m.Code))
            .ToDictionaryAsync(m => m.Code, m => m.Id, cancellationToken);

        string[] unknownCodes =
        [
            .. assetCodes.Except(assetIdsByCode.Keys, StringComparer.OrdinalIgnoreCase),
            .. metaCategoryCodes.Except(metaCategoryIdsByCode.Keys, StringComparer.OrdinalIgnoreCase)
        ];

        if (unknownCodes.Length > 0)
        {
            return ServiceResult<PlayerAssetUnlockTargets>.NotFound($"No catalogue row for code(s): {string.Join(", ", unknownCodes)}. Has the Assets seed been run?");
        }

        return ServiceResult<PlayerAssetUnlockTargets>.Succeed(
            new PlayerAssetUnlockTargets([.. assetIdsByCode.Values], [.. metaCategoryIdsByCode.Values]));
    }

    /// <summary>
    /// Reads the Round the Game's campaign lane is currently on, which is what an unlock is stamped with.
    /// </summary>
    /// <remarks>
    /// Only the grant path needs this - a withdrawal removes the row outright and so records no Round. A Game always has a campaign queue, but this fails rather
    /// than guessing a Round if one is somehow absent.
    /// </remarks>
    public static async Task<ServiceResult<int>> ReadCurrentRoundAsync(
        AscendantEmpireContext context,
        int gameId,
        CancellationToken cancellationToken)
    {
        int? currentRound = await context.TurnQueues
            .Where(q => q.GameId == gameId && q.QueueType == TurnQueueType.Campaign)
            .Select(q => (int?)q.CurrentRoundNumber)
            .FirstOrDefaultAsync(cancellationToken);

        return currentRound is null
            ? ServiceResult<int>.NotFound($"Game {gameId} has no campaign turn queue, so its current Round is unknown.")
            : ServiceResult<int>.Succeed(currentRound.Value);
    }
}
