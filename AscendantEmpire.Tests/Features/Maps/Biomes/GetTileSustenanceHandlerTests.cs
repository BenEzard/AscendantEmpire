using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Maps.Biomes.Handlers;
using AscendantEmpire.Application.Features.Maps.Biomes.Services;
using AscendantEmpire.Application.Features.Maps.CampaignMaps.Classes;
using AscendantEmpire.Application.Features.Maps.CampaignMaps.Services;
using AscendantEmpire.Application.Features.Maps.Depletion.Services;
using AscendantEmpire.Application.Helpers.ScaledNumerics;
using AscendantEmpire.BackendContracts.Biomes;
using AscendantEmpire.BackendContracts.CampaignMaps;
using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Entities.Maps.CampaignMaps;
using AscendantEmpire.Domain.Entities.Maps.MapResources;
using AscendantEmpire.Domain.Entities.Units;
using AscendantEmpire.Domain.Enums;
using AscendantEmpire.Domain.Enums.MapResources;
using AscendantEmpire.Domain.Enums.Maps;
using AscendantEmpire.Domain.Enums.Modifiers;
using AscendantEmpire.Tests.Features.Maps.Shared;

using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Tests.Features.Maps.Biomes;

/// <summary>
/// Tests for <see cref="GetTileSustenanceHandler"/>: that the tile toolbar is told what is available to the Player on a tile they can see, and nothing about a tile they cannot.
/// </summary>
/// <remarks>
/// <para>
/// On the in-memory tier. The handler's own reads are LINQ only; sight and geometry are substituted, since the sight rules are covered by their own tests and all this handler
/// does with them is ask whether the tile is in view. The Player's values come from the real <see cref="PlayerSustenanceValueReader"/>, so a modifier here is applied exactly as
/// in play.
/// </para>
/// <para>
/// The map is 10 x 10. The catalogue has one biome, Grassland (Forage 400, Hunt 100, no Fish, Fodder 1,000). By default the Player has one Force at (3, 4) seeing only its own
/// Grassland tile.
/// </para>
/// <para>
/// The support level comes from the real <see cref="TileSupportEvaluator"/>. Which Forces count, and the thresholds, are covered in <c>TileSupportEvaluatorTests</c> and
/// <c>TileSupportForecasterTests</c>; here it is only checked that the level reaches the response.
/// </para>
/// </remarks>
[TestFixture]
internal class GetTileSustenanceHandlerTests : InMemoryTestBase
{
    private const int _GAME_ID = 1;
    private const int _PLAYER_ID = 2;
    private const int _SEEN_X = 3;
    private const int _SEEN_Y = 4;

    private ISightDataReader _sightDataReader = null!;
    private IFieldOfViewService _fieldOfViewService = null!;
    private GetTileSustenanceHandler _handler = null!;

    /// <summary>
    /// The catalogue every Force here eats from: one ration per individual, so a Force's need is its individuals.
    /// </summary>
    private FoodDesignSeed.FoodDesign _design = null!;

    [SetUp]
    public async Task SetUpHandlerAsync()
    {
        await using (AscendantEmpireContext context = await DbFactory.CreateDbContextAsync())
        {
            context.CampaignBiomes.Add(new CampaignBiome
            {
                BiomeTypeId = BiomeType.Grassland,
                Name = "Grassland",
                MovementCost = 1m,
                IsTraversable = true,
                MapEditorHtmlColor = "#7cb342",
                BaseForageValue = ScaledAmount.FromWhole(400).RawValue,
                BaseHuntValue = ScaledAmount.FromWhole(100).RawValue,
                BaseFishValue = null,
                BaseFodderValue = ScaledAmount.FromWhole(1_000).RawValue
            });

            await context.SaveChangesAsync();

            _design = await FoodDesignSeed.SeedAsync(context);
        }

        _sightDataReader = Substitute.For<ISightDataReader>();
        _sightDataReader
            .GetGeometryAsync(Arg.Any<AscendantEmpireContext>(), _GAME_ID, Arg.Any<CancellationToken>())
            .Returns(new CampaignMapGeometry(CampaignMapOffsetId: 1, Width: 10, Height: 10, WrapsHorizontally: false, WrapsVertically: false));

        _fieldOfViewService = Substitute.For<IFieldOfViewService>();
        SetSeenTiles(BuildSeenTile(_SEEN_X, _SEEN_Y, BiomeType.Grassland));

        _handler = new GetTileSustenanceHandler(
            NullLogger<GetTileSustenanceHandler>.Instance,
            DbFactory,
            _sightDataReader,
            _fieldOfViewService,
            new PlayerSustenanceValueReader(),
            new TileSupportEvaluator(
                new PlayerSustenanceValueReader(),
                new BiomeRegenerationValueReader(),
                new SustenanceAssetReader(),
                new SupplySetMembershipReader(),
                new TileFoodStockReader(),
                new CampaignRiverReader()),
            new CampaignRiverReader(),
            new TileFoodDisclosureReader(new SustenanceAssetReader(), new SupplySetMembershipReader()));
    }

