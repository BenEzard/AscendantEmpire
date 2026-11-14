using AscendantEmpire.Application.Data;
using AscendantEmpire.Domain.Enums.MapResources;

using Microsoft.EntityFrameworkCore;

namespace AscendantEmpire.Application.Features.Maps.Depletion.Services;

/// <summary>
/// Which of a MapResource type's Assets a Player takes, as their own switches decide it.
/// </summary>
/// <remarks>
/// Shared by the readers that turn a patch into food - <see cref="ITileFoodStockReader"/> for what a Force really eats, and
/// <see cref="ITileFoodDisclosureReader"/> for what the toolbar tells them - so a yield they have switched off can never be on in one and off in the other.
/// </remarks>
internal static class MapResourceYieldSwitches
{
    /// <summary>
    /// What the Player's switches say about each yield they hold a row for, an off beating an on as <c>PlayerMapResourceSwitch</c> resolves them.
    /// </summary>
    /// <returns>A yield with no row of its own is absent, which the callers read as its type's default.</returns>
    public static async Task<IReadOnlyDictionary<(int TypeId, int AssetId), bool>> ReadAsync(
        AscendantEmpireContext context,
        int gameId,
        int playerId,
        CancellationToken cancelToken = default)
    {
        var switches = await context.PlayerMapResourceSwitches
            .Where(playerSwitch => playerSwitch.GameId == gameId)
            .Where(playerSwitch => playerSwitch.PlayerId == playerId)
            .Where(playerSwitch => playerSwitch.Kind == MapResourceSwitchKind.YieldAsset)
            .Where(playerSwitch => playerSwitch.AssetId != null)
            .Select(playerSwitch => new { playerSwitch.MapResourceTypeId, playerSwitch.AssetId, playerSwitch.IsEnabled })
            .ToListAsync(cancelToken);

        return switches
            .GroupBy(playerSwitch => (playerSwitch.MapResourceTypeId, AssetId: playerSwitch.AssetId!.Value))
            .ToDictionary(group => group.Key, group => group.All(playerSwitch => playerSwitch.IsEnabled));
    }
}
