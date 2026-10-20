using AscendantEmpire.Application.Features.Maps.Biomes.Classes;
using AscendantEmpire.Application.Features.Maps.Biomes.Services;
using AscendantEmpire.Application.Helpers.ScaledNumerics;
using AscendantEmpire.BackendContracts.CampaignMaps.GetPlayerCampaignMap;
using AscendantEmpire.Domain.Entities.Games;
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
/// A land tile with a river on any of its edges offers <b>Fish as well</b>, and its Forage and Hunt are raised, both through <see cref="RiverSustenanceBonus"/>. Fish joins
/// the pool only where there is a river, so a dry land tile behaves exactly as it always has.
/// </para>
/// <para>
/// The one place the pool is built, shared by the end-of-Turn feed (<c>Gathering</c>) and the support indicator (<see cref="ITileSupportEvaluator"/>), so the colour a
/// tile is shown in can never be worked out from a different pool than the one the Forces actually eat from.
/// </para>
/// </remarks>
internal static class TileFoodPool
{
    /// <summary>
    /// What a dry land tile offers. Fodder is for animals, and nothing eats it yet.
    /// </summary>
    private static readonly SustenanceResource[] _LAND_RESOURCES = [SustenanceResource.Forage, SustenanceResource.Hunt];

    /// <summary>
    /// What a land tile with a river on one of its edges offers: the dry pool, plus the Fish the river puts there.
    /// </summary>
    private static readonly SustenanceResource[] _RIVER_LAND_RESOURCES = [SustenanceResource.Forage, SustenanceResource.Hunt, SustenanceResource.Fish];

    /// <summary>
    /// What a water tile offers.
    /// </summary>
    private static readonly SustenanceResource[] _WATER_RESOURCES = [SustenanceResource.Fish];

    /// <summary>
    /// What is available to the Player of each resource in the tile's pool.
    /// </summary>
    /// <param name="isWater">Whether the tile is water, from <c>CampaignTileWater.IsWater</c>.</param>
    /// <param name="values">The Player's effective sustenance values.</param>
    /// <param name="biome">The tile's biome.</param>
    /// <param name="depletion">The tile's depletion row, or <see langword="null"/> when it has never been fed on.</param>
    /// <param name="rivers">The tile's four river edges, or <see langword="null"/> when they are not known, which is read as no rivers.</param>
    /// <returns>
    /// One entry per resource the biome has. A resource the biome does not have at all (a null effective value) is left out - unlike a stripped resource, which is in the pool
    /// at 0.
    /// </returns>
    public static Dictionary<SustenanceResource, ScaledAmount> GetAvailable(
        bool isWater,
        PlayerSustenanceValues values,
        BiomeType biome,
        CampaignMapDepletion? depletion,
        TileRivers? rivers = null)
    {
        SustenanceResource[] poolResources = GetPoolResources(isWater, rivers);

        var available = new Dictionary<SustenanceResource, ScaledAmount>();

        foreach (SustenanceResource resource in poolResources)
        {
            // The rivers are applied to the Player's effective value, before anything taken is removed: a river raises what the tile holds, not what is left of it.
            ScaledAmount? effectiveValue = RiverSustenanceBonus.Apply(values.GetValue(biome, resource), resource, rivers, isWater);

            ScaledAmount? availableValue = SustenanceValueCalculator.CalculateAvailableValue(effectiveValue, DepletionTaken.GetTaken(depletion, resource));

            if (availableValue is not null)
            {
                available.Add(resource, availableValue.Value);
            }
        }

        return available;
    }

    /// <summary>
    /// Which resources the tile's pool is made of.
    /// </summary>
    /// <remarks>
    /// Fish is only in a land tile's pool where a river puts it there. A land biome carrying its own Fish value - which the biome editor does not allow - is left out of the
    /// pool as it always has been.
    /// </remarks>
    private static SustenanceResource[] GetPoolResources(bool isWater, TileRivers? rivers)
    {
        if (isWater)
        {
            return _WATER_RESOURCES;
        }

        return rivers?.HasAny == true ? _RIVER_LAND_RESOURCES : _LAND_RESOURCES;
    }
}
