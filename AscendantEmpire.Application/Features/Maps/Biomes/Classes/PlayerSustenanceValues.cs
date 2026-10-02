using AscendantEmpire.Application.Helpers.ScaledNumerics;
using AscendantEmpire.Domain.Enums.Maps;
using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.Application.Features.Maps.Biomes.Classes;

/// <summary>
/// One Player's effective Forage, Hunt, Fish and Fodder values for every biome, as read by <c>IPlayerSustenanceValueReader</c>.
/// </summary>
/// <remarks>
/// A snapshot: it does not change if the Player's modifiers do after it was read.
/// </remarks>
internal sealed class PlayerSustenanceValues(int playerId, IReadOnlyDictionary<(BiomeType Biome, SustenanceResource Resource), ScaledAmount?> values)
{
    /// <summary>
    /// The Player these values belong to.
    /// </summary>
    public int PlayerId { get; } = playerId;

    /// <summary>
    /// The Player's effective value of one resource in one biome.
    /// </summary>
    /// <returns>
    /// The effective value (never negative), or <see langword="null"/> when the resource does not exist in that biome - including a biome with no <c>CampaignBiome</c> row at all.
    /// </returns>
    public ScaledAmount? GetValue(BiomeType biome, SustenanceResource resource)
    {
        return values.TryGetValue((biome, resource), out ScaledAmount? value) ? value : null;
    }
}
