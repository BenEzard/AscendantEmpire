using AscendantEmpire.Application.Features.Maps.Biomes.Classes;
using AscendantEmpire.Application.Features.Maps.Depletion.Classes;
using AscendantEmpire.Application.Features.Maps.Depletion.Services;
using AscendantEmpire.Application.Helpers.ScaledNumerics;
using AscendantEmpire.BackendContracts.CampaignMaps.GetPlayerCampaignMap;
using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.Tests.Features.Maps.Depletion;

/// <summary>
/// Tests for <see cref="TileFoodSources"/>: which sources a tile offers a Player, and how much of each Asset is available from them.
/// </summary>
/// <remarks>
/// <para>
/// No database - the sources are pure. The Player's values are built directly rather than read, since the formula behind them is covered in
/// <c>SustenanceValueCalculatorTests</c>, and the Asset ids are stand-ins because nothing here cares what they are.
/// </para>
/// <para>
/// Grassland has Forage 400, Hunt 200, no Fish and Fodder 1,000. Ocean has Fish 300 only. The required set prices Berries at 1.67 and everything else at 1.0,
/// and holds no Timber at all.
/// </para>
/// </remarks>
[TestFixture]
internal class TileFoodSourcesTests
{
    #region Fixture

    private const int _FORAGE_ASSET = 10;
    private const int _GAME_ASSET = 11;
    private const int _FISH_ASSET = 12;
    private const int _FODDER_ASSET = 13;
    private const int _BERRIES_ASSET = 20;
    private const int _TIMBER_ASSET = 30;

    private static readonly PlayerSustenanceValues _VALUES = new(playerId: 1, new Dictionary<(BiomeType Biome, SustenanceResource Resource), ScaledAmount?>
    {
        [(BiomeType.Grassland, SustenanceResource.Forage)] = ScaledAmount.FromWhole(400),
        [(BiomeType.Grassland, SustenanceResource.Hunt)] = ScaledAmount.FromWhole(200),
        [(BiomeType.Grassland, SustenanceResource.Fish)] = null,
        [(BiomeType.Grassland, SustenanceResource.Fodder)] = ScaledAmount.FromWhole(1_000),
        [(BiomeType.Ocean, SustenanceResource.Forage)] = null,
        [(BiomeType.Ocean, SustenanceResource.Hunt)] = null,
        [(BiomeType.Ocean, SustenanceResource.Fish)] = ScaledAmount.FromWhole(300),
        [(BiomeType.Ocean, SustenanceResource.Fodder)] = null
    });

    private static readonly Dictionary<SustenanceResource, int> _SUSTENANCE_ASSET_IDS = new()
    {
        [SustenanceResource.Forage] = _FORAGE_ASSET,
        [SustenanceResource.Hunt] = _GAME_ASSET,
        [SustenanceResource.Fish] = _FISH_ASSET,
        [SustenanceResource.Fodder] = _FODDER_ASSET
    };

    private static readonly Dictionary<int, long> _AMOUNT_PER_UNIT = new()
    {
        [_FORAGE_ASSET] = 1_000,
        [_GAME_ASSET] = 1_000,
        [_FISH_ASSET] = 1_000,
        [_BERRIES_ASSET] = 1_670
    };

    #endregion

    #region Biome sources

    [Test, Description("A land tile offers Forage and Hunt only; Fodder is for animals and stays out even though the biome has it.")]
    public void ShouldOfferForageAndHuntOnLand()
    {
        IReadOnlyCollection<FoodSource> sources = GetAvailable(isWater: false, BiomeType.Grassland);

        Assert.That(AvailableByResource(sources), Is.EquivalentTo(new Dictionary<SustenanceResource, ScaledAmount>
        {
            [SustenanceResource.Forage] = ScaledAmount.FromWhole(400),
            [SustenanceResource.Hunt] = ScaledAmount.FromWhole(200)
        }));
    }

    [Test, Description("Each biome resource comes back as its own Asset, so what is eaten can be told apart and banked later.")]
    public void ShouldNameTheAssetOfEveryBiomeResource()
    {
        IReadOnlyCollection<FoodSource> sources = GetAvailable(isWater: false, BiomeType.Grassland);

        Assert.That(sources.Select(source => source.Key), Is.EquivalentTo(new[]
        {
            new FoodSourceKey(FoodSourceType.Tile, SustenanceResource.Forage, MapResourceInstanceId: null, _FORAGE_ASSET),
            new FoodSourceKey(FoodSourceType.Tile, SustenanceResource.Hunt, MapResourceInstanceId: null, _GAME_ASSET)
        }));
    }

