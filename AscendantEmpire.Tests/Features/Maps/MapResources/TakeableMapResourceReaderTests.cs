using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Maps.MapResources.Classes;
using AscendantEmpire.Application.Features.Maps.MapResources.Services;
using AscendantEmpire.Application.Helpers.ScaledNumerics;
using AscendantEmpire.Domain.Entities.Assets;
using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Entities.Maps.MapResources;
using AscendantEmpire.Domain.Enums;
using AscendantEmpire.Domain.Enums.Assets;
using AscendantEmpire.Domain.Enums.MapResources;
using AscendantEmpire.Domain.Enums.Modifiers;

using Microsoft.Extensions.Logging.Abstractions;

namespace AscendantEmpire.Tests.Features.Maps.MapResources;

/// <summary>
/// Tests for <see cref="TakeableMapResourceReader"/>: which of a tile's MapResources a Player's Force could load up from, and what each is worth.
/// </summary>
/// <remarks>
/// <para>
/// On the in-memory tier, per <c>testing.md</c>: the reader is joins, a switch lookup and a grouping, with no transaction, no raw SQL and no database
/// constraint in play.
/// </para>
/// <para>
/// <b>No Game, Player or tile rows are written.</b> Foreign keys are not enforced in memory and the reader reads none of those tables, so the ids below are
/// bare numbers. The catalogue it does read — types, yields and Assets — is seeded in full.
/// </para>
/// </remarks>
[TestFixture]
internal class TakeableMapResourceReaderTests : InMemoryTestBase
{
    private const int _GAME_ID = 1;
    private const int _PLAYER_ID = 2;
    private const int _OTHER_PLAYER_ID = 3;
    private const int _TILE_ID = 112;
    private const int _OTHER_TILE_ID = 113;

    /// <summary>1.0 of the Asset per unit of the MapResource, so a take's arithmetic reads as the units themselves.</summary>
    private const long _ONE_PER_UNIT = 1_000;

    private TakeableMapResourceReader _reader = null!;

    private int _berriesAssetId;
    private int _hidesAssetId;

    [SetUp]
    public async Task SeedCatalogueAsync()
    {
        _reader = new TakeableMapResourceReader(NullLogger<TakeableMapResourceReader>.Instance);

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        _berriesAssetId = await AddAssetAsync(context, "Foodstuffs.PlantFoods.Berries", "Berries");
        _hidesAssetId = await AddAssetAsync(context, "RawMaterials.RawMaterials.Hides", "Hides");
    }

    #region What is takeable

    [Test, Description("The ordinary case: a discovered patch with a quantity and a live yield comes back with the figures the dialog and the write both price from.")]
    public async Task ShouldReturnADiscoveredPatchWithItsYieldAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        int typeId = await AddTypeAsync(context, "berries", "Berry Patch", [(_berriesAssetId, _ONE_PER_UNIT, true)], iconPath: "/berries.png");
        await AddDiscoveredInstanceAsync(context, typeId, quantityWhole: 500);

