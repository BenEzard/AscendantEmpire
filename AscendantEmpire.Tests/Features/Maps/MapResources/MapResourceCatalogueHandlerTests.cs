using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Maps.MapResources.Handlers;
using AscendantEmpire.Application.Features.Resources.Handlers;
using AscendantEmpire.BackendContracts.MapResources.AddOrUpdateMapResourceType;
using AscendantEmpire.BackendContracts.MapResources.GetMapResourceCatalogue;
using AscendantEmpire.BackendContracts.MapResources.PutMapResourceCatalogue;
using AscendantEmpire.BackendContracts.Resources;
using AscendantEmpire.Domain.Entities.Maps.CampaignMaps;
using AscendantEmpire.Domain.Entities.Maps.MapResources;
using AscendantEmpire.Domain.Entities.Resources;
using AscendantEmpire.Domain.Enums.Maps;
using AscendantEmpire.Domain.Enums.MapResources;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Tests.Features.Maps.MapResources;

/// <summary>
/// Tests for the MapResource Type Manager's handlers: saving the catalogue (categories and types by code, children updated in place), reading it back grouped, the two
/// deletes, and the Resource list its pickers use.
/// </summary>
/// <remarks>
/// Containerised rather than in-memory: the handlers lean on the database for owned children cascading on delete, codes matching case-insensitively under the unique
/// indexes, and foreign keys refusing a Distribution scenario a map still points at - none of which the in-memory provider reproduces.
/// </remarks>
[TestFixture]
internal class MapResourceCatalogueHandlerTests : SqlServerTestBase
{
    private int _berriesId;
    private int _peltId;
    private int _oakId;

    [SetUp]
    public async Task SeedResourcesAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        // Biomes and Geographies are seeded by the Entity Setup Tool, so the template database may not have the ones habitats name.
        await MapResourceTestSeed.EnsureBiomeAsync(context, BiomeType.Forest);
        await MapResourceTestSeed.EnsureBiomeAsync(context, BiomeType.Woodland);
        await MapResourceTestSeed.EnsureGeographyAsync(context, CampaignMapGeographyType.Hilly);

        List<Resource> resources = await MapResourceTestSeed.SeedResourcesAsync(context, "Berries", "Pelt", "Oak");

