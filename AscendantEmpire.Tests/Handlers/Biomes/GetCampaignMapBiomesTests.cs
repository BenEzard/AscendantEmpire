using AcendantEmpire.BackendContracts.Biomes;

using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Handlers.Maps.Biomes;
using AscendantEmpire.Domain.Entities.Maps.CampaignMaps;
using AscendantEmpire.Domain.Enums.Maps;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace MyEmpire.Tests.Handlers.Biomes;

internal class GetCampaignMapBiomesTests
{
    private AscendantEmpireContext _context = null!;
    private GetCampaignMapBiomesHandler _handler = null!;

    [SetUp]
    public async Task Setup()
    {
        var options = new DbContextOptionsBuilder<AscendantEmpireContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new AscendantEmpireContext(options);
        await _context.Database.EnsureDeletedAsync();
        await _context.Database.EnsureCreatedAsync();

        _handler = new GetCampaignMapBiomesHandler(new NullLogger<GetCampaignMapBiomesHandler>(), _context);
    }

    [TearDown]
    public void TearDown()
    {
        _context?.Dispose();
    }

    [Test, Description("Should return Campaign Map Biomes.")]
    public async Task GetCampaignMapBiomesAsync()
    {
        CampaignBiome[] loadedBiomes =
        [
            new CampaignBiome
            {
                BiomeTypeId = BiomeType.Forest,
                Name = "Invalid Color Biome",
                MovementCost = 1.0m,
                DoesBlockSight = true,
                IsWater = false,
                IsTraversable = true,
                RegenerationRate = 1.1m,
                BaseForageValue = 2,
                BaseHuntValue = 3,
                BaseFishValue = 4,
                BaseFodderValue = 5,
                BaseBoneValue = 6,
                BaseWoodValue = 7,
                BaseStoneValue = 8,
                BaseSpecialValue = 9,
                MapEditorHtmlColor = "not-a-color", // Invalid color
                CanBeElevated = false,
                IsPaintableOnCampaignMap = true
            },
            new CampaignBiome
            {
                BiomeTypeId = BiomeType.Arid,
                Name = "Arid",
                MovementCost = 1.0m,
                DoesBlockSight = false,
                IsWater = false,
                IsTraversable = false,
                RegenerationRate = 1.1m,
                BaseForageValue = 2,
                BaseHuntValue = 3,
                BaseFishValue = 4,
                BaseFodderValue = 5,
                BaseBoneValue = 6,
                BaseWoodValue = 7,
                BaseStoneValue = 8,
                BaseSpecialValue = 9,
                MapEditorHtmlColor = "#ffffff", // Invalid color
                CanBeElevated = true,
                IsPaintableOnCampaignMap = false
            },
            new CampaignBiome
            {
                BiomeTypeId = BiomeType.Ocean,
                Name = "Arid",
                MovementCost = 1.0m,
                DoesBlockSight = false,
                IsWater = false,
                IsTraversable = false,
                RegenerationRate = 1.1m,
                MapEditorHtmlColor = "#ffffcc", // Invalid color
                CanBeElevated = true,
                IsPaintableOnCampaignMap = false
            }
        ];
        _context.CampaignBiomes.AddRange(loadedBiomes);
        await _context.SaveChangesAsync();

        ServiceResult<IReadOnlyCollection<CampaignMapBiome>> result = await _handler.Handle(new GetCampaignMapBiomesRequest(), CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.True, "Biome fetch has not succeeded.");
            Assert.That(result.Errors, Is.Empty, "There were errors in the Biome fetch.");
            Assert.That(result.SuccessValue?.Count, Is.EqualTo(loadedBiomes.Length), "The count of results was not what was expected.");
        }

        foreach (CampaignMapBiome biome in result?.SuccessValue ?? [])
        {
            CampaignBiome? loadedBiome = loadedBiomes.FirstOrDefault(b => b.BiomeTypeId == biome.BiomeType);
            Assert.That(loadedBiome, Is.Not.Null, $"Biome {biome.Name} was not found in the loaded biomes.");

            using (Assert.EnterMultipleScope())
            {
                Assert.That(biome.Name, Is.EqualTo(loadedBiome.Name), $"Biome {biome.BiomeType} name does not match.");
                Assert.That(biome.MovementCost, Is.EqualTo(loadedBiome.MovementCost), $"Biome {biome.BiomeType} movement cost does not match.");
                Assert.That(biome.DoesBlockSight, Is.EqualTo(loadedBiome.DoesBlockSight), $"Biome {biome.BiomeType} sight blocking does not match.");
                Assert.That(biome.IsWater, Is.EqualTo(loadedBiome.IsWater), $"Biome {biome.BiomeType} water status does not match.");
                Assert.That(biome.IsTraversable, Is.EqualTo(loadedBiome.IsTraversable), $"Biome {biome.BiomeType} traversability does not match.");
                Assert.That(biome.RegenerationRate, Is.EqualTo(loadedBiome.RegenerationRate), $"Biome {biome.BiomeType} regeneration rate does not match.");
                Assert.That(biome.BaseForageValue, Is.EqualTo(loadedBiome.BaseForageValue), $"Biome {biome.BiomeType} forage value does not match.");
                Assert.That(biome.BaseHuntValue, Is.EqualTo(loadedBiome.BaseHuntValue), $"Biome {biome.BiomeType} hunt value does not match.");
                Assert.That(biome.BaseFishValue, Is.EqualTo(loadedBiome.BaseFishValue), $"Biome {biome.BiomeType} fish value does not match.");
                Assert.That(biome.BaseFodderValue, Is.EqualTo(loadedBiome.BaseFodderValue), $"Biome {biome.BiomeType} fodder value does not match.");
                Assert.That(biome.BaseBoneValue, Is.EqualTo(loadedBiome.BaseBoneValue), $"Biome {biome.BiomeType} bone value does not match.");
                Assert.That(biome.BaseWoodValue, Is.EqualTo(loadedBiome.BaseWoodValue), $"Biome {biome.BiomeType} wood value does not match.");
                Assert.That(biome.BaseStoneValue, Is.EqualTo(loadedBiome.BaseStoneValue), $"Biome {biome.BiomeType} stone value does not match.");
                Assert.That(biome.BaseSpecialValue, Is.EqualTo(loadedBiome.BaseSpecialValue), $"Biome {biome.BiomeType} special value does not match.");
                Assert.That(biome.MapEditorHtmlColor, Is.EqualTo(loadedBiome.MapEditorHtmlColor), $"Biome {biome.Name} MapEditorHtmlColor does not match the loaded value.");
                Assert.That(biome.CanBeElevated, Is.EqualTo(loadedBiome.CanBeElevated), $"Biome {biome.Name} CanBeElevated does not match the loaded value.");
                Assert.That(biome.IsPaintableOnCampaignMap, Is.EqualTo(loadedBiome.IsPaintableOnCampaignMap), $"Biome {biome.Name} IsPaintableOnCampaignMap does not match the loaded value.");
            }
        }
    }
}
