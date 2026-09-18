using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Maps.Depletion.Classes;
using AscendantEmpire.Domain.Enums.Maps;

using Microsoft.EntityFrameworkCore;

namespace AscendantEmpire.Application.Features.Maps.Depletion.Services;

/// <inheritdoc cref="IBiomeRegenerationValueReader" />
internal sealed class BiomeRegenerationValueReader : IBiomeRegenerationValueReader
{
    public async Task<Dictionary<BiomeType, BiomeRegenerationValues>> GetValuesAsync(AscendantEmpireContext context, CancellationToken cancelToken = default)
    {
        // A projection, so nothing is tracked: only the Percent and the four sustenance columns are needed off each biome.
        return await context.CampaignBiomes
            .Select(biome => new BiomeRegenerationValues
            {
                BiomeType = biome.BiomeTypeId,
                RegenerationPercent = biome.RegenerationPercent,
                BaseForageValue = biome.BaseForageValue,
                BaseHuntValue = biome.BaseHuntValue,
                BaseFishValue = biome.BaseFishValue,
                BaseFodderValue = biome.BaseFodderValue
            })
            .ToDictionaryAsync(biome => biome.BiomeType, cancelToken);
    }
}
