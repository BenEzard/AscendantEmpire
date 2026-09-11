using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Maps.CampaignMaps.Classes;
using AscendantEmpire.Domain.Enums.Games;

using Microsoft.EntityFrameworkCore;

namespace AscendantEmpire.Application.Features.Maps.CampaignMaps.Services;

/// <summary>
/// Every active Player sees alone: a coalition of one each.
/// </summary>
/// <remarks>
/// Only active Players are counted. An eliminated or excluded Player has nobody left to report to, so their Observers witness nothing.
/// </remarks>
internal class SoloCoalitionResolver : ICoalitionResolver
{
    /// <inheritdoc />
    public async Task<IReadOnlyCollection<VisionCoalition>> ResolveAsync(AscendantEmpireContext context, int gameId, CancellationToken cancelToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        List<int> activePlayerIds = await context.Players
            .AsNoTracking()
            .Where(player => player.GameId == gameId && player.Status == PlayerStatus.Active)
            .Select(player => player.Id)
            .ToListAsync(cancelToken);

        return SoloCoalitions.From(activePlayerIds);
    }
}
