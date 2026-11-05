using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Assets.Classes;
using AscendantEmpire.Application.Features.Assets.Services;
using AscendantEmpire.Application.Features.Game.Handlers;
using AscendantEmpire.Application.Features.Settlements.Classes;
using AscendantEmpire.Application.Helpers.ScaledNumerics;
using AscendantEmpire.Domain.Entities.Assets;
using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Entities.Settlements;
using AscendantEmpire.Domain.Entities.Structures;
using AscendantEmpire.Domain.Enums.Maps;
using AscendantEmpire.Domain.Enums.Settlements;
using AscendantEmpire.Domain.Enums.Structures;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Tests.Features.Assets;

/// <summary>
/// Tests the capacity service's grouped read against a real database, which is the only tier that can say whether it translates at all.
/// </summary>
/// <remarks>
/// <para>
/// On the containerised tier because of the read's shape, not its rules. It groups on a key made of two navigations - <c>balance.Bank.SettlementId</c> and
/// <c>balance.Asset.StorageClassId</c> - and the in-memory provider runs <c>GroupBy</c> in LINQ-to-Objects, so the fixtures in
/// <see cref="AssetBankCapacityServiceTests"/> would pass whether or not a relational provider could take it. The failure that guards against is severe and
/// silent: the read runs inside the end-of-Turn pipeline, where an untranslatable query stops a Player ending their Turn.
/// </para>
/// <para>
/// The arithmetic itself is covered without a database in <see cref="SettlementStorageCapacityTests"/>, and the service's refusals in-memory, so this fixture
/// asks only what a real engine answers differently: that the read translates, sums and groups per Settlement.
/// </para>
/// <para>
/// <b>It cannot ask whether the classes are kept apart</b>, and nothing can until a building grants a dedicated store. With no dedicated capacities, every
/// class overflows straight into the one tier pool, so free capacity is the pool less everything held whatever class it is in. The class is still half the
/// grouping key, and what this fixture pins is that the key translates - the two sums landing on one pool is the arithmetic working, not the key being ignored.
/// </para>
/// </remarks>
[TestFixture]
internal class AssetBankCapacityServicePersistenceTests : GameSqlServerTestBase
{
    private const int _MAP_WIDTH = 4;
    private const int _MAP_HEIGHT = 4;

    // Two Assets of one storage class, so the sum has something to add, and one of another, so the key has something to separate.
    private const string _OAK_LOG_CODE = "ConstructionMaterials.Wood.Logs.OakLog";
    private const string _CEDAR_LOG_CODE = "ConstructionMaterials.Wood.Logs.CedarLog";
    private const string _FODDER_CODE = "RawMaterials.RawMaterials.Fodder";

    private const long _OAK_LOGS_HELD = 4;
    private const long _CEDAR_LOGS_HELD = 6;
    private const long _FODDER_HELD = 2;
    private const long _NEIGHBOUR_OAK_LOGS_HELD = 3;

    private int _gameId;
    private int _playerId;
    private int _settlementId;
    private int _neighbourSettlementId;

    private AssetBankCapacityService _capacityService = null!;

    [SetUp]
    public async Task SetUpTwoStockedSettlementsAsync()
    {
        await SeedGameAsync(mapWidth: _MAP_WIDTH, mapHeight: _MAP_HEIGHT, numberOfPlayers: 2);

        ServiceResult<int> setupResult = await _service.SetupGameAsync(
            new SetupGameRequest(GalleryMapId: _galleryIndexId, GameName: "Asset Bank Capacity Game", NumberOfPlayers: 2, BattleMapSize: 50, GenerationSeed: null),
            CancellationToken.None);

        Assert.That(setupResult.HasSucceeded, Is.True, () => string.Join("; ", setupResult.Errors));

        _gameId = setupResult.SuccessValue;

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        _playerId = await context.Players.Where(player => player.GameId == _gameId).OrderBy(player => player.Id).Select(player => player.Id).FirstAsync();

        int structureTypeId = await SeedSettlementHallAsync(context);

        _settlementId = await SeedSettlementWithBankAsync(context, structureTypeId, "Hilltown", new Dictionary<string, long>
        {
            [_OAK_LOG_CODE] = _OAK_LOGS_HELD,
            [_CEDAR_LOG_CODE] = _CEDAR_LOGS_HELD,
            [_FODDER_CODE] = _FODDER_HELD
        });

        // A second stocked Settlement, so "grouped per Settlement" is asked of a query that has another Settlement's rows to confuse with these.
        _neighbourSettlementId = await SeedSettlementWithBankAsync(context, structureTypeId, "Stonewatch", new Dictionary<string, long>
        {
            [_OAK_LOG_CODE] = _NEIGHBOUR_OAK_LOGS_HELD
        });

        _capacityService = new AssetBankCapacityService(NullLogger<AssetBankCapacityService>.Instance);
    }

    [Test, Description("The grouped read runs on a relational provider and sums what a Settlement holds in the database, across Assets and across storage classes.")]
    public async Task ShouldSumWhatIsHeldInTheDatabaseAsync()
    {
        int storageYardId = await StorageClassOfAsync(_OAK_LOG_CODE);
        int granaryId = await StorageClassOfAsync(_FODDER_CODE);

        IReadOnlyDictionary<int, SettlementStorageCapacity> capacities = await CapacitiesAsync(_settlementId, _neighbourSettlementId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(granaryId, Is.Not.EqualTo(storageYardId), "The arrangement needs two classes for the grouping key to carry both.");
            Assert.That(
                capacities[_settlementId].SpaceAvailable(storageYardId),
                Is.EqualTo(VillagePool() - Raw(_OAK_LOGS_HELD + _CEDAR_LOGS_HELD + _FODDER_HELD)),
                "Three balance rows over two classes come back as the two sums the pool is charged for.");
        }
    }

