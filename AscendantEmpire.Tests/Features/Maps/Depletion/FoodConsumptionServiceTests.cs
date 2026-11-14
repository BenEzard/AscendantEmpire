using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Maps.Depletion.Classes;
using AscendantEmpire.Application.Features.Maps.Depletion.Services;
using AscendantEmpire.Application.Helpers.ScaledNumerics;
using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Entities.Maps.MapResources;
using AscendantEmpire.Domain.Enums.MapResources;
using AscendantEmpire.Domain.Enums.Maps;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace AscendantEmpire.Tests.Features.Maps.Depletion;

/// <summary>
/// Tests for <see cref="FoodConsumptionService"/>: that what a tile's Forces ate is charged to the right store, and that the service leaves saving to its
/// caller.
/// </summary>
/// <remarks>
/// On the in-memory tier. The service reads two tables through LINQ and writes through the change tracker; nothing here turns on a constraint, a collation
/// or generated SQL. What a source is worth, and how the need is split across sources, are pinned without a database in <c>FoodExtractionCalculatorTests</c>.
/// </remarks>
[TestFixture]
internal class FoodConsumptionServiceTests : InMemoryTestBase
{
    #region Fixture

    private const int _GAME_ID = 1;
    private const int _TILE_ID = 101;
    private const int _BERRIES_ASSET_ID = 20;

    /// <summary>
    /// A patch yielding one berry per unit of its quantity, so an amount of Asset and an amount of quantity are the same number.
    /// </summary>
    private const long _ONE_FOR_ONE = 1_000;

    private FoodConsumptionService _service = null!;

    [SetUp]
    public void SetUpService()
    {
        _service = new FoodConsumptionService(NullLogger<FoodConsumptionService>.Instance);
    }

    #endregion

    #region Tile sources

    [Test, Description("A tile source raises the matching Taken column, so the biome's food is spent where depletion reads it back.")]
    public async Task ShouldRaiseTakenWhenSourceIsTheTileAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        var depletion = new CampaignMapDepletion { CampaignMapId = _TILE_ID, GameId = _GAME_ID, ForageTaken = ScaledAmount.FromWhole(10).RawValue };

