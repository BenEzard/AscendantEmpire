using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.ForceOrders.ContextActions;
using AscendantEmpire.Application.Features.ForceOrders.ContextActions.Strategies;
using AscendantEmpire.Application.Features.ForceOrders.Handlers;
using AscendantEmpire.Application.Features.Maps.MapResources.Services;
using AscendantEmpire.Application.Helpers.ScaledNumerics;
using AscendantEmpire.BackendContracts.ForceOrders.TakeResources;
using AscendantEmpire.Domain.Entities.Assets;
using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Entities.Maps.CampaignMaps;
using AscendantEmpire.Domain.Entities.Maps.MapResources;
using AscendantEmpire.Domain.Entities.UnitDesigns;
using AscendantEmpire.Domain.Entities.Units;
using AscendantEmpire.Domain.Enums.MapResources;
using AscendantEmpire.Domain.Enums.Maps;
using AscendantEmpire.Domain.Enums.UnitDesigns;

using Microsoft.Extensions.Logging.Abstractions;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Tests.Features.ForceOrders.Handlers;

/// <summary>
/// Tests for <see cref="GetTakeResourcesOptionsHandler"/> — what the take-resources dialog opens with.
/// </summary>
/// <remarks>
/// <para>
/// On the in-memory tier. The handler reads through <see cref="ForceActionContextBuilder"/>, which projects only scalar columns off
/// <see cref="CompiledUnitTemplate"/> and never its <c>Attack</c>/<c>Resistance</c> complex types, and the handler itself writes nothing and opens no
/// transaction — so per <c>docs/testing.md</c> this is the lightest tier that suffices.
/// </para>
/// <para>
/// Every Force here is built from one template on one tile, and every patch yields 1.0 of its Asset per unit, so a promised gain reads as the units
/// themselves. The units come off <see cref="TakeResourcesRules.UNITS_PER_HEALTHY_INDIVIDUAL"/>, which is tuning, so the figures are derived from it
/// rather than written out.
/// </para>
/// </remarks>
[TestFixture]
internal class GetTakeResourcesOptionsHandlerTests : InMemoryTestBase
{
    private const int _GAME_ID = 1;
    private const int _PLAYER_ID = 1;
    private const int _OTHER_PLAYER_ID = 2;
    private const int _TILE_ID = 112;
    private const int _OTHER_TILE_ID = 113;
    private const int _TEMPLATE_ID = 1;
    private const int _TAG_ID = 9;
    private const long _CAMPAIGN_MOVEMENT_POINTS = 1_000;

    /// <summary>1.0 of the Asset per unit of the MapResource, so the promised gain reads as the units taken.</summary>
    private const long _ONE_PER_UNIT = 1_000;


    private GetTakeResourcesOptionsHandler _handler = null!;

    private int _berriesAssetId;
    private int _hidesAssetId;

    [SetUp]
    public async Task SetUpAsync()
    {
        _handler = new GetTakeResourcesOptionsHandler(
            NullLogger<GetTakeResourcesOptionsHandler>.Instance,
            new ForceActionContextBuilder(DbFactory, new TakeableMapResourceReader(NullLogger<TakeableMapResourceReader>.Instance)));

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        context.Tags.Add(new Tag
        {
            Id = _TAG_ID,
            Code = OrganisationTagCodes.SETTLER,
            DisplayName = "Settler",
            TagType = TagType.MilitaryOrganisationalGroup,
            LeadershipModel = LeadershipModel.Unled,
            UnledBaseSize = 25,
            UnledDefaultSize = 100,
            UnledMaxSize = 200
        });

        AddTemplate(context);
        AddTiles(context);

        await context.SaveChangesAsync();

        _berriesAssetId = await AddAssetAsync(context, "Foodstuffs.PlantFoods.Berries", "Berries");
        _hidesAssetId = await AddAssetAsync(context, "RawMaterials.RawMaterials.Hides", "Hides");
    }