    [Test, Description("A tile in live sight reports the Player's values for its biome, which with no modifiers and nothing taken are the biome's base values.")]
    public async Task ShouldReturnTheBiomeValuesWhenTheTileIsObservedAsync()
    {
        ServiceResult<TileSustenance> result = await HandleAsync(_SEEN_X, _SEEN_Y);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.True);
            Assert.That(result.SuccessValue!.IsObserved, Is.True);
            Assert.That(result.SuccessValue.Forage, Is.EqualTo(400m));
            Assert.That(result.SuccessValue.Hunt, Is.EqualTo(100m));
            Assert.That(result.SuccessValue.Fodder, Is.EqualTo(1_000m));
        }
    }

    [Test, Description("A resource the biome does not have comes back null rather than 0, so the toolbar can leave it out instead of showing a stripped tile.")]
    public async Task ShouldReturnNullForAResourceTheBiomeDoesNotHaveAsync()
    {
        ServiceResult<TileSustenance> result = await HandleAsync(_SEEN_X, _SEEN_Y);

        Assert.That(result.SuccessValue!.Fish, Is.Null);
    }

    [Test, Description("A riverbank reports the Fish its river puts there, with Forage, Hunt and Fodder all raised 20% by the edge.")]
    public async Task ShouldReportTheRiverBonusOnTheSeenTileAsync()
    {
        await AddRiverTileAsync(_SEEN_X, _SEEN_Y, RiverWidth.Medium);

        ServiceResult<TileSustenance> result = await HandleAsync(_SEEN_X, _SEEN_Y);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.SuccessValue!.Fish, Is.EqualTo(15m), "A Medium edge is worth 15 Fish on dry ground.");
            Assert.That(result.SuccessValue.Hunt, Is.EqualTo(120m));
            Assert.That(result.SuccessValue.Fodder, Is.EqualTo(1_200m));
            Assert.That(result.SuccessValue.Forage, Is.EqualTo(480m));
        }
    }

    [Test, Description("The values are the calling Player's own, so a modifier they hold raises what the toolbar shows them.")]
    public async Task ShouldApplyThePlayersModifiersAsync()
    {
        await using (AscendantEmpireContext context = await DbFactory.CreateDbContextAsync())
        {
            context.PlayerSustenanceModifiers.Add(new PlayerSustenanceModifier
            {
                GameId = _GAME_ID,
                PlayerId = _PLAYER_ID,
                SourceType = ModifierSourceType.Technology,
                SourceId = 10,
                Resource = SustenanceResource.Hunt,
                BiomeTypeId = BiomeType.Grassland,
                FlatValue = ScaledAmount.FromWhole(100).RawValue,
                PercentValue = 0
            });

            await context.SaveChangesAsync();
        }

        ServiceResult<TileSustenance> result = await HandleAsync(_SEEN_X, _SEEN_Y);

        Assert.That(result.SuccessValue!.Hunt, Is.EqualTo(200m));
    }

    [Test, Description("What has been taken from the tile is subtracted from each resource separately, so depletion of one resource never shows against another.")]
    public async Task ShouldSubtractEachResourcesOwnTakenAmountAsync()
    {
        await AddDepletionAsync(SeenTileId, forageTaken: ScaledAmount.FromWhole(150).RawValue, huntTaken: ScaledAmount.FromDecimal(25.5m).RawValue, fodderTaken: 0);

        ServiceResult<TileSustenance> result = await HandleAsync(_SEEN_X, _SEEN_Y);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.SuccessValue!.Forage, Is.EqualTo(250m));
            Assert.That(result.SuccessValue.Hunt, Is.EqualTo(74.5m));
            Assert.That(result.SuccessValue.Fish, Is.Null, "A resource the biome does not have stays absent, even on a depleted tile.");
            Assert.That(result.SuccessValue.Fodder, Is.EqualTo(1_000m));
        }
    }

    [Test, Description("A tile stripped past the Player's own value by a stronger Player shows 0 rather than a negative amount.")]
    public async Task ShouldShowZeroWhenMoreHasBeenTakenThanThePlayersValueAsync()
    {
        await AddDepletionAsync(SeenTileId, forageTaken: 0, huntTaken: ScaledAmount.FromWhole(180).RawValue, fodderTaken: 0);

        ServiceResult<TileSustenance> result = await HandleAsync(_SEEN_X, _SEEN_Y);

        Assert.That(result.SuccessValue!.Hunt, Is.Zero);
    }

    [Test, Description("Depletion is read for the tile asked about only, so another tile having been fed on changes nothing here.")]
    public async Task ShouldIgnoreDepletionOnAnotherTileAsync()
    {
        await AddDepletionAsync(SeenTileId + 1, forageTaken: ScaledAmount.FromWhole(400).RawValue, huntTaken: ScaledAmount.FromWhole(100).RawValue, fodderTaken: ScaledAmount.FromWhole(1_000).RawValue);

        ServiceResult<TileSustenance> result = await HandleAsync(_SEEN_X, _SEEN_Y);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.SuccessValue!.Forage, Is.EqualTo(400m));
            Assert.That(result.SuccessValue.Hunt, Is.EqualTo(100m));
            Assert.That(result.SuccessValue.Fodder, Is.EqualTo(1_000m));
        }
    }

    [Test, Description("A tile outside live sight discloses nothing, because what is left on a tile changes as anyone feeds and would leak where other Forces have been.")]
    public async Task ShouldDiscloseNothingWhenTheTileIsNotObservedAsync()
    {
        ServiceResult<TileSustenance> result = await HandleAsync(_SEEN_X + 1, _SEEN_Y);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.True, "An unwatched tile is an ordinary answer, not an error.");
            Assert.That(result.SuccessValue!.IsObserved, Is.False);
            Assert.That(result.SuccessValue.Forage, Is.Null);
            Assert.That(result.SuccessValue.Hunt, Is.Null);
            Assert.That(result.SuccessValue.Fish, Is.Null);
            Assert.That(result.SuccessValue.Fodder, Is.Null);
        }
    }

    [Test, Description("The toolbar is told whether the tile can feed the Player's Forces on it: 300 troops against 500 Forage and Hunt is one Turn.")]
    public async Task ShouldReturnTheSupportLevelForTheForcesOnTheTileAsync()
    {
        await AddForceAsync(troops: 300);

        ServiceResult<TileSustenance> result = await HandleAsync(_SEEN_X, _SEEN_Y);

        Assert.That(result.SuccessValue!.SupportLevel, Is.EqualTo(TileSupportLevel.OneTurn));
    }

    [Test, Description("A tile with food and no Forces on it has no support level, so the toolbar is not coloured.")]
    public async Task ShouldReturnNoSupportLevelWhenNoForcesAreOnTheTileAsync()
    {
        ServiceResult<TileSustenance> result = await HandleAsync(_SEEN_X, _SEEN_Y);

        Assert.That(result.SuccessValue!.SupportLevel, Is.Null);
    }

    [Test, Description("A stripped tile is exhausted even with no Forces on it, so the toolbar shows a bare tile as bare.")]
    public async Task ShouldReturnExhaustedForAStrippedTileWithNoForcesAsync()
    {
        await AddDepletionAsync(SeenTileId, forageTaken: ScaledAmount.FromWhole(400).RawValue, huntTaken: ScaledAmount.FromWhole(100).RawValue, fodderTaken: 0);

        ServiceResult<TileSustenance> result = await HandleAsync(_SEEN_X, _SEEN_Y);

        Assert.That(result.SuccessValue!.SupportLevel, Is.EqualTo(TileSupportLevel.Exhausted));
    }

    [Test, Description("A tile outside live sight has no support level either, since a level would disclose what is left on it.")]
    public async Task ShouldReturnNoSupportLevelWhenTheTileIsNotObservedAsync()
    {
        await AddDepletionAsync(SeenTileId + 1, forageTaken: ScaledAmount.FromWhole(400).RawValue, huntTaken: ScaledAmount.FromWhole(100).RawValue, fodderTaken: 0);

        ServiceResult<TileSustenance> result = await HandleAsync(_SEEN_X + 1, _SEEN_Y);

        Assert.That(result.SuccessValue!.SupportLevel, Is.Null);
    }

    #region Food total

    [Test, Description("The total is what the tile feeds for a Turn, so a figure the Player reads answers how long they may stay.")]
    public async Task ShouldReturnTotalInNeedUnitsAsync()
    {
        await AddForceAsync(troops: 10);

        ServiceResult<TileSustenance> result = await HandleAsync(_SEEN_X, _SEEN_Y);

        // 400 Forage and 100 Hunt, each costing one ration apiece.
        Assert.That(result.SuccessValue!.IndividualsFed, Is.EqualTo(500m));
    }

    [Test, Description("The breakdown names each source in the same rations as the total, so the parts the Player hovers add up to the figure they see.")]
    public async Task ShouldBreakTheTotalDownBySourceAsync()
    {
        await AddForceAsync(troops: 10);

        ServiceResult<TileSustenance> result = await HandleAsync(_SEEN_X, _SEEN_Y);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.SuccessValue!.FoodSources.Select(source => source.Label), Is.EqualTo(new[] { "Forage", "Hunt" }));
            Assert.That(result.SuccessValue.FoodSources.Sum(source => source.IndividualsFed), Is.EqualTo(500m));
        }
    }

    [Test, Description("A tile fed only by its biome reports an exact total, the Player's own values being no estimate.")]
    public async Task ShouldNotMarkTotalEstimatedWhenOnlyBiomeFoodAsync()
    {
        await AddForceAsync(troops: 10);

        ServiceResult<TileSustenance> result = await HandleAsync(_SEEN_X, _SEEN_Y);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.SuccessValue!.IsTotalEstimated, Is.False);
            Assert.That(result.SuccessValue.FoodSources.Any(source => source.IsEstimated), Is.False);
        }
    }

    [Test, Description("A patch with a known estimate makes the total an estimate, since part of it is fuzzed.")]
    public async Task ShouldMarkTotalEstimatedWhenAMapResourceContributesAsync()
    {
        await AddForceAsync(troops: 10);
        await AddDiscoveredPatchAsync(quantity: 600, fuzzyFactor: 0m);

        ServiceResult<TileSustenance> result = await HandleAsync(_SEEN_X, _SEEN_Y);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.SuccessValue!.IsTotalEstimated, Is.True);

            // A factor of 0 fuzzes nothing, so the 600 berries are the 600 the patch really holds.
            Assert.That(result.SuccessValue.IndividualsFed, Is.EqualTo(1_100m));
            Assert.That(result.SuccessValue.FoodSources.Last().Label, Is.EqualTo("Berries"));
            Assert.That(result.SuccessValue.FoodSources.Last().IsEstimated, Is.True);
        }
    }

    [Test, Description("A patch whose size the Player cannot make out is left out of the total, not counted as nothing.")]
    public async Task ShouldExcludeMapResourceWithNoEstimateAsync()
    {
        await AddForceAsync(troops: 10);
        await AddDiscoveredPatchAsync(quantity: 600, fuzzyFactor: null);

        ServiceResult<TileSustenance> result = await HandleAsync(_SEEN_X, _SEEN_Y);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.SuccessValue!.IndividualsFed, Is.EqualTo(500m));
            Assert.That(result.SuccessValue.IsTotalEstimated, Is.False);
        }
    }

    [Test, Description("A patch the Player has not discovered is no part of the total, which would otherwise tell them food is there before they find it.")]
    public async Task ShouldExcludeAnUndiscoveredMapResourceAsync()
    {
        await AddForceAsync(troops: 10);
        await AddDiscoveredPatchAsync(quantity: 600, fuzzyFactor: 0m, isDiscovered: false);

        ServiceResult<TileSustenance> result = await HandleAsync(_SEEN_X, _SEEN_Y);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.SuccessValue!.IndividualsFed, Is.EqualTo(500m));
            Assert.That(result.SuccessValue.IsTotalEstimated, Is.False);
        }
    }

    [Test, Description("With no Force of the Player's left to feed there is no diet to price the tile against, so no total is given rather than a made-up one.")]
    public async Task ShouldReturnNoTotalWhenThePlayerHasNothingThatEatsAsync()
    {
        ServiceResult<TileSustenance> result = await HandleAsync(_SEEN_X, _SEEN_Y);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.SuccessValue!.IndividualsFed, Is.Null);
            Assert.That(result.SuccessValue.FoodSources, Is.Empty);
        }
    }

    [Test, Description("A tile outside live sight gives no total either, since a figure would disclose what is left on it.")]
    public async Task ShouldReturnNoTotalWhenTheTileIsNotObservedAsync()
    {
        await AddForceAsync(troops: 10);

        ServiceResult<TileSustenance> result = await HandleAsync(_SEEN_X + 1, _SEEN_Y);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.SuccessValue!.IndividualsFed, Is.Null);
            Assert.That(result.SuccessValue.FoodSources, Is.Empty);
        }
    }

    #endregion

    [Test, Description("A coordinate past the edge of the map names no tile and is a 404, rather than being wrapped onto a tile that is on the map.")]
    public async Task ShouldReturnNotFoundWhenTheTileIsOffTheMapAsync()
    {
        ServiceResult<TileSustenance> result = await HandleAsync(10, _SEEN_Y);

        Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.NotFound));
    }

    [Test, Description("A Game with no map geometry does not exist, so the read is a 404 rather than an empty answer.")]
    public async Task ShouldReturnNotFoundWhenTheGameDoesNotExistAsync()
    {
        _sightDataReader
            .GetGeometryAsync(Arg.Any<AscendantEmpireContext>(), _GAME_ID, Arg.Any<CancellationToken>())
            .Returns((CampaignMapGeometry?)null);

        ServiceResult<TileSustenance> result = await HandleAsync(_SEEN_X, _SEEN_Y);

        Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.NotFound));
    }

    /// <summary>
    /// The seen tile's id, as <see cref="BuildSeenTile"/> numbers tiles on the 10 x 10 map.
    /// </summary>
    private static int SeenTileId => 1 + (_SEEN_Y * 10) + _SEEN_X;

    /// <summary>
    /// Records raw scaled amounts as already taken from a tile.
    /// </summary>
    private async Task AddDepletionAsync(int tileId, long forageTaken, long huntTaken, long fodderTaken)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        context.CampaignMapDepletions.Add(new CampaignMapDepletion
        {
            CampaignMapId = tileId,
            GameId = _GAME_ID,
            ForageTaken = forageTaken,
            HuntTaken = huntTaken,
            FodderTaken = fodderTaken,
            FedOnRound = 1
        });

        await context.SaveChangesAsync();
    }

    /// <summary>
    /// Adds a healthy Force of the Player's on the seen tile.
    /// </summary>
    private async Task AddForceAsync(int troops)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        context.UnitInstances.Add(new UnitInstance
        {
            Id = 1,
            GameId = _GAME_ID,
            PlayerId = _PLAYER_ID,
            CampaignMapTileId = SeenTileId,
            UnitTemplateId = _design.UnitTemplateId,
            InitialStrength = troops,
            HealthyCount = troops,
            InjuredCount = 0
        });

        await context.SaveChangesAsync();
    }

    /// <summary>
    /// Puts a patch of berries on the seen tile, yielding one Berries Asset per unit of its quantity.
    /// </summary>
    /// <param name="fuzzyFactor">How wide the Player's estimate of it may be. A factor of 0 is an estimate that happens to be exact; null is no estimate at all.</param>
    /// <param name="isDiscovered">Whether the Player has found it. An undiscovered patch is on the tile but not on their map.</param>
    private async Task AddDiscoveredPatchAsync(long quantity, decimal? fuzzyFactor, bool isDiscovered = true)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        var category = new MapResourceCategory { Code = "food", DisplayName = "Food" };
        context.MapResourceCategories.Add(category);

        await context.SaveChangesAsync();

        var type = new MapResourceType
        {
            Code = "berries",
            DisplayName = "Berries",
            MapResourceCategoryId = category.Id,
            ExtractionMethod = ExtractionMethod.Gather,
            DiscoveryLikelihood = 1.0m
        };

        context.MapResourceTypes.Add(type);

        await context.SaveChangesAsync();

        context.MapResourceTypeYields.Add(new MapResourceTypeYield
        {
            MapResourceTypeId = type.Id,
            AssetId = _design.AssetIdsByCode[FoodDesignSeed.BERRIES_ASSET_CODE],
            AmountPerUnit = ScaledAmount.FromWhole(1).RawValue,
            IsEnabledByDefault = true
        });

        var instance = new MapResourceInstance
        {
            GameId = _GAME_ID,
            CampaignMapId = SeenTileId,
            MapResourceTypeId = type.Id,
            Slot = MapResourceInstance.MIN_SLOT,
            Quantity = ScaledAmount.FromWhole(quantity).RawValue,
            EstimationFuzzyFactor = fuzzyFactor,
            CreatedOnRound = 1
        };

        context.MapResourceInstances.Add(instance);

        await context.SaveChangesAsync();

        if (isDiscovered)
        {
            context.PlayerKnownMapResources.Add(new PlayerKnownMapResource
            {
                GameId = _GAME_ID,
                PlayerId = _PLAYER_ID,
                MapResourceInstanceId = instance.Id,
                DiscoveredOnRound = 1
            });

            await context.SaveChangesAsync();
        }
    }

    /// <summary>
    /// Seeds the world-map row for a tile with a river of the given width on its own East edge.
    /// </summary>
    /// <remarks>
    /// The other tests seed no map rows at all - sight is substituted - so the river reader finds nothing and every tile reads as riverless, which is what they expect.
    /// </remarks>
    private async Task AddRiverTileAsync(int x, int y, RiverWidth width)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        context.CampaignMaps.Add(new CampaignMap
        {
            CampaignMapId = 1 + (y * 10) + x,
            GameId = _GAME_ID,
            XCoord = x,
            YCoord = y,
            BiomeTypeId = BiomeType.Grassland,
            CampaignMapGeographyTypeId = CampaignMapGeographyType.Flat,
            EastRiver = width,
            Biome = null!,
            Geography = null!,
            Game = null!,
            FirstVisitor = null,
            LastVisitor = null,
            OwnedBy = null
        });

        await context.SaveChangesAsync();
    }

    private async Task<ServiceResult<TileSustenance>> HandleAsync(int x, int y)
    {
        return await _handler.Handle(new GetTileSustenanceRequest(_GAME_ID, _PLAYER_ID, x, y), CancellationToken.None);
    }

    /// <summary>
    /// Has the field-of-view service report one Force of the Player's seeing exactly the given tiles.
    /// </summary>
    private void SetSeenTiles(params SightTerrain[] seenTiles)
    {
        var observer = new SightObserver
        {
            Id = 1,
            RecordType = RecordType.Unit,
            PlayerId = _PLAYER_ID,
            Location = new Coord(_SEEN_X, _SEEN_Y),
            BaseSightRange = 0,
            Detection = 0
        };

        IReadOnlyCollection<ObserverFieldOfView> fieldsOfView = [new ObserverFieldOfView(observer, seenTiles)];

        _fieldOfViewService
            .GetPlayerFieldOfViewAsync(_GAME_ID, _PLAYER_ID, Arg.Any<CampaignMapGeometry>(), Arg.Any<CancellationToken>())
            .Returns(ServiceResult<IReadOnlyCollection<ObserverFieldOfView>>.Succeed(fieldsOfView));
    }

    private static SightTerrain BuildSeenTile(int x, int y, BiomeType biome)
    {
        return new SightTerrain
        {
            CampaignMapId = 1 + (y * 10) + x,
            X = x,
            Y = y,
            BiomeType = biome,
            GeographyType = CampaignMapGeographyType.Flat,
            DoesBlockSight = false,
            RelativeElevation = 0,
            GeographyName = "Flat",
            GeographyMovementCost = 1m,
            BiomeName = biome.ToString(),
            BiomeMovementCost = 1m,
            BiomeIsWater = false,
            GeographyIsWater = false,
            BiomeIsTraversable = true,
            GeographyIsTraversable = true
        };
    }
}
