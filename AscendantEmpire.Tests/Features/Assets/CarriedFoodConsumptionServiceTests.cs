using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Assets.Classes;
using AscendantEmpire.Application.Features.Assets.Services;
using AscendantEmpire.Application.Features.Maps.Depletion.Services;
using AscendantEmpire.Application.Helpers.ScaledNumerics;
using AscendantEmpire.Domain.Entities.Assets;
using AscendantEmpire.Domain.Enums.Assets;
using AscendantEmpire.Domain.Enums.Maps;
using AscendantEmpire.Tests.Features.Maps.Shared;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace AscendantEmpire.Tests.Features.Assets;

/// <summary>
/// Tests for <see cref="CarriedFoodConsumptionService"/>: what a Force eats out of its own load, whose load it may reach, and what the eating leaves behind.
/// </summary>
/// <remarks>
/// <para>
/// In-memory: the service reads and writes through LINQ, Remove and the change tracker only, and every rule it keeps is in code rather than in a database
/// constraint. The food catalogue is <see cref="FoodDesignSeed"/>'s, and the prices come through the real <see cref="SupplySetMembershipReader"/>, so what
/// counts as food here is what counts as food in play.
/// </para>
/// <para>
/// No <c>UnitInstance</c> or <c>Player</c> rows are written. The foreign keys are not enforced in memory and the service reads neither: what says a bank is a
/// load is its own <c>Kind</c>, and what says whose it is, its own <c>PlayerId</c>.
/// </para>
/// <para>
/// Every Asset here is priced at one per ration, so a holding's quantity is its rations. The conversion between the two measures is pinned on its own in
/// <see cref="RationLevellerTests"/>.
/// </para>
/// </remarks>
[TestFixture]
internal class CarriedFoodConsumptionServiceTests : InMemoryTestBase
{
    private const int _PLAYER_ID = 1;
    private const int _OTHER_PLAYER_ID = 2;

    private CarriedFoodConsumptionService _service = null!;
    private FoodDesignSeed.FoodDesign _design = null!;
    private IReadOnlyDictionary<int, IReadOnlyDictionary<int, long>> _memberships = null!;

    private int _berriesAssetId;
    private int _fishAssetId;
    private int _timberAssetId;

    #region Set up

    [SetUp]
    public async Task SetUpServiceAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        _design = await FoodDesignSeed.SeedAsync(context);
        _berriesAssetId = _design.AssetIdsByCode[FoodDesignSeed.BERRIES_ASSET_CODE];
        _fishAssetId = _design.AssetIdsByCode[SustenanceResourceAssets.CodesByResource[SustenanceResource.Fish]];
        _timberAssetId = await AddTimberAsync(context);

        _memberships = await new SupplySetMembershipReader().GetAmountsPerUnitAsync(context, [_design.SupplySetId]);

