using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.Application.Features.Maps.Depletion.Services;

/// <summary>
/// The Asset each of a biome's four sustenance resources is, so what a Force eats off the land can be named, priced and one day banked.
/// </summary>
/// <remarks>
/// <para>
/// Held in code rather than on the Biome, because the mapping is the same on every tile in every Game: a biome's Hunt is game meat wherever it is hunted.
/// What differs by tile is how much there is, which is what the Biome's values and the depletion row already say.
/// </para>
/// <para>
/// <b>Forage must never become a MapResourceType.</b> It stands for whatever a Biome itself puts within a forager's reach, so a patch of it placed on a
/// tile would be counted twice over - once as the tile's own yield and once as the patch's. <c>MapResourceTypeSeedDataTests</c> pins that.
/// </para>
/// <para>
/// Fodder is mapped for completeness and nothing eats it: <c>mount-fodder</c> is a <c>Fuel</c> set spent per movement point, not a food.
/// </para>
/// </remarks>
internal static class SustenanceResourceAssets
{
    /// <summary>
    /// The Asset code each resource is identified as.
    /// </summary>
    public static readonly IReadOnlyDictionary<SustenanceResource, string> CodesByResource = new Dictionary<SustenanceResource, string>
    {
        [SustenanceResource.Forage] = "Foodstuffs.PlantFoods.Forage",
        [SustenanceResource.Hunt] = "Foodstuffs.Meat.Game",
        [SustenanceResource.Fish] = "Foodstuffs.Meat.Fish",
        [SustenanceResource.Fodder] = "RawMaterials.RawMaterials.Fodder"
    };
}
