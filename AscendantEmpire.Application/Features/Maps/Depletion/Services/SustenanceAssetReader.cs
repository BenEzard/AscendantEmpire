using AscendantEmpire.Application.Data;
using AscendantEmpire.Domain.Enums.Maps;

using Microsoft.EntityFrameworkCore;

namespace AscendantEmpire.Application.Features.Maps.Depletion.Services;

/// <inheritdoc cref="ISustenanceAssetReader" />
internal sealed class SustenanceAssetReader : ISustenanceAssetReader
{
    public async Task<IReadOnlyDictionary<SustenanceResource, int>> GetAssetIdsAsync(AscendantEmpireContext context, CancellationToken cancelToken = default)
    {
        string[] codes = [.. SustenanceResourceAssets.CodesByResource.Values];

        Dictionary<string, int> idsByCode = await context.Assets
            .Where(asset => codes.Contains(asset.Code))
            .ToDictionaryAsync(asset => asset.Code, asset => asset.Id, cancelToken);

        return SustenanceResourceAssets.CodesByResource
            .Where(entry => idsByCode.ContainsKey(entry.Value))
            .ToDictionary(entry => entry.Key, entry => idsByCode[entry.Value]);
    }
}
