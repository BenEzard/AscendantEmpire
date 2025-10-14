using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Handlers.Maps.Geographies;
using AscendantEmpire.BackendContracts.Geographies;
using AscendantEmpire.Domain.Entities.Maps.CampaignMaps;
using AscendantEmpire.Domain.Enums.Maps;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Tests.Handlers.Geographies;

internal class GetCampaignMapGeographiesTests
{
    private AscendantEmpireContext _context = null!;
    private GetCampaignMapGeographiesHandler _handler = null!;

    [SetUp]
    public async Task Setup()
    {
        var options = new DbContextOptionsBuilder<AscendantEmpireContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new AscendantEmpireContext(options);
        await _context.Database.EnsureDeletedAsync();
        await _context.Database.EnsureCreatedAsync();

        _handler = new GetCampaignMapGeographiesHandler(new NullLogger<GetCampaignMapGeographiesHandler>(), _context);
    }

    [TearDown]
    public void TearDown()
    {
        _context?.Dispose();
    }

    [Test, Description("Should return Campaign Map Geographies.")]
    public async Task GetCampaignMapGeographiesAsync()
    {
        CampaignGeography[] loadedGeographies =
        [
            new CampaignGeography
            {
                CampaignMapGeographyTypeId = CampaignMapGeographyType.DeepWater,
                RelativeElevation = 0,
                Name = "Invalid Color Geography",
                MovementCost = 1.0m,
                MapEditorIcon = "not-a-icon",
                IsPaintableOnCampaignMap = true
            },
            new CampaignGeography
            {
                CampaignMapGeographyTypeId = CampaignMapGeographyType.ShallowWater,
                RelativeElevation = 1,
                Name = "Forest",
                MovementCost = 1.0m,
                MapEditorIcon = "forest-icon",
                IsPaintableOnCampaignMap = false
            },
        ];

        _context.CampaignGeographies.AddRange(loadedGeographies);

        await _context.SaveChangesAsync();

        ServiceResult<IReadOnlyCollection<CampaignMapGeography>> result = await _handler.Handle(new GetCampaignMapGeographiesRequest(), CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.True, "Campaign Map Geography fetch has not succeeded.");
            Assert.That(result.Errors, Is.Empty, "There were errors in the Campaign Map Geography fetch.");
            Assert.That(result.SuccessValue?.Count ?? 0, Is.EqualTo(loadedGeographies.Length), "The count of results was not what was expected.");
        }

        foreach (CampaignMapGeography geographies in result?.SuccessValue ?? [])
        {
            CampaignGeography? loadedGeography = loadedGeographies.FirstOrDefault(b => b.CampaignMapGeographyTypeId == geographies.CampaignMapGeographyType);
            Assert.That(loadedGeography, Is.Not.Null, $"Biome {geographies.Name} was not found in the loaded Campaign Map Geographies.");

            using (Assert.EnterMultipleScope())
            {
                Assert.That(geographies.CampaignMapGeographyType, Is.EqualTo((CampaignMapGeographyType)loadedGeography.CampaignMapGeographyTypeId), "Campaign Map Geography Type does not match.");
                Assert.That(geographies.RelativeElevation, Is.EqualTo(loadedGeography.RelativeElevation), "Relative Elevation does not match.");
                Assert.That(geographies.Name, Is.EqualTo(loadedGeography.Name), "Name does not match.");
                Assert.That(geographies.MovementCost, Is.EqualTo(loadedGeography.MovementCost), "Movement Cost does not match.");
                Assert.That(geographies.MapEditorIcon, Is.EqualTo(loadedGeography.MapEditorIcon), "Map Editor Icon does not match.");
                Assert.That(geographies.IsPaintableOnCampaignMap, Is.EqualTo(loadedGeography.IsPaintableOnCampaignMap), "Is Paintable On Campaign Map does not match.");
            }
        }
    }
}