    [Test, Description("A land tile with a river offers Fish as well, at the river's flat amount, with Forage and Hunt both raised 20% by the edge.")]
    public void ShouldOfferFishAndARicherLandTileOnARiverbank()
    {
        var rivers = new TileRivers { East = RiverWidth.Large };

        IReadOnlyCollection<FoodSource> sources = GetAvailable(isWater: false, BiomeType.Grassland, rivers: rivers);

        Assert.That(AvailableByResource(sources), Is.EquivalentTo(new Dictionary<SustenanceResource, ScaledAmount>
        {
            [SustenanceResource.Forage] = ScaledAmount.FromWhole(480),
            [SustenanceResource.Hunt] = ScaledAmount.FromWhole(240),
            [SustenanceResource.Fish] = ScaledAmount.FromWhole(30)
        }));
    }

    [Test, Description("A water tile at a river mouth is unchanged: the river raises its banks, not the sea it runs into.")]
    public void ShouldLeaveAWaterTileAloneWhenARiverMeetsIt()
    {
        var rivers = new TileRivers { North = RiverWidth.Large };

        IReadOnlyCollection<FoodSource> sources = GetAvailable(isWater: true, BiomeType.Ocean, rivers: rivers);

        Assert.That(AvailableByResource(sources), Is.EquivalentTo(new Dictionary<SustenanceResource, ScaledAmount>
        {
            [SustenanceResource.Fish] = ScaledAmount.FromWhole(300)
        }));
    }

    [Test, Description("A water tile offers Fish only.")]
    public void ShouldOfferOnlyFishOnWater()
    {
        IReadOnlyCollection<FoodSource> sources = GetAvailable(isWater: true, BiomeType.Ocean);

        Assert.That(AvailableByResource(sources), Is.EquivalentTo(new Dictionary<SustenanceResource, ScaledAmount>
        {
            [SustenanceResource.Fish] = ScaledAmount.FromWhole(300)
        }));
    }

    [Test, Description("A resource the biome does not have is left out altogether, rather than being offered at 0 like a stripped one.")]
    public void ShouldLeaveOutAResourceTheBiomeDoesNotHave()
    {
        // Grassland has no Fish, so a Grassland tile marked as water offers nothing at all.
        IReadOnlyCollection<FoodSource> sources = GetAvailable(isWater: true, BiomeType.Grassland);

        Assert.That(sources, Is.Empty);
    }

    [Test, Description("What has been taken is subtracted per resource and floored at 0, so a stripped resource is still offered at 0.")]
    public void ShouldSubtractWhatHasBeenTaken()
    {
        var depletion = new CampaignMapDepletion
        {
            CampaignMapId = 1,
            GameId = 1,
            ForageTaken = ScaledAmount.FromWhole(150).RawValue,
            HuntTaken = ScaledAmount.FromWhole(250).RawValue
        };

        IReadOnlyCollection<FoodSource> sources = GetAvailable(isWater: false, BiomeType.Grassland, depletion);

        Assert.That(AvailableByResource(sources), Is.EquivalentTo(new Dictionary<SustenanceResource, ScaledAmount>
        {
            [SustenanceResource.Forage] = ScaledAmount.FromWhole(250),
            [SustenanceResource.Hunt] = ScaledAmount.Zero
        }));
    }

    [Test, Description("A biome resource whose Asset the required set does not hold is not food, so a Force needing only Fish starves on a Forage plain.")]
    public void ShouldExcludeBiomeResourceWhoseAssetIsOutsideTheSet()
    {
        Dictionary<int, long> fishOnly = new() { [_FISH_ASSET] = 1_000 };

        IReadOnlyCollection<FoodSource> sources = GetAvailable(isWater: false, BiomeType.Grassland, amountPerUnitByAssetId: fishOnly);

        Assert.That(sources, Is.Empty);
    }

    #endregion

    #region MapResource sources