        _service = new CarriedFoodConsumptionService(NullLogger<CarriedFoodConsumptionService>.Instance);
    }

    #endregion

    #region What is eaten

    [Test, Description("Several edible things in one load are levelled to a common number of rations, so a Force runs out of all of them at once.")]
    public async Task ShouldLevelTheLoadToACommonNumberOfRationsAsync()
    {
        await AddUnitBankAsync(unitInstanceId: 1, new Dictionary<int, long> { [_berriesAssetId] = 50, [_fishAssetId] = 30 });

        IReadOnlyDictionary<int, ScaledAmount> fed = await ConsumeAsync(Draw(unitInstanceId: 1, shortfallWhole: 40));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(fed[1], Is.EqualTo(ScaledAmount.FromWhole(40)));
            Assert.That(await CarriedAsync(unitInstanceId: 1, _berriesAssetId), Is.EqualTo(ScaledAmount.FromWhole(20).RawValue));
            Assert.That(await CarriedAsync(unitInstanceId: 1, _fishAssetId), Is.EqualTo(ScaledAmount.FromWhole(20).RawValue));
        }
    }

    [Test, Description("An Asset the Force's food set does not price is not food, so timber in the load is left alone however hungry the Force is.")]
    public async Task ShouldLeaveAnAssetOutsideTheFoodSetAloneAsync()
    {
        await AddUnitBankAsync(unitInstanceId: 1, new Dictionary<int, long> { [_berriesAssetId] = 10, [_timberAssetId] = 100 });

        IReadOnlyDictionary<int, ScaledAmount> fed = await ConsumeAsync(Draw(unitInstanceId: 1, shortfallWhole: 50));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(fed[1], Is.EqualTo(ScaledAmount.FromWhole(10)), "Only the berries were food, and they covered ten of the fifty.");
            Assert.That(await CarriedAsync(unitInstanceId: 1, _timberAssetId), Is.EqualTo(ScaledAmount.FromWhole(100).RawValue));
        }
    }

    [Test, Description("A balance eaten to nothing is deleted rather than left as a zero, so a load does not accumulate a row per Asset it once held.")]
    public async Task ShouldDeleteABalanceEatenToNothingAsync()
    {
        await AddUnitBankAsync(unitInstanceId: 1, new Dictionary<int, long> { [_berriesAssetId] = 10 });

        await ConsumeAsync(Draw(unitInstanceId: 1, shortfallWhole: 10));

        Assert.That(await CarriedAsync(unitInstanceId: 1, _berriesAssetId), Is.Null);
    }

    [Test, Description("A strategic reserve is never eaten, and the row holding one survives being emptied of everything else.")]
    public async Task ShouldNotEatAStrategicReserveAsync()
    {
        await AddUnitBankAsync(
            unitInstanceId: 1,
            new Dictionary<int, long> { [_berriesAssetId] = 5 },
            reserves: new Dictionary<int, long> { [_berriesAssetId] = 20 });

        IReadOnlyDictionary<int, ScaledAmount> fed = await ConsumeAsync(Draw(unitInstanceId: 1, shortfallWhole: 40));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(fed[1], Is.EqualTo(ScaledAmount.FromWhole(5)), "Only what was in circulation was food.");
            Assert.That(await CarriedAsync(unitInstanceId: 1, _berriesAssetId), Is.Zero, "The row stayed, because a reserve is the one thing eating cannot reach.");
            Assert.That(await ReservedAsync(unitInstanceId: 1, _berriesAssetId), Is.EqualTo(ScaledAmount.FromWhole(20).RawValue));
        }
    }

    [Test, Description("A Force with no load of its own is passed over rather than failing the feed, which is the ordinary case: most Units carry nothing.")]
    public async Task ShouldPassOverAForceCarryingNothingAsync()
    {
        await AddUnitBankAsync(unitInstanceId: 2, new Dictionary<int, long> { [_berriesAssetId] = 10 });

        IReadOnlyDictionary<int, ScaledAmount> fed = await ConsumeAsync(
            Draw(unitInstanceId: 1, shortfallWhole: 10),
            Draw(unitInstanceId: 2, shortfallWhole: 10));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(fed.ContainsKey(1), Is.False);
            Assert.That(fed[2], Is.EqualTo(ScaledAmount.FromWhole(10)));
        }
    }

    #endregion

    #region Whose load it reaches

    [Test, Description("A Force eats its own load before a Unit sharing its network is asked for anything, so a sibling keeps what it has while the Force has rations.")]
    public async Task ShouldEatItsOwnLoadBeforeASiblingsAsync()
    {
        var networkKey = Guid.NewGuid();

        await AddUnitBankAsync(unitInstanceId: 1, new Dictionary<int, long> { [_berriesAssetId] = 10 }, networkKey: networkKey);
        await AddUnitBankAsync(unitInstanceId: 2, new Dictionary<int, long> { [_berriesAssetId] = 100 }, networkKey: networkKey);

        IReadOnlyDictionary<int, ScaledAmount> fed = await ConsumeAsync(Draw(unitInstanceId: 1, shortfallWhole: 40));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(fed[1], Is.EqualTo(ScaledAmount.FromWhole(40)));
            Assert.That(await CarriedAsync(unitInstanceId: 1, _berriesAssetId), Is.Null, "Its own ten went first.");
            Assert.That(await CarriedAsync(unitInstanceId: 2, _berriesAssetId), Is.EqualTo(ScaledAmount.FromWhole(70).RawValue), "The sibling paid only the thirty still wanting.");
        }
    }

    [Test, Description("An unkeyed load is in no network rather than in one with every other unkeyed load, so a Force with one eats nothing but its own.")]
    public async Task ShouldNotReachAnotherLoadThroughAnEmptyNetworkKeyAsync()
    {
        await AddUnitBankAsync(unitInstanceId: 1, new Dictionary<int, long> { [_berriesAssetId] = 10 }, networkKey: Guid.Empty);
        await AddUnitBankAsync(unitInstanceId: 2, new Dictionary<int, long> { [_berriesAssetId] = 100 }, networkKey: Guid.Empty);

        IReadOnlyDictionary<int, ScaledAmount> fed = await ConsumeAsync(Draw(unitInstanceId: 1, shortfallWhole: 40));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(fed[1], Is.EqualTo(ScaledAmount.FromWhole(10)));
            Assert.That(await CarriedAsync(unitInstanceId: 2, _berriesAssetId), Is.EqualTo(ScaledAmount.FromWhole(100).RawValue));
        }
    }

    [Test, Description("A load of another Player's is never eaten, even where a stale network key would otherwise let the Force reach it.")]
    public async Task ShouldNotEatAnotherPlayersLoadAsync()
    {
        var networkKey = Guid.NewGuid();

        await AddUnitBankAsync(unitInstanceId: 1, new Dictionary<int, long> { [_berriesAssetId] = 10 }, networkKey: networkKey);
        await AddUnitBankAsync(unitInstanceId: 2, new Dictionary<int, long> { [_berriesAssetId] = 100 }, networkKey: networkKey, playerId: _OTHER_PLAYER_ID);

        IReadOnlyDictionary<int, ScaledAmount> fed = await ConsumeAsync(Draw(unitInstanceId: 1, shortfallWhole: 40));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(fed[1], Is.EqualTo(ScaledAmount.FromWhole(10)));
            Assert.That(await CarriedAsync(unitInstanceId: 2, _berriesAssetId), Is.EqualTo(ScaledAmount.FromWhole(100).RawValue));
        }
    }

    [Test, Description("Where two Forces share a network the lower Force id eats first, so a pooled load is drawn down in a fixed order rather than by chance.")]
    public async Task ShouldFeedTheLowerForceIdFirstFromASharedNetworkAsync()
    {
        var networkKey = Guid.NewGuid();

        await AddUnitBankAsync(unitInstanceId: 1, new Dictionary<int, long> { [_berriesAssetId] = 5 }, networkKey: networkKey);
        await AddUnitBankAsync(unitInstanceId: 2, new Dictionary<int, long> { [_berriesAssetId] = 5 }, networkKey: networkKey);

        IReadOnlyDictionary<int, ScaledAmount> fed = await ConsumeAsync(
            Draw(unitInstanceId: 2, shortfallWhole: 10),
            Draw(unitInstanceId: 1, shortfallWhole: 10));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(fed[1], Is.EqualTo(ScaledAmount.FromWhole(10)), "The first Force ate the whole pool, its own load and its sibling's.");
            Assert.That(fed.ContainsKey(2), Is.False, "There was nothing left for the second, whichever order the draws arrived in.");
        }
    }

    #endregion

    #region Staging

    [Test, Description("The service stages and never saves, so a load is only emptied once the caller commits the feed it belongs to.")]
    public async Task ShouldNotSaveTheFeedItselfAsync()
    {
        await AddUnitBankAsync(unitInstanceId: 1, new Dictionary<int, long> { [_berriesAssetId] = 50 });

        await using (AscendantEmpireContext context = await DbFactory.CreateDbContextAsync())
        {
            await _service.ConsumeAsync(context, _PLAYER_ID, [Draw(unitInstanceId: 1, shortfallWhole: 40)], _memberships);
        }

        Assert.That(await CarriedAsync(unitInstanceId: 1, _berriesAssetId), Is.EqualTo(ScaledAmount.FromWhole(50).RawValue));
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Runs the service on its own context and saves, the way <c>Gathering</c> does once everything the feed did is staged.
    /// </summary>
    private async Task<IReadOnlyDictionary<int, ScaledAmount>> ConsumeAsync(params CarriedFoodDraw[] draws)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        IReadOnlyDictionary<int, ScaledAmount> fed = await _service.ConsumeAsync(context, _PLAYER_ID, draws, _memberships);

        await context.SaveChangesAsync();

        return fed;
    }

    private CarriedFoodDraw Draw(int unitInstanceId, long shortfallWhole)
    {
        return new CarriedFoodDraw(unitInstanceId, _design.SupplySetId, ScaledAmount.FromWhole(shortfallWhole));
    }

    /// <summary>Gives a Unit a load of the given whole quantities by Asset id, in a network of its own unless one is named.</summary>
    private async Task AddUnitBankAsync(
        int unitInstanceId,
        IReadOnlyDictionary<int, long> balances,
        Guid? networkKey = null,
        IReadOnlyDictionary<int, long>? reserves = null,
        int playerId = _PLAYER_ID)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        var bank = new AssetBank
        {
            PlayerId = playerId,
            Kind = AssetBankKind.Unit,
            UnitInstanceId = unitInstanceId,
            NetworkKey = networkKey ?? Guid.NewGuid()
        };

        context.AssetBanks.Add(bank);

        await context.SaveChangesAsync();

        foreach (int assetId in balances.Keys.Union(reserves?.Keys ?? []))
        {
            context.AssetBankBalances.Add(new AssetBankBalance
            {
                AssetBankId = bank.Id,
                AssetId = assetId,
                Quantity = ScaledAmount.FromWhole(balances.GetValueOrDefault(assetId)).RawValue,
                ReservedQuantity = ScaledAmount.FromWhole(reserves?.GetValueOrDefault(assetId) ?? 0).RawValue
            });
        }

        await context.SaveChangesAsync();
    }

    /// <summary>What a Unit is still carrying of one Asset, raw, or null where the row has gone.</summary>
    private async Task<long?> CarriedAsync(int unitInstanceId, int assetId)
    {
        return await ReadBalanceAsync(unitInstanceId, assetId, balance => balance.Quantity);
    }

    private async Task<long?> ReservedAsync(int unitInstanceId, int assetId)
    {
        return await ReadBalanceAsync(unitInstanceId, assetId, balance => balance.ReservedQuantity);
    }

    private async Task<long?> ReadBalanceAsync(int unitInstanceId, int assetId, Func<AssetBankBalance, long> column)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        AssetBankBalance? balance = await context.AssetBankBalances
            .SingleOrDefaultAsync(row => row.Bank.UnitInstanceId == unitInstanceId && row.AssetId == assetId);

        return balance is null ? null : column(balance);
    }

    /// <summary>
    /// An Asset the food set does not hold, for the load that is carrying something other than food.
    /// </summary>
    private static async Task<int> AddTimberAsync(AscendantEmpireContext context)
    {
        Asset berries = await context.Assets.SingleAsync(asset => asset.Code == FoodDesignSeed.BERRIES_ASSET_CODE);

        var timber = new Asset
        {
            Code = "ConstructionMaterials.Wood.Timber",
            DisplayName = "Timber",
            IconPath = "/icon.png",
            IsGradedAsset = false,
            SortOrder = 99,
            AssetMetaCategoryId = berries.AssetMetaCategoryId,
            StorageClassId = berries.StorageClassId
        };

        context.Assets.Add(timber);

        await context.SaveChangesAsync();

        return timber.Id;
    }

    #endregion
}
