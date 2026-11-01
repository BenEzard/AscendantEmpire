using AscendantEmpire.Application.Data;
using AscendantEmpire.BackendContracts.Geographies;
using AscendantEmpire.Domain.Entities.Maps.CampaignMaps;
using AscendantEmpire.Domain.Enums.Maps;
using Microsoft.EntityFrameworkCore;

using static AscendantEmpire.Application.Common.ServiceResultBase;
using Microsoft.EntityFrameworkCore.Storage;
using AscendantEmpire.Application.Features.Maps.Geographies.Handlers;

namespace AscendantEmpire.Tests.Handlers.Geographies;

internal class GetCampaignMapGeographiesTests
{
    private static readonly InMemoryDatabaseRoot DbRoot = new();
    private static readonly DbContextOptions<AscendantEmpireContext> DbOptions = new DbContextOptionsBuilder<AscendantEmpireContext>()
        .UseInMemoryDatabase(nameof(GetCampaignMapGeographiesTests), DbRoot)
        .Options;

    private IDbContextFactory<AscendantEmpireContext> _dbFactory = null!;
    private GetCampaignMapGeographiesHandler _handler = null!;

    [SetUp]
    public async Task Setup()
    {
        _dbFactory = new TestDbContextFactory<AscendantEmpireContext>(DbOptions);
        
        await using (var setupContext = await _dbFactory.CreateDbContextAsync(CancellationToken.None))
        {
            await setupContext.Database.EnsureDeletedAsync();
            await setupContext.Database.EnsureCreatedAsync();
        }
        
        _handler = new GetCampaignMapGeographiesHandler(_dbFactory);
    }

    [TearDown]
    public void TearDown()
    {
        // contexts are created/disposed per test via the factory
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
                IsWater = true,
                IsTraversable = true,
                MapEditorIcon = "not-a-icon",
                IsPaintableOnCampaignMap = true
            },
            new CampaignGeography
            {
                CampaignMapGeographyTypeId = CampaignMapGeographyType.ShallowWater,

                // Below sea level, as CampaignMapGeographyValidator requires of any water geography: a fixture the catalogue API would refuse is a fixture
                // that stops describing the system it stands in for.
                RelativeElevation = -1,
                Name = "Forest",
                MovementCost = 1.0m,
                IsWater = true,
                IsTraversable = true,
                MapEditorIcon = "forest-icon",
                IsPaintableOnCampaignMap = false
            },
        ];

        await using (var seedContext = await _dbFactory.CreateDbContextAsync(CancellationToken.None))
        {
            seedContext.CampaignGeographies.AddRange(loadedGeographies);
            await seedContext.SaveChangesAsync();
        }

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

    [Test, Description("Pins that both terrain flags reach the client, since the Geography Manager round-trips this exact shape straight back into the PUT.")]
    public async Task ShouldReturnTheWaterAndTraversableFlagsAsync()
    {
        await using (var seedContext = await _dbFactory.CreateDbContextAsync(CancellationToken.None))
        {
            seedContext.CampaignGeographies.AddRange(
                new CampaignGeography
                {
                    CampaignMapGeographyTypeId = CampaignMapGeographyType.DeepWater,
                    RelativeElevation = -2,
                    Name = "Deep Water",
                    MovementCost = 1.0m,
                    MapEditorIcon = null,
                    IsPaintableOnCampaignMap = true,
                    IsWater = true,
                    IsTraversable = true
                },
                new CampaignGeography
                {
                    CampaignMapGeographyTypeId = CampaignMapGeographyType.Mountainous,
                    RelativeElevation = 3,
                    Name = "Mountainous",
                    MovementCost = CampaignGeography.IMPASSABLE_MOVEMENT_COST,
                    MapEditorIcon = null,
                    IsPaintableOnCampaignMap = true,
                    IsWater = false,
                    IsTraversable = false
                });

            await seedContext.SaveChangesAsync();
        }

        ServiceResult<IReadOnlyCollection<CampaignMapGeography>> result = await _handler.Handle(new GetCampaignMapGeographiesRequest(), CancellationToken.None);

        CampaignMapGeography? deepWater = result.SuccessValue?.SingleOrDefault(g => g.CampaignMapGeographyType == CampaignMapGeographyType.DeepWater);
        CampaignMapGeography? mountainous = result.SuccessValue?.SingleOrDefault(g => g.CampaignMapGeographyType == CampaignMapGeographyType.Mountainous);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(deepWater, Is.Not.Null, "Deep Water was not returned.");
            Assert.That(mountainous, Is.Not.Null, "Mountainous was not returned.");
        }

        using (Assert.EnterMultipleScope())
        {
            Assert.That(deepWater!.IsWater, Is.True, "Deep Water should be water.");
            Assert.That(deepWater.IsTraversable, Is.True, "Deep Water should be traversable - a ship enters it.");
            Assert.That(mountainous!.IsWater, Is.False, "Mountainous should not be water.");
            Assert.That(mountainous.IsTraversable, Is.False, "Mountainous should not be traversable.");
        }
    }
}