    [Test, Description("A berry patch on the tile is food, because its Asset is a member of the required set, and it is priced as that set prices it.")]
    public void ShouldIncludeMapResourceWhoseAssetIsInTheSet()
    {
        IReadOnlyCollection<FoodSource> sources = GetAvailable(
            isWater: false,
            BiomeType.Grassland,
            mapResourceStocks: [Stock(instanceId: 5, _BERRIES_ASSET, available: 1_000)]);

        FoodSource patch = sources.Single(source => source.Key.SourceType == FoodSourceType.MapResource);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(patch.Key, Is.EqualTo(new FoodSourceKey(FoodSourceType.MapResource, Resource: null, MapResourceInstanceId: 5, _BERRIES_ASSET)));
            Assert.That(patch.Available, Is.EqualTo(ScaledAmount.FromWhole(1_000)));
            Assert.That(patch.AmountPerNeedUnit, Is.EqualTo(1_670));
        }
    }

    [Test, Description("A timber patch is not food, set membership being what decides what may be eaten.")]
    public void ShouldExcludeMapResourceWhoseAssetIsOutsideTheSet()
    {
        IReadOnlyCollection<FoodSource> sources = GetAvailable(
            isWater: false,
            BiomeType.Grassland,
            mapResourceStocks: [Stock(instanceId: 5, _TIMBER_ASSET, available: 1_000)]);

        Assert.That(sources.Where(source => source.Key.SourceType == FoodSourceType.MapResource), Is.Empty);
    }

    [Test, Description("An instance holding its amount in child rows is skipped, not read as a patch that has been stripped bare.")]
    public void ShouldSkipMapResourceWithNoQuantityOfItsOwn()
    {
        IReadOnlyCollection<FoodSource> sources = GetAvailable(
            isWater: false,
            BiomeType.Grassland,
            mapResourceStocks: [new TileMapResourceStock(MapResourceInstanceId: 5, _BERRIES_ASSET, Available: null, IsYieldEnabled: true)]);

        Assert.That(sources.Where(source => source.Key.SourceType == FoodSourceType.MapResource), Is.Empty);
    }

    [Test, Description("A yield the Player has not switched on is not edible, so its patch contributes nothing.")]
    public void ShouldExcludeYieldSwitchedOffForThePlayer()
    {
        IReadOnlyCollection<FoodSource> sources = GetAvailable(
            isWater: false,
            BiomeType.Grassland,
            mapResourceStocks: [Stock(instanceId: 5, _BERRIES_ASSET, available: 1_000, isYieldEnabled: false)]);

        Assert.That(sources.Where(source => source.Key.SourceType == FoodSourceType.MapResource), Is.Empty);
    }

    [Test, Description("Two patches yielding one Asset stay separate sources, since each is its own stock and deplete independently.")]
    public void ShouldKeepTwoInstancesOfOneAssetApart()
    {
        IReadOnlyCollection<FoodSource> sources = GetAvailable(
            isWater: false,
            BiomeType.Grassland,
            mapResourceStocks: [Stock(instanceId: 5, _BERRIES_ASSET, available: 1_000), Stock(instanceId: 6, _BERRIES_ASSET, available: 400)]);

        Assert.That(
            sources.Where(source => source.Key.SourceType == FoodSourceType.MapResource).Select(source => (source.Key.MapResourceInstanceId, source.Available)),
            Is.EquivalentTo(new (int?, ScaledAmount)[]
            {
                (5, ScaledAmount.FromWhole(1_000)),
                (6, ScaledAmount.FromWhole(400))
            }));
    }

    #endregion

    #region Helpers

    private static IReadOnlyCollection<FoodSource> GetAvailable(
        bool isWater,
        BiomeType biome,
        CampaignMapDepletion? depletion = null,
        IReadOnlyDictionary<int, long>? amountPerUnitByAssetId = null,
        IReadOnlyCollection<TileMapResourceStock>? mapResourceStocks = null,
        TileRivers? rivers = null)
    {
        return TileFoodSources.GetAvailable(
            isWater,
            _VALUES,
            biome,
            depletion,
            _SUSTENANCE_ASSET_IDS,
            amountPerUnitByAssetId ?? _AMOUNT_PER_UNIT,
            mapResourceStocks ?? [],
            rivers);
    }

    /// <summary>
    /// What is available of each biome resource, for the tests that are about which resources a tile offers rather than how they are identified.
    /// </summary>
    private static Dictionary<SustenanceResource, ScaledAmount> AvailableByResource(IReadOnlyCollection<FoodSource> sources)
    {
        return sources
            .Where(source => source.Key.SourceType == FoodSourceType.Tile)
            .ToDictionary(source => source.Key.Resource!.Value, source => source.Available);
    }

    private static TileMapResourceStock Stock(int instanceId, int assetId, long available, bool isYieldEnabled = true)
    {
        return new TileMapResourceStock(instanceId, assetId, ScaledAmount.FromWhole(available), isYieldEnabled);
    }

    #endregion
}