        TakeableMapResource resource = (await ReadTileAsync(context)).Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(resource.MapResourceTypeId, Is.EqualTo(typeId));
            Assert.That(resource.DisplayName, Is.EqualTo("Berry Patch"));
            Assert.That(resource.IconPath, Is.EqualTo("/berries.png"));
            Assert.That(resource.QuantityRaw, Is.EqualTo(ScaledAmount.FromWhole(500).RawValue));
            Assert.That(resource.Yields.Single().AssetId, Is.EqualTo(_berriesAssetId));
            Assert.That(resource.Yields.Single().AssetDisplayName, Is.EqualTo("Berries"));
            Assert.That(resource.Yields.Single().AmountPerUnit, Is.EqualTo(_ONE_PER_UNIT));
        }
    }

    [Test, Description("A type with two yields loads both, because a take credits every Asset its type gives rather than making the player pick one.")]
    public async Task ShouldReturnEveryLiveYieldOfAPatchAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        int typeId = await AddTypeAsync(context, "deer", "Deer Herd", [(_berriesAssetId, _ONE_PER_UNIT, true), (_hidesAssetId, 500, true)]);
        await AddDiscoveredInstanceAsync(context, typeId, quantityWhole: 100);

        TakeableMapResource resource = (await ReadTileAsync(context)).Single();

        Assert.That(resource.Yields.Select(yield => yield.AssetId), Is.EquivalentTo(new[] { _berriesAssetId, _hidesAssetId }));
    }

    [Test, Description("A tile's two resources come back in slot order, so the dialog lists them the way the tile itself is drawn rather than however the join returned them.")]
    public async Task ShouldReturnATilesResourcesInSlotOrderAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        int firstTypeId = await AddTypeAsync(context, "berries", "Berry Patch", [(_berriesAssetId, _ONE_PER_UNIT, true)]);
        int secondTypeId = await AddTypeAsync(context, "deer", "Deer Herd", [(_hidesAssetId, _ONE_PER_UNIT, true)]);

        // Added out of order, so an accidental insertion-order result would fail rather than pass by luck.
        await AddDiscoveredInstanceAsync(context, secondTypeId, quantityWhole: 100, slot: MapResourceInstance.MAX_SLOT);
        await AddDiscoveredInstanceAsync(context, firstTypeId, quantityWhole: 100, slot: MapResourceInstance.MIN_SLOT);

        IReadOnlyCollection<TakeableMapResource> onTile = await ReadTileAsync(context);

        Assert.That(
            onTile.Select(resource => resource.Slot),
            Is.EqualTo(new[] { MapResourceInstance.MIN_SLOT, MapResourceInstance.MAX_SLOT }).AsCollection);
    }

    [Test, Description("Several tiles are read in one pass and keyed by tile, which is what lets the context builder keep its query count fixed whatever the selection size.")]
    public async Task ShouldGroupResourcesByTheTileTheyStandOnAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        int typeId = await AddTypeAsync(context, "berries", "Berry Patch", [(_berriesAssetId, _ONE_PER_UNIT, true)]);

        await AddDiscoveredInstanceAsync(context, typeId, quantityWhole: 100);
        await AddDiscoveredInstanceAsync(context, typeId, quantityWhole: 100, tileId: _OTHER_TILE_ID);

        IReadOnlyDictionary<int, IReadOnlyCollection<TakeableMapResource>> byTile =
            await _reader.GetForTilesAsync(context, _GAME_ID, _PLAYER_ID, [_TILE_ID, _OTHER_TILE_ID], CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(byTile[_TILE_ID], Has.Exactly(1).Items);
            Assert.That(byTile[_OTHER_TILE_ID], Has.Exactly(1).Items);
        }
    }

    [Test, Description("A tile with nothing takeable is absent rather than present-and-empty, since the strategy reads a missing key as nothing to offer.")]
    public async Task ShouldLeaveOutATileHoldingNothingAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        Assert.That(await _reader.GetForTilesAsync(context, _GAME_ID, _PLAYER_ID, [_TILE_ID], CancellationToken.None), Is.Empty);
    }

    [Test, Description("Asked about no tiles at all, the reader must not query, because the context builder hands it whatever the selection occupies — including nothing.")]
    public async Task ShouldReturnNothingForAnEmptyTileListAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        Assert.That(await _reader.GetForTilesAsync(context, _GAME_ID, _PLAYER_ID, [], CancellationToken.None), Is.Empty);
    }

    #endregion

    #region Quantity

    [Test, Description("A patch worked down to nothing is not takeable, since taking from it would spend a Force's whole turn for no cargo.")]
    public async Task ShouldSkipAPatchWithNothingLeftAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        int typeId = await AddTypeAsync(context, "berries", "Berry Patch", [(_berriesAssetId, _ONE_PER_UNIT, true)]);
        await AddDiscoveredInstanceAsync(context, typeId, quantityWhole: 0);

        Assert.That(await ReadTileAsync(context), Is.Empty);
    }

    [Test, Description("A Distribution and a stratified seam keep their amounts in child rows, so neither has a quantity to take against and neither is takeable yet.")]
    [TestCase(true, false, Description = "A Timber Distribution, which holds a slot on every forest tile.")]
    [TestCase(false, true, Description = "A stratified mineral seam.")]
    public async Task ShouldSkipAnInstanceWithNoQuantityOfItsOwnAsync(bool isDistribution, bool isStratified)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        int typeId = await AddTypeAsync(
            context,
            "timber",
            "Timber",
            [(_berriesAssetId, _ONE_PER_UNIT, true)],
            isDistribution: isDistribution,
            isStratified: isStratified);

        await AddDiscoveredInstanceAsync(context, typeId, quantityWhole: null);

        Assert.That(await ReadTileAsync(context), Is.Empty);
    }

    #endregion

    #region Discovery and research

    [Test, Description("A patch this Player has not found is not offered, or the toolbar would reveal a resource the map does not draw for them.")]
    public async Task ShouldSkipAnUndiscoveredPatchAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        int typeId = await AddTypeAsync(context, "berries", "Berry Patch", [(_berriesAssetId, _ONE_PER_UNIT, true)]);
        await AddInstanceAsync(context, typeId, quantityWhole: 500);

        Assert.That(await ReadTileAsync(context), Is.Empty);
    }

    [Test, Description("Discovery is per Player, so a rival's find does not hand this one the patch.")]
    public async Task ShouldSkipAPatchOnlyAnotherPlayerHasFoundAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        int typeId = await AddTypeAsync(context, "berries", "Berry Patch", [(_berriesAssetId, _ONE_PER_UNIT, true)]);
        int instanceId = await AddInstanceAsync(context, typeId, quantityWhole: 500);

        await AddDiscoveryAsync(context, instanceId, _OTHER_PLAYER_ID);

        Assert.That(await ReadTileAsync(context), Is.Empty);
    }

    [Test, Description("A type that must be researched before it can be worked is withheld until this Player has researched it, which is the gate the catalogue flag exists for.")]
    public async Task ShouldSkipAResearchGatedTypeThisPlayerHasNotResearchedAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        int typeId = await AddTypeAsync(
            context,
            "copper",
            "Copper Outcrop",
            [(_berriesAssetId, _ONE_PER_UNIT, true)],
            requiresResearchToExtract: true);

        await AddDiscoveredInstanceAsync(context, typeId, quantityWhole: 500);

        Assert.That(await ReadTileAsync(context), Is.Empty);
    }

    [Test, Description("Seeing a type is not researching it, so a knowledge row with no researched round still withholds the patch.")]
    public async Task ShouldSkipAResearchGatedTypeKnownButNotResearchedAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        int typeId = await AddTypeAsync(
            context,
            "copper",
            "Copper Outcrop",
            [(_berriesAssetId, _ONE_PER_UNIT, true)],
            requiresResearchToExtract: true);

        await AddDiscoveredInstanceAsync(context, typeId, quantityWhole: 500);
        await AddTypeKnowledgeAsync(context, typeId, researchedOnRound: null);

        Assert.That(await ReadTileAsync(context), Is.Empty);
    }

    [Test, Description("Once researched the patch is takeable, so the gate opens rather than withholding the type forever.")]
    public async Task ShouldReturnAResearchGatedTypeOnceResearchedAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        int typeId = await AddTypeAsync(
            context,
            "copper",
            "Copper Outcrop",
            [(_berriesAssetId, _ONE_PER_UNIT, true)],
            requiresResearchToExtract: true);

        await AddDiscoveredInstanceAsync(context, typeId, quantityWhole: 500);
        await AddTypeKnowledgeAsync(context, typeId, researchedOnRound: 4);

        Assert.That(await ReadTileAsync(context), Has.Exactly(1).Items);
    }

    #endregion

    #region Yields and switches

    [Test, Description("A yield off by default is left out, since the Player has chosen not to take that Asset and a take must not force it on them.")]
    public async Task ShouldDropAYieldThatIsOffByDefaultAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        int typeId = await AddTypeAsync(context, "deer", "Deer Herd", [(_berriesAssetId, _ONE_PER_UNIT, true), (_hidesAssetId, _ONE_PER_UNIT, false)]);
        await AddDiscoveredInstanceAsync(context, typeId, quantityWhole: 100);

        TakeableMapResource resource = (await ReadTileAsync(context)).Single();

        Assert.That(resource.Yields.Single().AssetId, Is.EqualTo(_berriesAssetId));
    }

    [Test, Description("A Player's own switch beats the catalogue default both ways, which is what makes the yield switches worth consulting at all.")]
    public async Task ShouldHonourThePlayersOwnYieldSwitchAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        int typeId = await AddTypeAsync(context, "deer", "Deer Herd", [(_berriesAssetId, _ONE_PER_UNIT, true), (_hidesAssetId, _ONE_PER_UNIT, false)]);
        await AddDiscoveredInstanceAsync(context, typeId, quantityWhole: 100);

        await AddYieldSwitchAsync(context, typeId, _berriesAssetId, isEnabled: false);
        await AddYieldSwitchAsync(context, typeId, _hidesAssetId, isEnabled: true);

        TakeableMapResource resource = (await ReadTileAsync(context)).Single();

        Assert.That(resource.Yields.Single().AssetId, Is.EqualTo(_hidesAssetId), "The switched-on yield replaces the switched-off one.");
    }

    [Test, Description("With every yield switched off the whole patch is withheld, because taking it would strip the tile and hand the Force nothing.")]
    public async Task ShouldSkipAPatchWhoseEveryYieldIsOffAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        int typeId = await AddTypeAsync(context, "berries", "Berry Patch", [(_berriesAssetId, _ONE_PER_UNIT, true)]);
        await AddDiscoveredInstanceAsync(context, typeId, quantityWhole: 500);

        await AddYieldSwitchAsync(context, typeId, _berriesAssetId, isEnabled: false);

        Assert.That(await ReadTileAsync(context), Is.Empty);
    }

    [Test, Description("A yield rated at nothing gives nothing, so it is dropped rather than staging a balance row of zero.")]
    public async Task ShouldDropAYieldRatedAtNothingAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        int typeId = await AddTypeAsync(context, "deer", "Deer Herd", [(_berriesAssetId, _ONE_PER_UNIT, true), (_hidesAssetId, 0, true)]);
        await AddDiscoveredInstanceAsync(context, typeId, quantityWhole: 100);

        TakeableMapResource resource = (await ReadTileAsync(context)).Single();

        Assert.That(resource.Yields.Single().AssetId, Is.EqualTo(_berriesAssetId));
    }

    [Test, Description("An Asset held per Player keeps its number outside the banks entirely, so crediting one into a hold would write a second source of truth.")]
    public async Task ShouldDropAYieldOfAnAssetThatCannotBeBankedAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        int experienceAssetId = await AddAssetAsync(context, "Knowledge.Experience.Scouting", "Scouting Experience", AssetStorageScope.Player);

        int typeId = await AddTypeAsync(context, "berries", "Berry Patch", [(experienceAssetId, _ONE_PER_UNIT, true)]);
        await AddDiscoveredInstanceAsync(context, typeId, quantityWhole: 500);

        Assert.That(await ReadTileAsync(context), Is.Empty, "The patch's only yield cannot be carried, so there is nothing to take.");
    }

    [Test, Description("A type with no yields at all is withheld, since there is no Asset a take could credit.")]
    public async Task ShouldSkipATypeThatYieldsNothingAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        int typeId = await AddTypeAsync(context, "scenery", "Standing Stones", []);
        await AddDiscoveredInstanceAsync(context, typeId, quantityWhole: 500);

        Assert.That(await ReadTileAsync(context), Is.Empty);
    }

    #endregion

    #region Fixtures

    private async Task<IReadOnlyCollection<TakeableMapResource>> ReadTileAsync(AscendantEmpireContext context)
    {
        IReadOnlyDictionary<int, IReadOnlyCollection<TakeableMapResource>> byTile =
            await _reader.GetForTilesAsync(context, _GAME_ID, _PLAYER_ID, [_TILE_ID], CancellationToken.None);

        return byTile.TryGetValue(_TILE_ID, out IReadOnlyCollection<TakeableMapResource>? onTile) ? onTile : [];
    }

    /// <summary>An Asset in the catalogue, bankable unless told otherwise.</summary>
    private static async Task<int> AddAssetAsync(
        AscendantEmpireContext context,
        string code,
        string displayName,
        AssetStorageScope storageScope = AssetStorageScope.SettlementBank)
    {
        var storageClass = new StorageClass { Code = $"{code}-store", DisplayName = "Store" };
        context.StorageClasses.Add(storageClass);

        var metaCategory = new AssetMetaCategory { Code = $"{code}-meta", DisplayName = "Meta", SortOrder = 1, IconPath = "/icon.png" };
        context.AssetMetaCategories.Add(metaCategory);

        await context.SaveChangesAsync();

        var asset = new Asset
        {
            Code = code,
            DisplayName = displayName,
            IconPath = "/icon.png",
            IsGradedAsset = false,
            SortOrder = 1,
            AssetMetaCategoryId = metaCategory.Id,
            StorageClassId = storageClass.Id,
            StorageScope = storageScope
        };

        context.Assets.Add(asset);
        await context.SaveChangesAsync();

        return asset.Id;
    }

    /// <summary>A MapResourceType and its yields, each given as the Asset, its rate and whether it is on by default.</summary>
    private static async Task<int> AddTypeAsync(
        AscendantEmpireContext context,
        string code,
        string displayName,
        IReadOnlyCollection<(int AssetId, long AmountPerUnit, bool IsEnabledByDefault)> yields,
        string? iconPath = null,
        bool isDistribution = false,
        bool isStratified = false,
        bool requiresResearchToExtract = false)
    {
        var category = new MapResourceCategory { Code = $"{code}-category", DisplayName = "Category", SortOrder = 1 };
        context.MapResourceCategories.Add(category);

        await context.SaveChangesAsync();

        var type = new MapResourceType
        {
            Code = code,
            DisplayName = displayName,
            MapResourceCategoryId = category.Id,
            SortOrder = 1,
            IconPath = iconPath,
            IsDistribution = isDistribution,
            IsStratified = isStratified,
            RequiresResearchToExtract = requiresResearchToExtract,
            ExtractionMethod = ExtractionMethod.Gather,
            DiscoveryLikelihood = 1m
        };

        context.MapResourceTypes.Add(type);
        await context.SaveChangesAsync();

        context.MapResourceTypeYields.AddRange(yields.Select(yield => new MapResourceTypeYield
        {
            MapResourceTypeId = type.Id,
            AssetId = yield.AssetId,
            AmountPerUnit = yield.AmountPerUnit,
            IsEnabledByDefault = yield.IsEnabledByDefault
        }));

        await context.SaveChangesAsync();

        return type.Id;
    }

    /// <summary>An instance on the tile, undiscovered.</summary>
    private static async Task<int> AddInstanceAsync(
        AscendantEmpireContext context,
        int mapResourceTypeId,
        int? quantityWhole,
        byte slot = MapResourceInstance.MIN_SLOT,
        int tileId = _TILE_ID)
    {
        var instance = new MapResourceInstance
        {
            GameId = _GAME_ID,
            CampaignMapId = tileId,
            MapResourceTypeId = mapResourceTypeId,
            Slot = slot,
            Quantity = quantityWhole is null ? null : ScaledAmount.FromWhole(quantityWhole.Value).RawValue,
            CreatedOnRound = 1
        };

        context.MapResourceInstances.Add(instance);
        await context.SaveChangesAsync();

        return instance.Id;
    }

    /// <summary>An instance on the tile, discovered by the reading Player — the state every takeable patch is in.</summary>
    private static async Task<int> AddDiscoveredInstanceAsync(
        AscendantEmpireContext context,
        int mapResourceTypeId,
        int? quantityWhole,
        byte slot = MapResourceInstance.MIN_SLOT,
        int tileId = _TILE_ID)
    {
        int instanceId = await AddInstanceAsync(context, mapResourceTypeId, quantityWhole, slot, tileId);

        await AddDiscoveryAsync(context, instanceId, _PLAYER_ID);

        return instanceId;
    }

    private static async Task AddDiscoveryAsync(AscendantEmpireContext context, int mapResourceInstanceId, int playerId)
    {
        context.PlayerKnownMapResources.Add(new PlayerKnownMapResource
        {
            GameId = _GAME_ID,
            PlayerId = playerId,
            MapResourceInstanceId = mapResourceInstanceId,
            DiscoveredOnRound = 1
        });

        await context.SaveChangesAsync();
    }

    private static async Task AddTypeKnowledgeAsync(AscendantEmpireContext context, int mapResourceTypeId, int? researchedOnRound)
    {
        context.PlayerMapResourceTypeKnowledge.Add(new PlayerMapResourceTypeKnowledge
        {
            GameId = _GAME_ID,
            PlayerId = _PLAYER_ID,
            MapResourceTypeId = mapResourceTypeId,
            FirstSeenOnRound = 1,
            ResearchedOnRound = researchedOnRound
        });

        await context.SaveChangesAsync();
    }

    private static async Task AddYieldSwitchAsync(AscendantEmpireContext context, int mapResourceTypeId, int assetId, bool isEnabled)
    {
        context.PlayerMapResourceSwitches.Add(new PlayerMapResourceSwitch
        {
            GameId = _GAME_ID,
            PlayerId = _PLAYER_ID,
            SourceType = ModifierSourceType.Technology,
            SourceId = 10,
            Kind = MapResourceSwitchKind.YieldAsset,
            MapResourceTypeId = mapResourceTypeId,
            AssetId = assetId,
            IsEnabled = isEnabled
        });

        await context.SaveChangesAsync();
    }

    #endregion
}
