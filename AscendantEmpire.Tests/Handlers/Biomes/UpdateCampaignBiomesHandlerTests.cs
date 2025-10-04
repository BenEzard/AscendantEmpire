using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Handlers.Maps.Biomes;
using AscendantEmpire.Domain.Entities.Maps.CampaignMaps;
using AscendantEmpire.Domain.Enums.Maps;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace AscendantEmpire.Tests.Handlers.Biomes;

internal class UpdateCampaignBiomesHandlerTests
{
    private AscendantEmpireContext _context = null!;
    private UpdateCampaignMapBiomesHandler _handler = null!;
    private DbContextOptions<AscendantEmpireContext> _options;

    [SetUp]
    public async Task Setup()
    {
        _options = new DbContextOptionsBuilder<AscendantEmpireContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new AscendantEmpireContext(_options);
        await _context.Database.EnsureDeletedAsync();
        await _context.Database.EnsureCreatedAsync();

        _handler = new UpdateCampaignMapBiomesHandler(new NullLogger<UpdateCampaignMapBiomesHandler>(), _context);
    }

    [TearDown]
    public void TearDown()
    {
        _context?.Dispose();
    }

    [Test, Description("Valid update should succeed.")]
    public async Task ShouldUpdateValidBiomeAsync()
    {
        _context.CampaignBiomes.Add(new CampaignBiome { BiomeTypeId = BiomeType.Arid, Name = "Arid", MapEditorHtmlColor = "#c0c0c0" });
        _context.CampaignBiomes.Add(new CampaignBiome { BiomeTypeId = BiomeType.Ocean, Name = "Ocean", MapEditorHtmlColor = "#c0c0c1" });
        await _context.SaveChangesAsync();

        var updateRequest = new UpdateCampaignMapBiomesRequest(
        [
            new()
            {
                BiomeType = BiomeType.Arid,
                Name = "Not Arid",
                MovementCost = 2.0m,
                DoesBlockSight = true,
                IsWater = false,
                IsTraversable = true,
                RegenerationRate = 2m,
                IsPaintableOnCampaignMap = true,
                MapEditorHtmlColor = "#fcfda4",
                CanBeElevated = true,
                BaseForageValue = 1,
                BaseHuntValue = 2,
                BaseFishValue = 3,
                BaseFodderValue = 4,
                BaseBoneValue = 5,
                BaseWoodValue = 6,
                BaseStoneValue = 7,
                BaseSpecialValue = 8,
            }
        ]);

        ServiceResultBase result = await _handler.Handle(updateRequest, CancellationToken.None);

        using (var verifyContext = new AscendantEmpireContext(_options))
        {
            var updated = verifyContext.CampaignBiomes.Single(b => b.BiomeTypeId == BiomeType.Arid);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(updated.Name, Is.EqualTo("Not Arid"), $"{nameof(updated.Name)} value is not what's expected.");
                Assert.That(updated.MovementCost, Is.EqualTo(2.0m), $"{nameof(updated.MovementCost)} value is not what's expected.");
                Assert.That(updated.DoesBlockSight, Is.True, $"{nameof(updated.DoesBlockSight)} value is not what's expected.");
                Assert.That(updated.IsWater, Is.False, $"{nameof(updated.IsWater)} value is not what's expected.");
                Assert.That(updated.IsTraversable, Is.True, $"{nameof(updated.IsTraversable)} value is not what's expected.");
                Assert.That(updated.RegenerationRate, Is.EqualTo(2m), $"{nameof(updated.RegenerationRate)} value is not what's expected.");
                Assert.That(updated.IsPaintableOnCampaignMap, Is.True, $"{nameof(updated.IsPaintableOnCampaignMap)} value is not what's expected.");
                Assert.That(updated.MapEditorHtmlColor, Is.EqualTo("#fcfda4"), $"{nameof(updated.MapEditorHtmlColor)} value is not what's expected.");
                Assert.That(updated.CanBeElevated, Is.True, $"{nameof(updated.CanBeElevated)} value is not what's expected.");
                Assert.That(updated.BaseForageValue, Is.EqualTo(1), $"{nameof(updated.BaseForageValue)} value is not what's expected.");
                Assert.That(updated.BaseHuntValue, Is.EqualTo(2), $"{nameof(updated.BaseHuntValue)} value is not what's expected.");
                Assert.That(updated.BaseFishValue, Is.EqualTo(3), $"{nameof(updated.BaseFishValue)} value is not what's expected.");
                Assert.That(updated.BaseFodderValue, Is.EqualTo(4), $"{nameof(updated.BaseFodderValue)} value is not what's expected.");
                Assert.That(updated.BaseBoneValue, Is.EqualTo(5), $"{nameof(updated.BaseBoneValue)} value is not what's expected.");
                Assert.That(updated.BaseWoodValue, Is.EqualTo(6), $"{nameof(updated.BaseWoodValue)} value is not what's expected.");
                Assert.That(updated.BaseStoneValue, Is.EqualTo(7), $"{nameof(updated.BaseStoneValue)} value is not what's expected.");
                Assert.That(updated.BaseSpecialValue, Is.EqualTo(8), $"{nameof(updated.BaseSpecialValue)} value is not what's expected.");
            }
        }
    }

    [Test, Description("Should fail if the Biome is not in the db.")]
    public async Task ShouldFailWhenBiomeDoesNotExistAsync()
    {
        _context.CampaignBiomes.Add(new CampaignBiome { BiomeTypeId = BiomeType.Arid, Name = "Arid", MapEditorHtmlColor = "#c0c0c0" });
        await _context.SaveChangesAsync();

        var request = new UpdateCampaignMapBiomesRequest(
        [
            new()
            {
                BiomeType = BiomeType.Coastal,
                Name = "Arid Duplicate",
                MovementCost = 1.0m,
                DoesBlockSight = false,
                IsWater = false,
                IsTraversable = true,
                RegenerationRate = 1.15m,
                MapEditorHtmlColor = "#000000",
                CanBeElevated = false,
                IsPaintableOnCampaignMap = true
            }
        ]);

        ServiceResultBase result = await _handler.Handle(request, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False, "Biome update succeeded, when it wasn't expected to.");
            Assert.That(result.Errors, Is.Not.Null.And.Not.Empty, "Expected error messages when duplicate biome exists.");
        }
    }
}