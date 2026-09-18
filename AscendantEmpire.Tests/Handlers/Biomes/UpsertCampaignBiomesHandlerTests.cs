using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Maps.Biomes.Handlers;
using AscendantEmpire.Application.Helpers.ScaledNumerics;
using AscendantEmpire.Domain.Entities.Maps.CampaignMaps;
using AscendantEmpire.Domain.Enums.Maps;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

namespace AscendantEmpire.Tests.Handlers.Biomes;

internal class UpsertCampaignBiomesHandlerTests
{
    private static readonly DbContextOptions<AscendantEmpireContext> DbOptions = new DbContextOptionsBuilder<AscendantEmpireContext>()
        .UseInMemoryDatabase(nameof(UpsertCampaignBiomesHandlerTests), new InMemoryDatabaseRoot())
        .Options;

    private AscendantEmpireContext _context = null!;
    private UpsertCampaignMapBiomesHandler _handler = null!;
    private IMediator _mediator = null!;

    [SetUp]
    public async Task Setup()
    {
        _context = new AscendantEmpireContext(DbOptions);
        await _context.Database.EnsureDeletedAsync();
        await _context.Database.EnsureCreatedAsync();

        _mediator = Substitute.For<IMediator>();
        _handler = new UpsertCampaignMapBiomesHandler(new NullLogger<UpsertCampaignMapBiomesHandler>(), _context, _mediator);
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

        var updateRequest = new UpsertCampaignMapBiomesRequest(
        [
            new()
            {
                BiomeType = BiomeType.Arid,
                Name = "Not Arid",
                MovementCost = 2.0m,
                DoesBlockSight = true,
                IsWater = false,
                IsTraversable = true,
                RegenerationPercent = 0.2m,
                IsPaintableOnCampaignMap = true,
                MapEditorHtmlColor = "#fcfda4",
                CanBeElevated = true,
                BaseForageValue = 0,
                BaseHuntValue = 20,
                BaseFishValue = 30,
                BaseFodderValue = 40,
                BaseBoneValue = 50,
                BaseWoodValue = 60,
                BaseStoneValue = 70,
                BaseSpecialValue = 80,
            }
        ]);

        ServiceResultBase result = await _handler.Handle(updateRequest, CancellationToken.None);

        using (var verifyContext = new AscendantEmpireContext(DbOptions))
        {
            var updated = verifyContext.CampaignBiomes.Single(b => b.BiomeTypeId == BiomeType.Arid);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(updated.Name, Is.EqualTo("Not Arid"), $"{nameof(updated.Name)} value is not what's expected.");
                Assert.That(updated.MovementCost, Is.EqualTo(2.0m), $"{nameof(updated.MovementCost)} value is not what's expected.");
                Assert.That(updated.DoesBlockSight, Is.True, $"{nameof(updated.DoesBlockSight)} value is not what's expected.");
                Assert.That(updated.IsWater, Is.False, $"{nameof(updated.IsWater)} value is not what's expected.");
                Assert.That(updated.IsTraversable, Is.True, $"{nameof(updated.IsTraversable)} value is not what's expected.");
                Assert.That(updated.RegenerationPercent, Is.EqualTo(0.2m), $"{nameof(updated.RegenerationPercent)} value is not what's expected.");
                Assert.That(updated.IsPaintableOnCampaignMap, Is.True, $"{nameof(updated.IsPaintableOnCampaignMap)} value is not what's expected.");
                Assert.That(updated.MapEditorHtmlColor, Is.EqualTo("#fcfda4"), $"{nameof(updated.MapEditorHtmlColor)} value is not what's expected.");
                Assert.That(updated.CanBeElevated, Is.True, $"{nameof(updated.CanBeElevated)} value is not what's expected.");
                // The handler stores base values SCALED, so the persisted entity holds whole * ScaledAmount.Scale.
                Assert.That(updated.BaseForageValue, Is.EqualTo(ScaledAmount.FromWhole(0).RawValue), $"{nameof(updated.BaseForageValue)} value is not what's expected.");
                Assert.That(updated.BaseHuntValue, Is.EqualTo(ScaledAmount.FromWhole(20).RawValue), $"{nameof(updated.BaseHuntValue)} value is not what's expected.");
                Assert.That(updated.BaseFishValue, Is.EqualTo(ScaledAmount.FromWhole(30).RawValue), $"{nameof(updated.BaseFishValue)} value is not what's expected.");
                Assert.That(updated.BaseFodderValue, Is.EqualTo(ScaledAmount.FromWhole(40).RawValue), $"{nameof(updated.BaseFodderValue)} value is not what's expected.");
                Assert.That(updated.BaseBoneValue, Is.EqualTo(ScaledAmount.FromWhole(50).RawValue), $"{nameof(updated.BaseBoneValue)} value is not what's expected.");
                Assert.That(updated.BaseWoodValue, Is.EqualTo(ScaledAmount.FromWhole(60).RawValue), $"{nameof(updated.BaseWoodValue)} value is not what's expected.");
                Assert.That(updated.BaseStoneValue, Is.EqualTo(ScaledAmount.FromWhole(70).RawValue), $"{nameof(updated.BaseStoneValue)} value is not what's expected.");
                Assert.That(updated.BaseSpecialValue, Is.EqualTo(ScaledAmount.FromWhole(80).RawValue), $"{nameof(updated.BaseSpecialValue)} value is not what's expected.");
            }
        }
    }
}