        _berriesId = resources[0].Id;
        _peltId = resources[1].Id;
        _oakId = resources[2].Id;
    }

    #region Put and Get
    [Test, Description("A new category and a type in it can be saved in one request, and read back grouped under it with every value as it was sent.")]
    public async Task ShouldSaveANewCategoryAndTypeTogetherAndReadThemBackAsync()
    {
        ServiceResultBase result = await PutAsync(Catalogue([Category("Food")], [Berry()]));

        MapResourceCatalogue catalogue = await GetAsync();

        MapResourceCategoryDetail food = catalogue.Categories.Single(c => c.Code == "Food");
        MapResourceTypeDetail berry = food.Types.Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.True, string.Join("; ", result.Errors));
            Assert.That(berry.Code, Is.EqualTo("Berry"));
            Assert.That(berry.MapResourceCategoryCode, Is.EqualTo("Food"));
            Assert.That(berry.IconPath, Is.EqualTo("/icons/berry.png"));
            Assert.That(berry.Yields.Single().AmountPerUnit, Is.EqualTo(2), "Whole numbers go in scaled and come back whole.");
            Assert.That(berry.Habitats, Has.Count.EqualTo(2));
            Assert.That(berry.Habitats.First().GeographyType, Is.Null, "The Biome-wide habitat reads before the Geography-specific one.");
            Assert.That(berry.Habitats.First().MaximumQuantity, Is.EqualTo(500));
            Assert.That(berry.Habitats.First().GrowthPercentage, Is.EqualTo(0.1m));
        }

        // Stored scaled, as every other amount on the campaign map.
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();
        MapResourceHabitat stored = await context.MapResourceHabitats.FirstAsync(h => h.CampaignMapGeographyTypeId == null);

        Assert.That(stored.MaximumQuantity, Is.EqualTo(500_000));
    }

    [Test, Description("Saving again updates children in place: a removed habitat goes, a kept one keeps its row, and a changed yield changes.")]
    public async Task ShouldUpdateChildrenInPlaceAsync()
    {
        await PutAsync(Catalogue([Category("Food")], [Berry()]));

        int keptHabitatId = await ReadAsync(c => c.MapResourceHabitats.Where(h => h.CampaignMapGeographyTypeId == null).Select(h => h.Id).SingleAsync());

        AddOrUpdateMapResourceTypeDto changed = Berry() with
        {
            Yields = [Yield(_berriesId, amount: 5), Yield(_peltId, amount: 1)],
            Habitats = [ForestHabitat()],
        };

        ServiceResultBase result = await PutAsync(Catalogue([], [changed]));

        MapResourceTypeDetail berry = (await GetAsync()).Categories.Single().Types.Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.True, string.Join("; ", result.Errors));
            Assert.That(berry.Habitats, Has.Count.EqualTo(1));
            Assert.That(berry.Yields.Select(y => (y.ResourceId, y.AmountPerUnit)), Is.EquivalentTo(new[] { (_berriesId, 5), (_peltId, 1) }));
            Assert.That(await ReadAsync(c => c.MapResourceHabitats.Select(h => h.Id).SingleAsync()), Is.EqualTo(keptHabitatId), "The kept habitat is updated, not replaced.");
        }
    }

    [Test, Description("Codes match case-insensitively, as the unique indexes compare them, so a differently-cased code updates rather than colliding.")]
    public async Task ShouldMatchCodesCaseInsensitivelyAsync()
    {
        await PutAsync(Catalogue([Category("Food")], [Berry()]));

        ServiceResultBase result = await PutAsync(Catalogue([Category("food") with { DisplayName = "Foodstuffs" }], [Berry() with { Code = "berry", DisplayName = "Wild Berry" }]));

        MapResourceCatalogue catalogue = await GetAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.True, string.Join("; ", result.Errors));
            Assert.That(catalogue.Categories.Single().DisplayName, Is.EqualTo("Foodstuffs"));
            Assert.That(catalogue.Categories.Single().Types.Single().DisplayName, Is.EqualTo("Wild Berry"));
        }
    }

    [Test, Description("A Distribution scenario that is saved again keeps its row, since map placements and game instances point at it by id.")]
    public async Task ShouldKeepADistributionScenarioIdAcrossSavesAsync()
    {
        await PutAsync(Catalogue([Category("Wood")], [ForestWood(amount: 200)]));
        int scenarioId = await ReadAsync(c => c.MapResourceDistributionScenarios.Select(s => s.Id).SingleAsync());

        ServiceResultBase result = await PutAsync(Catalogue([], [ForestWood(amount: 300)]));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.True, string.Join("; ", result.Errors));
            Assert.That(await ReadAsync(c => c.MapResourceDistributionScenarios.Select(s => s.Id).SingleAsync()), Is.EqualTo(scenarioId));
            Assert.That(await ReadAsync(c => c.MapResourceDistributionScenarioAmounts.Select(a => a.Amount).SingleAsync()), Is.EqualTo(300_000));
        }
    }

    [Test, Description("Removing a Distribution scenario a map still places is refused with a message, not a server error.")]
    public async Task ShouldRefuseToRemoveAScenarioAMapPlacesAsync()
    {
        await PutAsync(Catalogue([Category("Wood")], [ForestWood(amount: 200) with { DistributionScenarios = [Scenario(1, 200), Scenario(2, 100)] }]));
        await PlaceOnAGalleryTileAsync("ForestWood", scenarioNumber: 2);

        ServiceResultBase result = await PutAsync(Catalogue([], [ForestWood(amount: 200)]));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.GenericError));
            Assert.That(result.Errors, Has.Some.Contains("must not be in use by any map or game"));
        }
    }

    [Test, Description("A type naming a category that neither exists nor is in the request is refused.")]
    public async Task ShouldRejectAnUnknownCategoryAsync()
    {
        ServiceResultBase result = await PutAsync(Catalogue([], [Berry()]));

        Assert.That(result.Errors, Has.Some.Contains("there is no category 'Food'"));
    }

    [Test, Description("A yield naming a Resource that does not exist is refused, rather than failing on the foreign key.")]
    public async Task ShouldRejectAnUnknownResourceAsync()
    {
        ServiceResultBase result = await PutAsync(Catalogue([Category("Food")], [Berry() with { Yields = [Yield(999_999, amount: 1)] }]));

        Assert.That(result.Errors, Has.Some.Contains("There is no Resource 999999"));
    }

    [Test, Description("The type validator runs on every type in the request.")]
    public async Task ShouldRejectAnInvalidTypeAsync()
    {
        ServiceResultBase result = await PutAsync(Catalogue([Category("Food")], [Berry() with { IsStratified = true }]));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.GenericError));
            Assert.That(await ReadAsync(c => c.MapResourceTypes.AnyAsync()), Is.False, "Nothing is saved when anything is invalid.");
        }
    }
    #endregion

    #region Delete
    [Test, Description("Deleting an unused type removes it and everything it owned.")]
    public async Task ShouldDeleteAnUnusedTypeWithItsChildrenAsync()
    {
        await PutAsync(Catalogue([Category("Food")], [Berry()]));

        ServiceResultBase result = await DeleteTypeAsync("Berry");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.True, string.Join("; ", result.Errors));
            Assert.That(await ReadAsync(c => c.MapResourceTypes.AnyAsync()), Is.False);
            Assert.That(await ReadAsync(c => c.MapResourceHabitats.AnyAsync()), Is.False);
            Assert.That(await ReadAsync(c => c.MapResourceTypeYields.AnyAsync()), Is.False);
        }
    }

    [Test, Description("A type placed on a map cannot be deleted, and the message says why.")]
    public async Task ShouldRefuseToDeleteATypePlacedOnAMapAsync()
    {
        await PutAsync(Catalogue([Category("Wood")], [ForestWood(amount: 200)]));
        await PlaceOnAGalleryTileAsync("ForestWood", scenarioNumber: null);

        ServiceResultBase result = await DeleteTypeAsync("ForestWood");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Errors, Has.Some.Contains("it is placed on 1 map tile(s)"));
            Assert.That(await ReadAsync(c => c.MapResourceTypes.AnyAsync()), Is.True);
        }
    }

    [Test, Description("Deleting a type that does not exist is a 404.")]
    public async Task ShouldReportAMissingTypeAsNotFoundAsync()
    {
        ServiceResultBase result = await DeleteTypeAsync("Nothing");

        Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.NotFound));
    }

    [Test, Description("A category with types in it cannot be deleted; an empty one can.")]
    public async Task ShouldOnlyDeleteAnEmptyCategoryAsync()
    {
        await PutAsync(Catalogue([Category("Food"), Category("Metals")], [Berry()]));

        ServiceResultBase refused = await DeleteCategoryAsync("Food");
        ServiceResultBase deleted = await DeleteCategoryAsync("Metals");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(refused.Errors, Has.Some.Contains("still has 1 MapResourceType(s)"));
            Assert.That(deleted.HasSucceeded, Is.True, string.Join("; ", deleted.Errors));
            Assert.That(await ReadAsync(c => c.MapResourceCategories.Select(cat => cat.Code).ToListAsync()), Is.EqualTo(new[] { "Food" }));
        }
    }
    #endregion

    [Test, Description("Every Resource is listed with its meta category, for the pickers.")]
    public async Task ShouldListEveryResourceAsync()
    {
        var handler = new GetResourcesHandler(new NullLogger<GetResourcesHandler>(), DbFactory);

        ServiceResult<IReadOnlyCollection<ResourceSummary>> result = await handler.Handle(new GetResourcesRequest(), CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.SuccessValue!.Select(r => r.DisplayName), Is.EqualTo(new[] { "Berries", "Oak", "Pelt" }), "Within a meta category, by name.");
            Assert.That(result.SuccessValue!.Select(r => r.MetaCategoryDisplayName), Is.All.EqualTo("Test Meta"));
        }
    }

    #region Handler calls
    private async Task<ServiceResultBase> PutAsync(PutMapResourceCatalogueDto catalogue)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();
        var handler = new PutMapResourceCatalogueHandler(new NullLogger<PutMapResourceCatalogueHandler>(), context);

        return await handler.Handle(new PutMapResourceCatalogueRequest(catalogue), CancellationToken.None);
    }

    private async Task<MapResourceCatalogue> GetAsync()
    {
        var handler = new GetMapResourceCatalogueHandler(new NullLogger<GetMapResourceCatalogueHandler>(), DbFactory);

        ServiceResult<MapResourceCatalogue> result = await handler.Handle(new GetMapResourceCatalogueRequest(), CancellationToken.None);

        return result.SuccessValue!;
    }

    private async Task<ServiceResultBase> DeleteTypeAsync(string code)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();
        var handler = new DeleteMapResourceTypeHandler(new NullLogger<DeleteMapResourceTypeHandler>(), context);

        return await handler.Handle(new DeleteMapResourceTypeRequest(code), CancellationToken.None);
    }

    private async Task<ServiceResultBase> DeleteCategoryAsync(string code)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();
        var handler = new DeleteMapResourceCategoryHandler(new NullLogger<DeleteMapResourceCategoryHandler>(), context);

        return await handler.Handle(new DeleteMapResourceCategoryRequest(code), CancellationToken.None);
    }

    private async Task<T> ReadAsync<T>(Func<AscendantEmpireContext, Task<T>> read)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        return await read(context);
    }

    /// <summary>
    /// Places the type on the first tile of a one-tile gallery map, optionally pinning one of its scenarios.
    /// </summary>
    private async Task PlaceOnAGalleryTileAsync(string typeCode, int? scenarioNumber)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        var index = new CampaignMapGalleryIndex { MapName = "Placement Map", Width = 1, Height = 1, Status = "Ready", PropertiesJson = "{}" };
        context.CampaignMapGalleryIndex.Add(index);
        await context.SaveChangesAsync();

        var tile = new CampaignMapGallery
        {
            CampaignMapGalleryId = 1,
            CampaignMapGalleryIndexId = index.Id,
            BiomeTypeId = BiomeType.Forest,
            CampaignMapGeographyTypeId = CampaignMapGeographyType.Hilly,
            Biome = await context.CampaignBiomes.SingleAsync(b => b.BiomeTypeId == BiomeType.Forest),
            Geography = await context.CampaignGeographies.SingleAsync(g => g.CampaignMapGeographyTypeId == CampaignMapGeographyType.Hilly),
            Index = index
        };
        context.CampaignMapGallery.Add(tile);

        MapResourceType type = await context.MapResourceTypes.Include(t => t.DistributionScenarios).SingleAsync(t => t.Code == typeCode);

        context.CampaignMapGalleryResources.Add(new CampaignMapGalleryResource
        {
            CampaignMapGalleryId = tile.CampaignMapGalleryId,
            MapResourceTypeId = type.Id,
            Slot = 1,
            MapResourceDistributionScenarioId = scenarioNumber == null ? null : type.DistributionScenarios.Single(s => s.ScenarioNumber == scenarioNumber).Id
        });

        await context.SaveChangesAsync();
    }
    #endregion

    #region Builders
    private static PutMapResourceCatalogueDto Catalogue(AddOrUpdateMapResourceCategoryDto[] categories, AddOrUpdateMapResourceTypeDto[] types)
    {
        return new PutMapResourceCatalogueDto { Categories = categories, Types = types };
    }

    private static AddOrUpdateMapResourceCategoryDto Category(string code)
    {
        return new AddOrUpdateMapResourceCategoryDto { Code = code, DisplayName = code, SortOrder = 10 };
    }

    /// <summary>
    /// A renewable, spreading type with one yield, and a Forest habitat plus a Hilly Forest one.
    /// </summary>
    private AddOrUpdateMapResourceTypeDto Berry()
    {
        return new AddOrUpdateMapResourceTypeDto
        {
            Code = "Berry",
            DisplayName = "Berry",
            MapResourceCategoryCode = "Food",
            IconPath = "/icons/berry.png",
            IsRenewable = true,
            IsStratified = false,
            IsDistribution = false,
            ExtractionMethod = ExtractionMethod.Gather,
            RequiresResearchToExtract = true,
            IsDiscoverableByDefault = true,
            DiscoveryLikelihood = 1m,
            EstimationFuzzyFactorMin = 0.1m,
            EstimationFuzzyFactorMax = 0.3m,
            SurvivalRounds = 5,
            ReseedLikelihood = 0.2m,
            Yields = [Yield(_berriesId, amount: 2)],
            Habitats = [ForestHabitat(), ForestHabitat() with { GeographyType = CampaignMapGeographyType.Hilly }],
        };
    }

    private static MapResourceHabitatDto ForestHabitat()
    {
        return new MapResourceHabitatDto
        {
            BiomeType = BiomeType.Forest,
            InitialQuantityMin = 200,
            InitialQuantityMax = 400,
            MaximumQuantity = 500,
            GrowthPercentage = 0.1m,
            SpreadLikelihood = 0.1m,
            SpreadLimitMin = 2,
            SpreadLimitMax = 5,
        };
    }

    private static MapResourceTypeYieldDto Yield(int resourceId, int amount)
    {
        return new MapResourceTypeYieldDto { ResourceId = resourceId, AmountPerUnit = amount, IsEnabledByDefault = true };
    }

    /// <summary>
    /// A Distribution of oak with one scenario, in Woodland.
    /// </summary>
    private AddOrUpdateMapResourceTypeDto ForestWood(int amount)
    {
        return new AddOrUpdateMapResourceTypeDto
        {
            Code = "ForestWood",
            DisplayName = "Forest Wood",
            MapResourceCategoryCode = "Wood",
            IsRenewable = true,
            IsStratified = false,
            IsDistribution = true,
            ExtractionMethod = ExtractionMethod.Fell,
            RequiresResearchToExtract = false,
            IsDiscoverableByDefault = true,
            DiscoveryLikelihood = 1m,
            EstimationFuzzyFactorMin = 0.1m,
            EstimationFuzzyFactorMax = 0.2m,
            SurvivalRounds = 5,
            ReseedLikelihood = 0.1m,
            Habitats = [new MapResourceHabitatDto { BiomeType = BiomeType.Woodland, MaximumQuantity = 20_000, GrowthPercentage = 0.05m }],
            DistributionScenarios = [Scenario(1, amount)],
        };
    }

    private MapResourceDistributionScenarioDto Scenario(int number, int oak)
    {
        return new MapResourceDistributionScenarioDto
        {
            ScenarioNumber = number,
            Amounts = [new MapResourceDistributionScenarioAmountDto { ResourceId = _oakId, Amount = oak }],
            BiomeWeights = [new MapResourceDistributionScenarioBiomeWeightDto { BiomeType = BiomeType.Woodland, Weight = 1 }],
        };
    }
    #endregion
}
