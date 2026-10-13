using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Maps.MapResources.Handlers;
using AscendantEmpire.BackendContracts.MapResources.GalleryPlacements;
using AscendantEmpire.Domain.Entities.Maps.CampaignMaps;
using AscendantEmpire.Domain.Entities.Maps.MapResources;
using AscendantEmpire.Domain.Enums.Maps;
using AscendantEmpire.Domain.Enums.MapResources;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Tests.Features.Maps.MapResources;

/// <summary>
/// Tests for placing MapResources on gallery map tiles in the map editor: which placements are refused (no habitat, a repeat, a full tile), the slots they
/// take, setting their specifics, and removing them.
/// </summary>
/// <remarks>
/// Containerised, as the other MapResource handler tests are: placements are joined to tiles and scenarios by hand, and the slot and one-per-type rules are
/// unique indexes the in-memory provider would not enforce.
/// </remarks>
[TestFixture]
internal class GalleryMapResourceHandlerTests : SqlServerTestBase
{
    private int _mapId;
    private int _otherMapId;
    private MapResourceTestCatalogue _catalogue = null!;
    private int _hillsOnlyTypeId;

    [SetUp]
    public async Task SeedMapAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        // Every seeded type has a Biome-wide Forest habitat.
        _catalogue = await MapResourceTestSeed.SeedCatalogueAsync(context);
        await MapResourceTestSeed.EnsureBiomeAsync(context, BiomeType.Woodland);
        await MapResourceTestSeed.EnsureGeographyAsync(context, CampaignMapGeographyType.Flat);
        await MapResourceTestSeed.EnsureGeographyAsync(context, CampaignMapGeographyType.Hilly);

        // A type that can only exist in Hilly Forest: a Geography-specific habitat and no Biome-wide one.
        var hillsOnly = new MapResourceType
        {
            Code = "TestHillHerb",
            DisplayName = "Hill Herb",
            MapResourceCategoryId = await context.MapResourceCategories.Select(c => c.Id).FirstAsync(),
            ExtractionMethod = ExtractionMethod.Gather,
            DiscoveryLikelihood = 1m,
            EstimationFuzzyFactorMin = 0.1m,
            EstimationFuzzyFactorMax = 0.2m,
            Yields = [new MapResourceTypeYield { AssetId = _catalogue.AssetId, AmountPerUnit = 1_000, IsEnabledByDefault = true }],
            Habitats = [new MapResourceHabitat { BiomeTypeId = BiomeType.Forest, CampaignMapGeographyTypeId = CampaignMapGeographyType.Hilly, InitialQuantityMin = 0, InitialQuantityMax = 1_000 }]
        };
        context.MapResourceTypes.Add(hillsOnly);
        await context.SaveChangesAsync();
        _hillsOnlyTypeId = hillsOnly.Id;

        // A three-tile map - Flat Forest, Hilly Forest, Flat Woodland - and a one-tile map to prove placements stay on their own map.
        _mapId = await SeedGalleryMapAsync(context, "Placement Map", startId: 1,
            (BiomeType.Forest, CampaignMapGeographyType.Flat),
            (BiomeType.Forest, CampaignMapGeographyType.Hilly),
            (BiomeType.Woodland, CampaignMapGeographyType.Flat));

