using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Maps.Biomes.Classes;

namespace AscendantEmpire.Application.Features.Maps.Biomes.Services;

/// <summary>
/// Reads a Player's effective Forage, Hunt, Fish and Fodder values: each biome's base value with the Player's <c>PlayerSustenanceModifier</c> rows applied.
/// </summary>
/// <remarks>
/// <para>
/// The only way to get a Player's sustenance values. The effective value is never stored, so every consumer - depletion, replenishment, and reconciling Players with
/// different values on a shared tile - goes through here, and the formula (<see cref="SustenanceValueCalculator"/>) cannot drift between them.
/// </para>
/// <para>
/// No raw SQL. The base values are read live from <c>map.CampaignBiomes</c> rather than copied at game setup, since they are the same for every game.
/// </para>
/// <para>
/// Reads through the caller's context, so a read made during the end-of-turn pipeline sees that pipeline's uncommitted writes.
/// </para>
/// </remarks>
internal interface IPlayerSustenanceValueReader
{
    /// <summary>
    /// The Player's effective value of every sustenance resource in every biome.
    /// </summary>
    /// <remarks>
    /// Loads the whole Player at once - the handful of biome rows and the Player's modifiers - because the consumers work tile by tile across a map and would otherwise
    /// query per tile. A Player with no modifiers, or no such Player, simply gets the base values.
    /// </remarks>
    Task<PlayerSustenanceValues> GetValuesAsync(AscendantEmpireContext context, int playerId, CancellationToken cancelToken = default);
}