    #region What the dialog is offered

    [Test, Description("The ordinary case: the patch is offered with the units the selection would take and what that becomes, which is the promise the write then has to keep.")]
    public async Task ShouldOfferThePatchWithWhatTheSelectionWouldGainAsync()
    {
        await using (AscendantEmpireContext context = await DbFactory.CreateDbContextAsync())
        {
            await AddPatchAsync(context, "berries", "Berry Patch", quantityWhole: PlentifulQuantity, [(_berriesAssetId, _ONE_PER_UNIT)], iconPath: "/berries.png");
            await AddForceAsync(context, unitInstanceId: 5, healthyCount: 10);
        }

        ServiceResult<TakeResourcesOptions> result = await HandleAsync([5]);

        Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));

        TakeResourcesOption option = result.SuccessValue!.Resources.Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(option.DisplayName, Is.EqualTo("Berry Patch"));
            Assert.That(option.IconPath, Is.EqualTo("/berries.png"));
            Assert.That(option.Slot, Is.EqualTo(MapResourceInstance.MIN_SLOT));
            Assert.That(option.UnitsWouldTake, Is.EqualTo(WholeUnitsFor(10)), "Ten healthy individuals, at the per-individual rate.");
            Assert.That(option.WouldGain.Single().AssetId, Is.EqualTo(_berriesAssetId));
            Assert.That(option.WouldGain.Single().AssetDisplayName, Is.EqualTo("Berries"));
            Assert.That(option.WouldGain.Single().Amount, Is.EqualTo(WholeUnitsFor(10)), "At 1.0 per unit the gain is the units themselves.");
        }
    }

    [Test, Description("A yield's rate is what turns units into cargo, so the promised gain has to be priced at it rather than reported as the units.")]
    public async Task ShouldPriceTheGainAtTheYieldsRateAsync()
    {
        await using (AscendantEmpireContext context = await DbFactory.CreateDbContextAsync())
        {
            // Half an Asset per unit.
            await AddPatchAsync(context, "deer", "Deer Herd", quantityWhole: PlentifulQuantity, [(_hidesAssetId, _ONE_PER_UNIT / 2)]);
            await AddForceAsync(context, unitInstanceId: 5, healthyCount: 10);
        }

        ServiceResult<TakeResourcesOptions> result = await HandleAsync([5]);

        TakeResourcesOption option = result.SuccessValue!.Resources.Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(option.UnitsWouldTake, Is.EqualTo(WholeUnitsFor(10)));
            Assert.That(option.WouldGain.Single().Amount, Is.EqualTo(WholeUnitsFor(10) / 2));
        }
    }

    [Test, Description("The promise is bounded by what is actually there, since a Force cannot be told it will gain more than the patch holds.")]
    public async Task ShouldCapThePromiseAtWhatThePatchHoldsAsync()
    {
        // Half of what the Force could carry, so the promise can only be the patch's own figure.
        int quantityWhole = WholeUnitsFor(10) / 2;

        await using (AscendantEmpireContext context = await DbFactory.CreateDbContextAsync())
        {
            await AddPatchAsync(context, "berries", "Berry Patch", quantityWhole, [(_berriesAssetId, _ONE_PER_UNIT)]);
            await AddForceAsync(context, unitInstanceId: 5, healthyCount: 10);
        }

        ServiceResult<TakeResourcesOptions> result = await HandleAsync([5]);

        Assert.That(result.SuccessValue!.Resources.Single().UnitsWouldTake, Is.EqualTo(quantityWhole));
    }

    [Test, Description("The whole selection takes together, so the promise pools every selected Force's carriers rather than reporting one Force's share.")]
    public async Task ShouldPoolTheWholeSelectionsCarriersAsync()
    {
        await using (AscendantEmpireContext context = await DbFactory.CreateDbContextAsync())
        {
            await AddPatchAsync(context, "berries", "Berry Patch", quantityWhole: PlentifulQuantity, [(_berriesAssetId, _ONE_PER_UNIT)]);
            await AddForceAsync(context, unitInstanceId: 5, healthyCount: 10);
            await AddForceAsync(context, unitInstanceId: 6, healthyCount: 4);
        }

        ServiceResult<TakeResourcesOptions> result = await HandleAsync([5, 6]);

        Assert.That(result.SuccessValue!.Resources.Single().UnitsWouldTake, Is.EqualTo(WholeUnitsFor(14)), "Fourteen healthy individuals between them.");
    }

    [Test, Description("A tile holding two resources offers both, in slot order, because choosing between them is the only thing the dialog exists for.")]
    public async Task ShouldOfferBothOfATilesResourcesInSlotOrderAsync()
    {
        await using (AscendantEmpireContext context = await DbFactory.CreateDbContextAsync())
        {
            // Added out of slot order, so an accidental insertion-order result would fail rather than pass by luck.
            await AddPatchAsync(context, "deer", "Deer Herd", quantityWhole: PlentifulQuantity, [(_hidesAssetId, _ONE_PER_UNIT)], slot: MapResourceInstance.MAX_SLOT);
            await AddPatchAsync(context, "berries", "Berry Patch", quantityWhole: PlentifulQuantity, [(_berriesAssetId, _ONE_PER_UNIT)], slot: MapResourceInstance.MIN_SLOT);

            await AddForceAsync(context, unitInstanceId: 5, healthyCount: 10);
        }

        ServiceResult<TakeResourcesOptions> result = await HandleAsync([5]);

        Assert.That(result.SuccessValue!.Resources.Select(resource => resource.DisplayName), Is.EqualTo(new[] { "Berry Patch", "Deer Herd" }).AsCollection);
    }

    [Test, Description("A bare tile succeeds with nothing to offer, so the dialog says there is nothing here rather than showing an error.")]
    public async Task ShouldSucceedWithNoResourcesOnABareTileAsync()
    {
        await using (AscendantEmpireContext context = await DbFactory.CreateDbContextAsync())
        {
            await AddForceAsync(context, unitInstanceId: 5, healthyCount: 10);
        }

        ServiceResult<TakeResourcesOptions> result = await HandleAsync([5]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));
            Assert.That(result.SuccessValue!.Resources, Is.Empty);
        }
    }

    [Test, Description("A Force with no healthy individuals is promised nothing, which is the same figure the blocked button is about.")]
    public async Task ShouldPromiseNothingToAForceWithNoHealthyIndividualsAsync()
    {
        await using (AscendantEmpireContext context = await DbFactory.CreateDbContextAsync())
        {
            await AddPatchAsync(context, "berries", "Berry Patch", quantityWhole: PlentifulQuantity, [(_berriesAssetId, _ONE_PER_UNIT)]);
            await AddForceAsync(context, unitInstanceId: 5, healthyCount: 0, injuredCount: 10);
        }

        ServiceResult<TakeResourcesOptions> result = await HandleAsync([5]);

        TakeResourcesOption option = result.SuccessValue!.Resources.Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(option.UnitsWouldTake, Is.Zero);
            Assert.That(option.WouldGain.Single().Amount, Is.Zero);
        }
    }

    #endregion

    #region Refusals

    [Test, Description("Another player's Force is refused exactly as a missing one, or the response would let ids be probed for what a rival is fielding.")]
    public async Task ShouldRefuseAnotherPlayersForceAsync()
    {
        await using (AscendantEmpireContext context = await DbFactory.CreateDbContextAsync())
        {
            await AddPatchAsync(context, "berries", "Berry Patch", quantityWhole: PlentifulQuantity, [(_berriesAssetId, _ONE_PER_UNIT)]);
            await AddForceAsync(context, unitInstanceId: 8, playerId: _OTHER_PLAYER_ID, healthyCount: 10);
        }

        ServiceResult<TakeResourcesOptions> result = await HandleAsync([8]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.NotFound));
        }
    }

    [Test, Description("A Force id that matches nothing is not found, which is what stops the dialog opening on a Force that has since been wiped out.")]
    public async Task ShouldRefuseAForceThatDoesNotExistAsync()
    {
        ServiceResult<TakeResourcesOptions> result = await HandleAsync([999]);

        Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.NotFound));
    }

    [Test, Description("One resource cannot be taken from two tiles at once, so a selection spanning them is refused rather than having a tile picked for it.")]
    public async Task ShouldRefuseASelectionSpanningTwoTilesAsync()
    {
        await using (AscendantEmpireContext context = await DbFactory.CreateDbContextAsync())
        {
            await AddPatchAsync(context, "berries", "Berry Patch", quantityWhole: PlentifulQuantity, [(_berriesAssetId, _ONE_PER_UNIT)]);
            await AddForceAsync(context, unitInstanceId: 5, healthyCount: 10);
            await AddForceAsync(context, unitInstanceId: 6, healthyCount: 10, campaignMapTileId: _OTHER_TILE_ID);
        }

        ServiceResult<TakeResourcesOptions> result = await HandleAsync([5, 6]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(result.Errors, Has.One.Contains("not all on the same tile"));
        }
    }

    [Test, Description("An empty selection is a malformed request, caught by validation rather than reaching the builder as a selection of nothing.")]
    public async Task ShouldRefuseAnEmptySelectionAsync()
    {
        ServiceResult<TakeResourcesOptions> result = await HandleAsync([]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(result.Errors, Has.One.Contains("At least one Force Id is required."));
        }
    }

    #endregion

    #region Fixtures

    /// <summary>Whole units a selection of this many healthy individuals is promised, where the patch holds enough.</summary>
    /// <remarks>
    /// Read off <see cref="TakeResourcesRules"/> rather than written out, since the per-individual rate is tuning standing in for a carry weight that does
    /// not exist yet, and retuning it must not falsify every promise here.
    /// </remarks>
    private static int WholeUnitsFor(int healthyCount)
    {
        return (int)ScaledAmount.FromRaw(TakeResourcesRules.CalculateUnitsSoughtRaw(healthyCount)).ToWholeRoundedDown();
    }

    /// <summary>A quantity no selection here can empty, so a test about the promise is never also a test about the clamp.</summary>
    private static int PlentifulQuantity => WholeUnitsFor(1_000);

    private async Task<ServiceResult<TakeResourcesOptions>> HandleAsync(int[] forceIds, int playerId = _PLAYER_ID)
    {
        return await _handler.Handle(new GetTakeResourcesOptionsRequest(_GAME_ID, playerId, forceIds), CancellationToken.None);
    }

    /// <summary>Adds the one template every Force here is built from, plus the compiled snapshot every runtime read goes through.</summary>
    private static void AddTemplate(AscendantEmpireContext context)
    {
        context.UnitTemplates.Add(new UnitTemplate
        {
            Id = _TEMPLATE_ID,
            DisplayName = "Settler",
            MapImagePath = "settler.png",
            BodyComponentId = 1
        });

        context.CompiledUnitTemplates.Add(new CompiledUnitTemplate
        {
            UnitTemplateId = _TEMPLATE_ID,
            DisplayName = "Settler",
            MapImagePath = "settler.png",
            BodyComponentId = 1,
            CampaignMovementPoints = _CAMPAIGN_MOVEMENT_POINTS,
            OrganisationTagId = _TAG_ID,
            Capabilities = []
        });
    }

    /// <summary>
    /// Adds the two tiles the Forces stand on, plus the biome and geography they draw on.
    /// </summary>
    /// <remarks>
    /// Real tiles are needed because the context builder projects the tile's water flag and geography off the Force's <c>CampaignMap</c> navigation.
    /// </remarks>
    private static void AddTiles(AscendantEmpireContext context)
    {
        var flat = new CampaignGeography
        {
            CampaignMapGeographyTypeId = CampaignMapGeographyType.Flat,
            Name = "Flat",
            MovementCost = 1m,
            IsWater = false,
            IsTraversable = true,
            MapEditorIcon = null,
            IsPaintableOnCampaignMap = true
        };

        var grassland = new CampaignBiome
        {
            BiomeTypeId = BiomeType.Grassland,
            Name = "Grassland",
            MovementCost = 1m,
            IsWater = false,
            IsTraversable = true,
            MapEditorHtmlColor = "#7cb342"
        };

        context.CampaignGeographies.Add(flat);
        context.CampaignBiomes.Add(grassland);

        AddTile(_TILE_ID, xCoord: 1);
        AddTile(_OTHER_TILE_ID, xCoord: 2);

        void AddTile(int tileId, int xCoord)
        {
            context.CampaignMaps.Add(new CampaignMap
            {
                CampaignMapId = tileId,
                GameId = _GAME_ID,
                XCoord = xCoord,
                YCoord = 1,
                BiomeTypeId = grassland.BiomeTypeId,
                CampaignMapGeographyTypeId = flat.CampaignMapGeographyTypeId,
                Biome = grassland,
                Geography = flat,
                Game = null!,
                FirstVisitor = null,
                LastVisitor = null,
                OwnedBy = null
            });
        }
    }

    /// <summary>Adds a Force, defaulting to the player's own unmoved settler on the shared tile.</summary>
    private static async Task AddForceAsync(
        AscendantEmpireContext context,
        int unitInstanceId,
        int healthyCount,
        int playerId = _PLAYER_ID,
        int injuredCount = 0,
        int campaignMapTileId = _TILE_ID)
    {
        context.UnitInstances.Add(new UnitInstance
        {
            Id = unitInstanceId,
            GameId = _GAME_ID,
            PlayerId = playerId,
            CampaignMapTileId = campaignMapTileId,
            UnitTemplateId = _TEMPLATE_ID,
            InitialStrength = healthyCount + injuredCount,
            HealthyCount = healthyCount,
            InjuredCount = injuredCount,
            CampaignMovementRemaining = _CAMPAIGN_MOVEMENT_POINTS
        });

        await context.SaveChangesAsync();
    }

    /// <summary>An Asset in the catalogue, bankable, so a yield pointed at it survives the reader.</summary>
    private static async Task<int> AddAssetAsync(AscendantEmpireContext context, string code, string displayName)
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
            StorageClassId = storageClass.Id
        };

        context.Assets.Add(asset);
        await context.SaveChangesAsync();

        return asset.Id;
    }

    /// <summary>A discovered MapResource on the shared tile, with its type and yields behind it.</summary>
    private static async Task AddPatchAsync(
        AscendantEmpireContext context,
        string code,
        string displayName,
        int quantityWhole,
        IReadOnlyCollection<(int AssetId, long AmountPerUnit)> yields,
        string? iconPath = null,
        byte slot = MapResourceInstance.MIN_SLOT)
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
            IsEnabledByDefault = true
        }));

        var instance = new MapResourceInstance
        {
            GameId = _GAME_ID,
            CampaignMapId = _TILE_ID,
            MapResourceTypeId = type.Id,
            Slot = slot,
            Quantity = ScaledAmount.FromWhole(quantityWhole).RawValue,
            CreatedOnRound = 1
        };

        context.MapResourceInstances.Add(instance);
        await context.SaveChangesAsync();

        context.PlayerKnownMapResources.Add(new PlayerKnownMapResource
        {
            GameId = _GAME_ID,
            PlayerId = _PLAYER_ID,
            MapResourceInstanceId = instance.Id,
            DiscoveredOnRound = 1
        });

        await context.SaveChangesAsync();
    }

    #endregion
}
