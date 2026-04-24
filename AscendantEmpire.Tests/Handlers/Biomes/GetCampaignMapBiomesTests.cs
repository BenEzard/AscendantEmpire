using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Maps.Biomes.Handlers;
using AscendantEmpire.BackendContracts.Biomes;
using AscendantEmpire.Domain.Entities.Maps.CampaignMaps;
using AscendantEmpire.Domain.Enums.Maps;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging.Abstractions;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Tests.Handlers.Biomes;

internal class GetCampaignMapBiomesTests
{
    private static readonly InMemoryDatabaseRoot DbRoot = new();
    private static readonly DbContextOptions<AscendantEmpireContext> DbOptions = new DbContextOptionsBuilder<AscendantEmpireContext>()
        .UseInMemoryDatabase(nameof(GetCampaignMapBiomesTests), DbRoot)
        .Options;

    private IDbContextFactory<AscendantEmpireContext> _dbFactory = null!;
    private GetCampaignMapBiomesHandler _handler = null!;

    [SetUp]
    public async Task Setup()
    {
        _dbFactory = new TestDbContextFactory<AscendantEmpireContext>(DbOptions);

        // Ensure the store is clean + created (do it using a context from the factory).
        await using (var setupContext = await _dbFactory.CreateDbContextAsync(CancellationToken.None))
        {
            await setupContext.Database.EnsureDeletedAsync();
            await setupContext.Database.EnsureCreatedAsync();
        }

        // Handler now takes IDbContextFactory<AscendantEmpireContext>
        _handler = new GetCampaignMapBiomesHandler(
            new NullLogger<GetCampaignMapBiomesHandler>(),
            _dbFactory
        );
    }

    [TearDown]
    public void TearDown()
    {
        _dbFactory = null!;
        _handler = null!;
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

        // Seed using a context from the factory (NOT a field context), because the handler will read from a different newly-created context.
        await using (var seedContext = await _dbFactory.CreateDbContextAsync(CancellationToken.None))
        {
            seedContext.CampaignBiomes.AddRange(loadedBiomes);
            await seedContext.SaveChangesAsync();
        }

        ServiceResult<IReadOnlyCollection<CampaignMapBiome>> result =
            await _handler.Handle(new GetCampaignMapBiomesRequest(), CancellationToken.None);

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
