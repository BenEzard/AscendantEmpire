using AscendantEmpire.Application.Data;

using Microsoft.EntityFrameworkCore;

namespace AscendantEmpire.Application.Features.Assets.Services;

/// <inheritdoc cref="IPlayerExtractionEfficiencyReader" />
internal sealed class PlayerExtractionEfficiencyReader : IPlayerExtractionEfficiencyReader
{
    public async Task<PlayerExtractionEfficiencies> GetEfficienciesAsync(AscendantEmpireContext context, int playerId, CancellationToken cancelToken = default)
    {
        // A Player holds a handful of these, so they are summed in memory, the same as PlayerSustenanceValueReader does.
        var modifiers = await context.PlayerExtractionEfficiencyModifiers
            .Where(modifier => modifier.PlayerId == playerId)
            .Select(modifier => new { modifier.AssetId, modifier.FlatValue, modifier.PercentValue })
            .ToListAsync(cancelToken);

        Dictionary<int, (long FlatRawTotal, long PercentRawTotal)> totalsByAssetId = modifiers
            .GroupBy(modifier => modifier.AssetId)
            .ToDictionary(
                group => group.Key,
                group => (group.Sum(modifier => modifier.FlatValue), group.Sum(modifier => modifier.PercentValue)));

        return new PlayerExtractionEfficiencies(totalsByAssetId);
    }
}
