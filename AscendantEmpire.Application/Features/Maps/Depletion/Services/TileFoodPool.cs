using AscendantEmpire.Application.Features.Maps.Biomes.Classes;
using AscendantEmpire.Application.Features.Maps.Biomes.Services;
using AscendantEmpire.Application.Helpers.ScaledNumerics;
using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Enums.Maps;
using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.Application.Features.Maps.Depletion.Services;

/// <summary>
/// The food a campaign map tile offers a Player's Forces: what is available of each resource in the tile's pool.
/// </summary>
/// <remarks>
/// <para>
/// A land tile's pool is Forage and Hunt; a water tile's is Fish. Fodder is for animals, and nothing eats it yet. What is available of each resource is
/// <c>MAX(0, effective − Taken)</c>, through <see cref="SustenanceValueCalculator.CalculateAvailableValue"/>.
/// </para>
/// <para>
/// The one place the pool is built, shared by the end-of-Turn feed (<c>Gathering</c>) and the support indicator (<see cref="ITileSupportEvaluator"/>), so the colour a
/// tile is shown in can never be worked out from a different pool than the one the Forces actually eat from.
/// </para>
/// <para>
/// Pure and database-free.
/// </para>
/// </remarks>
internal static class TileFoodPool
{
    /// <summary>
    /// What is available to the Player of each resource in the tile's pool.
    /// </summary>
    /// <param name="isWater">Whether the tile is water, from <c>CampaignTileWater.IsWater</c>.</param>
    /// <param name="values">The Player's effective sustenance values.</param>
    /// <param name="biome">The tile's biome.</param>
    /// <param name="depletion">The tile's depletion row, or <see langword="null"/> when it has never been fed on.</param>
    /// <returns>
    /// One entry per resource the biome has. A resource the biome does not have at all (a null effective value) is left out - unlike a stripped resource, which is in the pool
    /// at 0.
    /// </returns>
    public static Dictionary<SustenanceResource, ScaledAmount> GetAvailable(bool isWater, PlayerSustenanceValues values, BiomeType biome, CampaignMapDepletion? depletion)
    {
        SustenanceResource[] poolResources = isWater
            ? [SustenanceResource.Fish]
            : [SustenanceResource.Forage, SustenanceResource.Hunt];

        var available = new Dictionary<SustenanceResource, ScaledAmount>();

        foreach (SustenanceResource resource in poolResources)
        {
            ScaledAmount? availableValue = SustenanceValueCalculator.CalculateAvailableValue(values.GetValue(biome, resource), DepletionTaken.GetTaken(depletion, resource));

            if (availableValue is not null)
            {
                available.Add(resource, availableValue.Value);
            }
        }

        return available;
    }
}