        await _service.ConsumeAsync(context, _GAME_ID, [Consumption(depletion, (TileSource(SustenanceResource.Forage), 40), (TileSource(SustenanceResource.Hunt), 25))]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(depletion.ForageTaken, Is.EqualTo(ScaledAmount.FromWhole(50).RawValue), "Taken adds to what was there, since it persists between Turns.");
            Assert.That(depletion.HuntTaken, Is.EqualTo(ScaledAmount.FromWhole(25).RawValue));
            Assert.That(depletion.FishTaken, Is.Zero);
        }
    }

    #endregion

    #region MapResource sources

    [Test, Description("A MapResource source lowers the instance's own quantity, a patch being a stock rather than a rate that regrows.")]
    public async Task ShouldLowerInstanceQuantityWhenSourceIsAMapResourceAsync()
    {
        int instanceId = await SeedPatchAsync(quantity: 1_000, amountPerUnit: _ONE_FOR_ONE);

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        await _service.ConsumeAsync(context, _GAME_ID, [Consumption(Depletion(), (PatchSource(instanceId), 400))]);
        await context.SaveChangesAsync();

        Assert.That(await QuantityAsync(instanceId), Is.EqualTo(ScaledAmount.FromWhole(600).RawValue));
    }

    [Test, Description("A patch whose Asset costs more than one unit of it loses the units that Asset came out of, not the Asset amount itself.")]
    public async Task ShouldLowerQuantityByTheUnitsBehindTheAssetAsync()
    {
        // Two berries a unit, so 400 berries came out of 200 units of patch.
        int instanceId = await SeedPatchAsync(quantity: 1_000, amountPerUnit: 2_000);

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        await _service.ConsumeAsync(context, _GAME_ID, [Consumption(Depletion(), (PatchSource(instanceId), 400))]);
        await context.SaveChangesAsync();

        Assert.That(await QuantityAsync(instanceId), Is.EqualTo(ScaledAmount.FromWhole(800).RawValue));
    }

    [Test, Description("A patch is never left owing food, so a stripped patch floors at nothing rather than going negative.")]
    public async Task ShouldFloorQuantityAtZeroAsync()
    {
        int instanceId = await SeedPatchAsync(quantity: 100, amountPerUnit: _ONE_FOR_ONE);

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        await _service.ConsumeAsync(context, _GAME_ID, [Consumption(Depletion(), (PatchSource(instanceId), 400))]);
        await context.SaveChangesAsync();

        Assert.That(await QuantityAsync(instanceId), Is.Zero);
    }

    [Test, Description("A patch in another Game is never charged, so one Game's Forces cannot eat another's berries.")]
    public async Task ShouldNotChargeAPatchInAnotherGameAsync()
    {
        int instanceId = await SeedPatchAsync(quantity: 1_000, amountPerUnit: _ONE_FOR_ONE, gameId: _GAME_ID + 1);

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        await _service.ConsumeAsync(context, _GAME_ID, [Consumption(Depletion(), (PatchSource(instanceId), 400))]);
        await context.SaveChangesAsync();

        Assert.That(await QuantityAsync(instanceId), Is.EqualTo(ScaledAmount.FromWhole(1_000).RawValue));
    }

    #endregion

    #region Saving

    [Test, Description("Nothing is saved by the service itself, so the caller's feed and its casualties land in one save.")]
    public async Task ShouldNotSaveChangesAsync()
    {
        int instanceId = await SeedPatchAsync(quantity: 1_000, amountPerUnit: _ONE_FOR_ONE);

        await using (AscendantEmpireContext context = await DbFactory.CreateDbContextAsync())
        {
            await _service.ConsumeAsync(context, _GAME_ID, [Consumption(Depletion(), (PatchSource(instanceId), 400))]);
        }

        Assert.That(await QuantityAsync(instanceId), Is.EqualTo(ScaledAmount.FromWhole(1_000).RawValue), "The context was dropped unsaved, so the patch is untouched.");
    }

    #endregion

    #region Helpers

    private static CampaignMapDepletion Depletion()
    {
        return new CampaignMapDepletion { CampaignMapId = _TILE_ID, GameId = _GAME_ID };
    }

    private static FoodSourceKey TileSource(SustenanceResource resource)
    {
        return new FoodSourceKey(FoodSourceType.Tile, resource, MapResourceInstanceId: null, AssetId: 1);
    }

    private static FoodSourceKey PatchSource(int instanceId)
    {
        return new FoodSourceKey(FoodSourceType.MapResource, Resource: null, instanceId, _BERRIES_ASSET_ID);
    }

    private static TileFoodConsumption Consumption(CampaignMapDepletion depletion, params (FoodSourceKey Key, long Taken)[] taken)
    {
        return new TileFoodConsumption(depletion, taken.ToDictionary(entry => entry.Key, entry => ScaledAmount.FromWhole(entry.Taken)));
    }

    /// <summary>
    /// Seeds a berry patch holding the given whole quantity, whose type yields the given amount of berries per unit.
    /// </summary>
    private async Task<int> SeedPatchAsync(long quantity, long amountPerUnit, int gameId = _GAME_ID)
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
            AssetId = _BERRIES_ASSET_ID,
            AmountPerUnit = amountPerUnit,
            IsEnabledByDefault = true
        });

        var instance = new MapResourceInstance
        {
            GameId = gameId,
            CampaignMapId = _TILE_ID,
            MapResourceTypeId = type.Id,
            Slot = MapResourceInstance.MIN_SLOT,
            Quantity = ScaledAmount.FromWhole(quantity).RawValue,
            CreatedOnRound = 1
        };

        context.MapResourceInstances.Add(instance);

        await context.SaveChangesAsync();

        return instance.Id;
    }

    private async Task<long?> QuantityAsync(int instanceId)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        return await context.MapResourceInstances.Where(instance => instance.Id == instanceId).Select(instance => instance.Quantity).SingleAsync();
    }

    #endregion
}
