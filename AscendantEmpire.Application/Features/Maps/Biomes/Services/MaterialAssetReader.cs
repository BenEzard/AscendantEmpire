using AscendantEmpire.Application.Data;
using AscendantEmpire.Domain.Enums.Maps;

using Microsoft.EntityFrameworkCore;

namespace AscendantEmpire.Application.Features.Maps.Biomes.Services;

/// <inheritdoc cref="IMaterialAssetReader" />
internal sealed class MaterialAssetReader : IMaterialAssetReader
{
    public async Task<IReadOnlyDictionary<MaterialResource, int>> GetAssetIdsAsync(AscendantEmpireContext context, CancellationToken cancelToken = default)
    {
        string[] codes = [.. MaterialResourceAssets.CodesByResource.Values];

        Dictionary<string, int> idsByCode = await context.Assets
            .Where(asset => codes.Contains(asset.Code))
            .ToDictionaryAsync(asset => asset.Code, asset => asset.Id, cancelToken);

        return MaterialResourceAssets.CodesByResource
            .Where(entry => idsByCode.ContainsKey(entry.Value))
            .ToDictionary(entry => entry.Key, entry => idsByCode[entry.Value]);
    }
}
