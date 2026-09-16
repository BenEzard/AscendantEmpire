using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Units.Classes;

using Microsoft.Extensions.Logging;

namespace AscendantEmpire.Application.Features.Units.Services;

/// <summary>
/// Stand-in <see cref="IUnderfedForceResolver"/> that only logs the Forces that went short.
/// </summary>
/// <remarks>
/// Changes nothing, so going hungry has no consequence yet.
/// </remarks>
internal class LoggingUnderfedForceResolver(ILogger<LoggingUnderfedForceResolver> logger) : IUnderfedForceResolver
{
    public Task ResolveAsync(AscendantEmpireContext context, int gameId, int playerId, IReadOnlyCollection<ForceFeedingResult> results, CancellationToken cancelToken = default)
    {
        // TODO CM-37: damage model for partial feeding. A Force that gets only part of what it needs should take damage in proportion to its shortfall; how much, to which
        //  troops (healthy first, or injured), and whether several hungry Turns in a row compound, all still need working out. Stage the writes on the context given here.
        foreach (ForceFeedingResult result in results.Where(r => r.IsUnderfed))
        {
            logger.LogInformation(
                "Unit Instance Id {UnitInstanceId} belonging to Player Id {PlayerId} in Game Id {GameId} went short of food on tile {CampaignMapTileId}: needed {Need}, fed {Fed}, short by {Shortfall}.",
                result.UnitInstanceId,
                playerId,
                gameId,
                result.CampaignMapTileId,
                result.Need.ToDecimal(),
                result.Fed.ToDecimal(),
                result.Shortfall.ToDecimal());
        }

        return Task.CompletedTask;
    }
}
