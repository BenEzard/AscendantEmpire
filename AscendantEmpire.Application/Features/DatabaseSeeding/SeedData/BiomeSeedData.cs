using AscendantEmpire.BackendContracts.Biomes;
using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.Application.Features.DatabaseSeeding.SeedData;

/// <summary>
/// The initial <c>map.CampaignBiomes</c> values applied by the database seed.
/// </summary>
/// <remarks>
/// <para>
/// Transcribed from <c>Resources/Documentation/Installation scripts/CampaignMapBiomes.json</c>.
/// </para>
/// <para>
/// The <c>Base*Value</c> figures are the plain whole numbers exactly as a user enters them in the biome
/// editor. The upsert handler scales them by 1,000 (via <c>ScaledResourceValue.ToRaw</c>) when it writes
/// the row, so they must NOT be pre-scaled here.
/// </para>
/// </remarks>
internal static class BiomeSeedData
{
    /// <summary>
    /// Builds the campaign map biome rows to seed.
    /// </summary>
    public static IReadOnlyCollection<AddOrUpdateCampaignMapBiomeDto> Build()
    {
        return
        [
            new AddOrUpdateCampaignMapBiomeDto
            {
                BiomeType = BiomeType.Unknown,
                Name = "Unknown",
                MovementCost = 1m,
                DoesBlockSight = true,
                IsWater = false,
                IsTraversable = true,
                RegenerationRate = null,
                IsPaintableOnCampaignMap = false,
                MapEditorHtmlColor = "#c9c9cf",
                CanBeElevated = true,
                // Not paintable, so every resource value must stay null (enforced by the biome validator).
            },
            new AddOrUpdateCampaignMapBiomeDto
            {
                BiomeType = BiomeType.Coastal,
                Name = "Coastal",
                MovementCost = 1m,
                DoesBlockSight = false,
                IsWater = true,
                IsTraversable = true,
                RegenerationRate = 1.25m,
                IsPaintableOnCampaignMap = true,
                MapEditorHtmlColor = "#0c92eb",
                CanBeElevated = false,
                BaseFishValue = 100,
                BaseBoneValue = 100,
            },
            new AddOrUpdateCampaignMapBiomeDto
            {
                BiomeType = BiomeType.Ocean,
                Name = "Ocean",
                MovementCost = 1m,
                DoesBlockSight = false,
                IsWater = true,
                IsTraversable = true,
                RegenerationRate = 1.25m,
                IsPaintableOnCampaignMap = true,
                MapEditorHtmlColor = "#063ea9",
                CanBeElevated = false,
                BaseFishValue = 200,
                BaseBoneValue = 20,
            },
            new AddOrUpdateCampaignMapBiomeDto
            {
                BiomeType = BiomeType.Desert,
                Name = "Desert",
                MovementCost = 1.1m,
                DoesBlockSight = false,
                IsWater = false,
                IsTraversable = true,
                RegenerationRate = 1.1m,
                IsPaintableOnCampaignMap = true,
                MapEditorHtmlColor = "#fee414",
                CanBeElevated = true,
                BaseForageValue = 10,
                BaseHuntValue = 10,
                BaseBoneValue = 10,
                BaseWoodValue = 0,
                BaseStoneValue = 20,
            },
            new AddOrUpdateCampaignMapBiomeDto
            {
                BiomeType = BiomeType.Arid,
                Name = "Arid",
                MovementCost = 1m,
                DoesBlockSight = false,
                IsWater = false,
                IsTraversable = true,
                RegenerationRate = 1.15m,
                IsPaintableOnCampaignMap = true,
                MapEditorHtmlColor = "#fcfda4",
                CanBeElevated = true,
                BaseForageValue = 30,
                BaseHuntValue = 30,
                BaseFodderValue = 20,
                BaseBoneValue = 20,
                BaseWoodValue = 30,
                BaseStoneValue = 50,
            },
            new AddOrUpdateCampaignMapBiomeDto
            {
                BiomeType = BiomeType.Ice,
                Name = "Ice",
                MovementCost = 1m,
                DoesBlockSight = false,
                IsWater = false,
                IsTraversable = true,
                RegenerationRate = 1.05m,
                IsPaintableOnCampaignMap = true,
                MapEditorHtmlColor = "#fdf4ea",
                CanBeElevated = true,
                BaseForageValue = 10,
                BaseHuntValue = 10,
                BaseBoneValue = 0,
            },
            new AddOrUpdateCampaignMapBiomeDto
            {
                BiomeType = BiomeType.Tundra,
                Name = "Tundra",
                MovementCost = 1m,
                DoesBlockSight = false,
                IsWater = false,
                IsTraversable = true,
                RegenerationRate = 1.15m,
                IsPaintableOnCampaignMap = true,
                MapEditorHtmlColor = "#66bfda",
                CanBeElevated = true,
                BaseForageValue = 30,
                BaseHuntValue = 30,
                BaseFodderValue = 10,
                BaseBoneValue = 20,
                BaseWoodValue = 30,
                BaseStoneValue = 20,
            },
            new AddOrUpdateCampaignMapBiomeDto
            {
                BiomeType = BiomeType.Grassland,
                Name = "Grassland",
                MovementCost = 1m,
                DoesBlockSight = false,
                IsWater = false,
                IsTraversable = true,
                RegenerationRate = 1.25m,
                IsPaintableOnCampaignMap = true,
                MapEditorHtmlColor = "#98c760",
                CanBeElevated = true,
                BaseForageValue = 40,
                BaseHuntValue = 70,
                BaseFodderValue = 100,
                BaseBoneValue = 40,
                BaseWoodValue = 40,
                BaseStoneValue = 40,
            },
            new AddOrUpdateCampaignMapBiomeDto
            {
                BiomeType = BiomeType.Forest,
                Name = "Forest",
                MovementCost = 1m,
                DoesBlockSight = true,
                IsWater = false,
                IsTraversable = true,
                RegenerationRate = 1.3m,
                IsPaintableOnCampaignMap = true,
                MapEditorHtmlColor = "#066114",
                CanBeElevated = true,
                BaseForageValue = 50,
                BaseHuntValue = 50,
                BaseFodderValue = 20,
                BaseBoneValue = 30,
                BaseWoodValue = 100,
                BaseStoneValue = 20,
            },
            new AddOrUpdateCampaignMapBiomeDto
            {
                BiomeType = BiomeType.Jungle,
                Name = "Jungle",
                MovementCost = 1m,
                DoesBlockSight = true,
                IsWater = false,
                IsTraversable = true,
                RegenerationRate = 1.5m,
                IsPaintableOnCampaignMap = true,
                MapEditorHtmlColor = "#098f57",
                CanBeElevated = true,
                BaseForageValue = 40,
                BaseHuntValue = 40,
                BaseFodderValue = 30,
                BaseBoneValue = 30,
                BaseWoodValue = 30,
                BaseStoneValue = 10,
            },
            new AddOrUpdateCampaignMapBiomeDto
            {
                BiomeType = BiomeType.Woodland,
                Name = "Woodland",
                MovementCost = 1m,
                DoesBlockSight = false,
                IsWater = false,
                IsTraversable = true,
                RegenerationRate = 1.25m,
                IsPaintableOnCampaignMap = true,
                MapEditorHtmlColor = "#806b51",
                CanBeElevated = true,
                BaseForageValue = 40,
                BaseHuntValue = 70,
                BaseFodderValue = 40,
                BaseBoneValue = 30,
                BaseWoodValue = 80,
                BaseStoneValue = 50,
            },
            new AddOrUpdateCampaignMapBiomeDto
            {
                BiomeType = BiomeType.Void,
                Name = "Void",
                MovementCost = 99m,
                DoesBlockSight = true,
                IsWater = false,
                IsTraversable = false,
                RegenerationRate = 1.1m,
                IsPaintableOnCampaignMap = true,
                MapEditorHtmlColor = "#000000",
                CanBeElevated = true,
                // Impassable void carries no harvestable resources.
            },
        ];
    }
}