    [Test, Description("The read groups per Settlement, so one Settlement's stock never counts against another's free capacity.")]
    public async Task ShouldKeepEachSettlementApartAsync()
    {
        int storageYardId = await StorageClassOfAsync(_OAK_LOG_CODE);

        IReadOnlyDictionary<int, SettlementStorageCapacity> capacities = await CapacitiesAsync(_settlementId, _neighbourSettlementId);

        Assert.That(
            capacities[_neighbourSettlementId].SpaceAvailable(storageYardId),
            Is.EqualTo(VillagePool() - Raw(_NEIGHBOUR_OAK_LOGS_HELD)),
            "The neighbour is charged for its own Logs alone, not for Hilltown's stock.");
    }

    private static long VillagePool()
    {
        SettlementStorageRules.TryGetGenericPoolCapacity(SettlementTier.Village, out long rawCapacity);

        return rawCapacity;
    }

    private static long Raw(long wholeUnits)
    {
        return ScaledAmount.FromWhole(wholeUnits).RawValue;
    }

    private async Task<IReadOnlyDictionary<int, SettlementStorageCapacity>> CapacitiesAsync(params int[] settlementIds)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        ServiceResult<IReadOnlyDictionary<int, SettlementStorageCapacity>> result = await _capacityService.GetSettlementCapacitiesAsync(context, settlementIds);

        Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));

        return result.SuccessValue!;
    }

    private async Task<int> StorageClassOfAsync(string assetCode)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        return await context.Assets.Where(asset => asset.Code == assetCode).Select(asset => asset.StorageClassId).SingleAsync();
    }

    /// <summary>
    /// Stands in the one Structure kind this fixture's Settlements sit on, since the catalogue is authored data the app never creates.
    /// </summary>
    private static async Task<int> SeedSettlementHallAsync(AscendantEmpireContext context)
    {
        var hall = new Structure
        {
            Code = "settlement_hall",
            Name = "Settlement Hall",
            RequiresSlotType = TileSlotType.Primary,
            AllowedPositions = PositionOnTile.WholeTile,
            CanBeBuiltOnCampaignMap = true,
            CanBeBuiltOnBattleMap = false,
            DualMapBuildTrigger = false,
            BuildPointsRequired = 100,
            SightRange = 2,
            SightDirection = DetectionDirection.FullRadius,
            Asserts = StructureAssertion.Owned,
            MapImagePath = "hall.png",
            FullImagePath = "hall-full.png"
        };

        context.Set<Structure>().Add(hall);
        await context.SaveChangesAsync();

        return hall.Id;
    }

    /// <summary>
    /// Builds a Village on a free tile, with the bank it would have been born with and the balances given in whole units.
    /// </summary>
    private async Task<int> SeedSettlementWithBankAsync(
        AscendantEmpireContext context,
        int structureTypeId,
        string name,
        IReadOnlyDictionary<string, long> balances)
    {
        int tileId = await context.CampaignMaps
            .Where(tile => tile.GameId == _gameId
                && context.StructureInstances.Any(structure => structure.CampaignMapTileId == tile.CampaignMapId) == false)
            .OrderBy(tile => tile.CampaignMapId)
            .Select(tile => tile.CampaignMapId)
            .FirstAsync();

        var structure = new StructureInstance
        {
            GameId = _gameId,
            CampaignMapTileId = tileId,
            BattleMapTileId = null,
            StructureId = structureTypeId,
            SlotType = TileSlotType.Primary,
            Position = PositionOnTile.WholeTile,
            OwnedByPlayerId = _playerId,
            CreatedOnTurnId = 0,
            BuildPointsProgress = 100,
            BuildPointsPerTurn = 10
        };

        context.StructureInstances.Add(structure);
        await context.SaveChangesAsync();

        var settlement = new Settlement
        {
            GameId = _gameId,
            StructureInstanceId = structure.Id,
            FoundingPlayerId = _playerId,
            Name = name,
            Tier = SettlementTier.Village,
            Population = 20,
            FoundedOnRoundNumber = 1,
            FoundedOnTurnId = 0
        };

        context.Settlements.Add(settlement);
        await context.SaveChangesAsync();

        var bank = new AssetBank
        {
            PlayerId = _playerId,
            SettlementId = settlement.Id,
            NetworkKey = Guid.NewGuid()
        };

        context.AssetBanks.Add(bank);
        await context.SaveChangesAsync();

        foreach ((string assetCode, long wholeQuantity) in balances)
        {
            int assetId = await context.Assets.Where(asset => asset.Code == assetCode).Select(asset => asset.Id).SingleAsync();

            context.AssetBankBalances.Add(new AssetBankBalance
            {
                AssetBankId = bank.Id,
                AssetId = assetId,
                Quantity = ScaledAmount.FromWhole(wholeQuantity).RawValue
            });
        }

        await context.SaveChangesAsync();

        return settlement.Id;
    }
}