        _otherMapId = await SeedGalleryMapAsync(context, "Other Map", startId: 100, (BiomeType.Forest, CampaignMapGeographyType.Flat));
    }

    #region Place
    [Test, Description("MapResources fill a tile's slots in order, and a third is refused.")]
    public async Task ShouldFillSlotsInOrderAndRefuseAThirdAsync()
    {
        ServiceResult<GalleryMapResource> first = await PlaceAsync(_mapId, 0, 0, _catalogue.BerryTypeId);
        ServiceResult<GalleryMapResource> second = await PlaceAsync(_mapId, 0, 0, _catalogue.WoodTypeId);
        ServiceResult<GalleryMapResource> third = await PlaceAsync(_mapId, 0, 0, _catalogue.CoalTypeId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(first.StatusCode, Is.EqualTo(ServiceResultStatus.Created), string.Join("; ", first.Errors));
            Assert.That(first.SuccessValue!.Slot, Is.EqualTo(1));
            Assert.That(second.SuccessValue!.Slot, Is.EqualTo(2));
            Assert.That(third.Errors, Has.Some.Contains("this one is full"));
        }
    }

    [Test, Description("A slot freed by a removal is the next one taken.")]
    public async Task ShouldReuseAFreedSlotAsync()
    {
        GalleryMapResource first = (await PlaceAsync(_mapId, 0, 0, _catalogue.BerryTypeId)).SuccessValue!;
        await PlaceAsync(_mapId, 0, 0, _catalogue.WoodTypeId);
        await DeleteAsync(_mapId, first.Id);

        ServiceResult<GalleryMapResource> replacement = await PlaceAsync(_mapId, 0, 0, _catalogue.CoalTypeId);

        Assert.That(replacement.SuccessValue!.Slot, Is.EqualTo(1));
    }

    [Test, Description("A tile holds at most one of each type.")]
    public async Task ShouldRefuseTheSameTypeTwiceOnATileAsync()
    {
        await PlaceAsync(_mapId, 0, 0, _catalogue.BerryTypeId);

        ServiceResult<GalleryMapResource> repeat = await PlaceAsync(_mapId, 0, 0, _catalogue.BerryTypeId);

        Assert.That(repeat.Errors, Has.Some.Contains("already has"));
    }

    [Test, Description("A type with no habitat for the tile's Biome can never exist there, so it cannot be placed there.")]
    public async Task ShouldRefuseATileWithNoHabitatForItsBiomeAsync()
    {
        ServiceResult<GalleryMapResource> woodland = await PlaceAsync(_mapId, 2, 0, _catalogue.BerryTypeId);

        Assert.That(woodland.Errors, Has.Some.Contains("cannot exist here"));
    }

    [Test, Description("A Geography-specific habitat allows its own Geography only.")]
    public async Task ShouldHonourAGeographySpecificHabitatAsync()
    {
        ServiceResult<GalleryMapResource> flat = await PlaceAsync(_mapId, 0, 0, _hillsOnlyTypeId);
        ServiceResult<GalleryMapResource> hilly = await PlaceAsync(_mapId, 1, 0, _hillsOnlyTypeId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(flat.Errors, Has.Some.Contains("cannot exist here"));
            Assert.That(hilly.StatusCode, Is.EqualTo(ServiceResultStatus.Created), string.Join("; ", hilly.Errors));
        }
    }

    [Test, Description("A tile outside the map is a 404.")]
    public async Task ShouldReportAMissingTileAsNotFoundAsync()
    {
        ServiceResult<GalleryMapResource> outside = await PlaceAsync(_mapId, 9, 9, _catalogue.BerryTypeId);

        Assert.That(outside.StatusCode, Is.EqualTo(ServiceResultStatus.NotFound));
    }
    #endregion

    #region Update and read
    [Test, Description("A single-quantity type takes a starting quantity, stored scaled and read back whole with the tile's coordinates.")]
    public async Task ShouldSetAStartingQuantityAsync()
    {
        GalleryMapResource placed = (await PlaceAsync(_mapId, 1, 0, _catalogue.BerryTypeId)).SuccessValue!;

        ServiceResult<GalleryMapResource> updated = await UpdateAsync(_mapId, placed.Id, new UpdateGalleryMapResourceDto { InitialQuantity = 250 });
        IReadOnlyCollection<GalleryMapResource> listed = await GetAsync(_mapId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(updated.SuccessValue!.InitialQuantity, Is.EqualTo(250), string.Join("; ", updated.Errors));
            Assert.That(listed.Single(), Is.EqualTo(updated.SuccessValue with { }));
            Assert.That(listed.Single().XCoord, Is.EqualTo(1));
            Assert.That(await ReadAsync(c => c.CampaignMapGalleryResources.Select(p => p.InitialQuantity).SingleAsync()), Is.EqualTo(250_000));
        }
    }

    [Test, Description("A Distribution takes a scenario by its number, and cannot be given a starting quantity.")]
    public async Task ShouldPinAScenarioOnADistributionAsync()
    {
        GalleryMapResource placed = (await PlaceAsync(_mapId, 0, 0, _catalogue.WoodTypeId)).SuccessValue!;

        ServiceResult<GalleryMapResource> pinned = await UpdateAsync(_mapId, placed.Id, new UpdateGalleryMapResourceDto { DistributionScenarioNumber = 1 });
        ServiceResult<GalleryMapResource> quantity = await UpdateAsync(_mapId, placed.Id, new UpdateGalleryMapResourceDto { InitialQuantity = 10 });
        ServiceResult<GalleryMapResource> unknown = await UpdateAsync(_mapId, placed.Id, new UpdateGalleryMapResourceDto { DistributionScenarioNumber = 7 });

        using (Assert.EnterMultipleScope())
        {
            Assert.That(pinned.SuccessValue!.DistributionScenarioNumber, Is.EqualTo(1), string.Join("; ", pinned.Errors));
            Assert.That(quantity.Errors, Has.Some.Contains("does not start from a single quantity"));
            Assert.That(unknown.Errors, Has.Some.Contains("has no scenario 7"));
        }
    }

    [Test, Description("A scenario only means something on a Distribution.")]
    public async Task ShouldRefuseAScenarioOnATypeThatIsNotADistributionAsync()
    {
        GalleryMapResource placed = (await PlaceAsync(_mapId, 0, 0, _catalogue.BerryTypeId)).SuccessValue!;

        ServiceResult<GalleryMapResource> result = await UpdateAsync(_mapId, placed.Id, new UpdateGalleryMapResourceDto { DistributionScenarioNumber = 1 });

        Assert.That(result.Errors, Has.Some.Contains("is not a Distribution"));
    }
    #endregion

    [Test, Description("A placement can only be changed or removed through its own map.")]
    public async Task ShouldNotReachAPlacementThroughAnotherMapAsync()
    {
        GalleryMapResource placed = (await PlaceAsync(_mapId, 0, 0, _catalogue.BerryTypeId)).SuccessValue!;

        ServiceResult<GalleryMapResource> update = await UpdateAsync(_otherMapId, placed.Id, new UpdateGalleryMapResourceDto { InitialQuantity = 5 });
        ServiceResultBase delete = await DeleteAsync(_otherMapId, placed.Id);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(update.StatusCode, Is.EqualTo(ServiceResultStatus.NotFound));
            Assert.That(delete.StatusCode, Is.EqualTo(ServiceResultStatus.NotFound));
            Assert.That(await GetAsync(_mapId), Has.Count.EqualTo(1));
        }
    }

    #region Helpers
    private async Task<ServiceResult<GalleryMapResource>> PlaceAsync(int mapId, int x, int y, int typeId)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();
        var handler = new PlaceGalleryMapResourceHandler(context);

        return await handler.Handle(new PlaceGalleryMapResourceRequest(mapId, new PlaceGalleryMapResourceDto { XCoord = x, YCoord = y, MapResourceTypeId = typeId }), CancellationToken.None);
    }

    private async Task<ServiceResult<GalleryMapResource>> UpdateAsync(int mapId, int placementId, UpdateGalleryMapResourceDto specifics)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();
        var handler = new UpdateGalleryMapResourceHandler(context);

        return await handler.Handle(new UpdateGalleryMapResourceRequest(mapId, placementId, specifics), CancellationToken.None);
    }

    private async Task<ServiceResultBase> DeleteAsync(int mapId, int placementId)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();
        var handler = new DeleteGalleryMapResourceHandler(context);

        return await handler.Handle(new DeleteGalleryMapResourceRequest(mapId, placementId), CancellationToken.None);
    }

    private async Task<IReadOnlyCollection<GalleryMapResource>> GetAsync(int mapId)
    {
        var handler = new GetGalleryMapResourcesHandler(DbFactory);

        return (await handler.Handle(new GetGalleryMapResourcesRequest(mapId), CancellationToken.None)).SuccessValue!;
    }

    private async Task<T> ReadAsync<T>(Func<AscendantEmpireContext, Task<T>> read)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        return await read(context);
    }

    /// <summary>
    /// Adds a gallery map one row high, with one tile per (Biome, Geography) given, ids from <paramref name="startId"/>.
    /// </summary>
    private static async Task<int> SeedGalleryMapAsync(AscendantEmpireContext context, string name, int startId, params (BiomeType Biome, CampaignMapGeographyType Geography)[] tiles)
    {
        var index = new CampaignMapGalleryIndex { MapName = name, Width = tiles.Length, Height = 1, Status = "Ready", PropertiesJson = "{}" };
        context.CampaignMapGalleryIndex.Add(index);
        await context.SaveChangesAsync();

        for (int x = 0; x < tiles.Length; x++)
        {
            (BiomeType biome, CampaignMapGeographyType geography) = tiles[x];

            context.CampaignMapGallery.Add(new CampaignMapGallery
            {
                CampaignMapGalleryId = startId + x,
                CampaignMapGalleryIndexId = index.Id,
                XCoord = x,
                YCoord = 0,
                BiomeTypeId = biome,
                CampaignMapGeographyTypeId = geography,
                Biome = await context.CampaignBiomes.SingleAsync(b => b.BiomeTypeId == biome),
                Geography = await context.CampaignGeographies.SingleAsync(g => g.CampaignMapGeographyTypeId == geography),
                Index = index
            });
        }

        await context.SaveChangesAsync();

        return index.Id;
    }
    #endregion
}
