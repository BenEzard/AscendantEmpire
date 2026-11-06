using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Assets.Services;
using AscendantEmpire.Application.Features.Game.Handlers;
using AscendantEmpire.Domain.Entities.Assets;
using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Entities.Settlements;
using AscendantEmpire.Domain.Entities.Structures;
using AscendantEmpire.Domain.Enums.Maps;
using AscendantEmpire.Domain.Enums.Settlements;
using AscendantEmpire.Domain.Enums.Structures;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Tests.Features.Assets;

/// <summary>
/// Tests for what a successful conquest does to a bank, and for the save ordering only a real database can prove.
/// </summary>
/// <remarks>
/// <para>
/// On the containerised tier, for the reason <c>testing.md</c> names: the hand-over saves twice inside a transaction it opens itself, and the in-memory
/// provider has no transaction to give it. The refusals, which are settled before any of that, are covered in <see cref="AssetBankConquestServiceTests"/>.
/// </para>
/// <para>
/// <b>The connectivity service is substituted, but not blanked.</b> The recomputes are stubbed so each one can report what the database says the bank's owner
/// is at the moment it runs - which is the whole ordering contract, and is invisible otherwise because a recompute changes nothing under today's isolation.
/// <c>AssignKeyToCapturedBank</c> is forwarded to the real implementation, so the key the fixture reads back is the one production would write.
/// </para>
/// </remarks>
[TestFixture]
internal class AssetBankConquestServicePersistenceTests : GameSqlServerTestBase
{
    private const int _MAP_WIDTH = 4;
    private const int _MAP_HEIGHT = 4;

    private const string _OAK_LOG_CODE = "ConstructionMaterials.Wood.Logs.OakLog";
    private const string _CEDAR_LOG_CODE = "ConstructionMaterials.Wood.Logs.CedarLog";

    private const long _OAK_LOGS_HELD = 4_000;
    private const long _CEDAR_LOGS_HELD = 11_500;

    // Small enough that a release always fits a Village's stockpile, so the reserve cases pin the release rule rather than a capacity clamp.
    private const long _CAPTURED_OAK_RESERVE = 6_000;
    private const long _CONQUEROR_OAK_TARGET = 2_000;

    private int _gameId;
    private int _losingPlayerId;
    private int _conqueringPlayerId;
    private int _capturedSettlementId;
    private int _capturedBankId;
    private Guid _keyBeforeCapture;
    private int _structureTypeId;

    /// <summary>What the database said the captured bank's owner was each time a recompute ran, in the order the recomputes happened.</summary>
    private readonly List<RecomputeObservation> _recomputes = [];

    private IAssetNetworkConnectivityService _connectivityService = null!;
    private AssetBankConquestService _conquestService = null!;

    [SetUp]
    public async Task SetUpConqueredSettlementAsync()
    {
        _recomputes.Clear();

        await SeedGameAsync(mapWidth: _MAP_WIDTH, mapHeight: _MAP_HEIGHT, numberOfPlayers: 2);

        ServiceResult<int> setupResult = await _service.SetupGameAsync(
            new SetupGameRequest(GalleryMapId: _galleryIndexId, GameName: "Asset Bank Conquest Game", NumberOfPlayers: 2, BattleMapSize: 50, GenerationSeed: null),
            CancellationToken.None);

        Assert.That(setupResult.HasSucceeded, Is.True, () => string.Join("; ", setupResult.Errors));

        _gameId = setupResult.SuccessValue;

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        int[] playerIds = await context.Players
            .Where(player => player.GameId == _gameId)
            .OrderBy(player => player.Id)
            .Select(player => player.Id)
            .ToArrayAsync();

        _losingPlayerId = playerIds[0];
        _conqueringPlayerId = playerIds[1];

        _structureTypeId = await SeedSettlementHallAsync(context);

        (_capturedSettlementId, _capturedBankId, _keyBeforeCapture) = await SeedSettlementWithBankAsync(
            context,
            _structureTypeId,
            _losingPlayerId,
            "Riverhold",
            new Dictionary<string, long> { [_OAK_LOG_CODE] = _OAK_LOGS_HELD, [_CEDAR_LOG_CODE] = _CEDAR_LOGS_HELD });

        // A second Settlement the loser keeps, so "the loser is recomputed too" is asked of a Player who still has banks after the conquest.
        await SeedSettlementWithBankAsync(context, _structureTypeId, _losingPlayerId, "Stonewatch", new Dictionary<string, long>());

        BuildService();
    }

    [Test, Description("A conquest moves the bank's PlayerId and nothing else, because a Settlement's bank is its bank for the Settlement's whole life.")]
    public async Task ShouldMoveOwnershipWithoutMovingTheBankAsync()
    {
        await using (AscendantEmpireContext context = await DbFactory.CreateDbContextAsync())
        {
            ServiceResultBase result = await _conquestService.TransferOnConquestAsync(context, _capturedSettlementId, _conqueringPlayerId);

            Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));
        }

        AssetBank saved = await ReloadBankAsync(_capturedBankId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(saved.PlayerId, Is.EqualTo(_conqueringPlayerId));
            Assert.That(saved.SettlementId, Is.EqualTo(_capturedSettlementId), "The bank stays with its Settlement; only its owner moves.");
            Assert.That(saved.Id, Is.EqualTo(_capturedBankId), "No bank is created or dissolved by a conquest.");
        }
    }

    [Test, Description("The conqueror receives the full stock, so every balance row survives a conquest at the quantity it held before it.")]
    public async Task ShouldLeaveEveryBalanceUntouchedAsync()
    {
        await using (AscendantEmpireContext context = await DbFactory.CreateDbContextAsync())
        {
            await _conquestService.TransferOnConquestAsync(context, _capturedSettlementId, _conqueringPlayerId);
        }

        Dictionary<string, long> balances = await ReadBalancesAsync(_capturedBankId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(balances[_OAK_LOG_CODE], Is.EqualTo(_OAK_LOGS_HELD));
            Assert.That(balances[_CEDAR_LOG_CODE], Is.EqualTo(_CEDAR_LOGS_HELD));
            Assert.That(balances, Has.Count.EqualTo(2), "No balance row is added or removed by a conquest.");
        }
    }

    [Test, Description("The captured bank is given a brand-new key, so it cannot keep sharing a network with the banks the losing Player still holds.")]
    public async Task ShouldIsolateTheCapturedBankWithAFreshKeyAsync()
    {
        await using (AscendantEmpireContext context = await DbFactory.CreateDbContextAsync())
        {
            await _conquestService.TransferOnConquestAsync(context, _capturedSettlementId, _conqueringPlayerId);
        }

        AssetBank saved = await ReloadBankAsync(_capturedBankId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(saved.NetworkKey, Is.Not.EqualTo(_keyBeforeCapture));
            Assert.That(saved.NetworkKey, Is.Not.EqualTo(Guid.Empty), "An unkeyed bank would reach nothing at all, its own Settlement included.");
        }

        _connectivityService.Received(1).AssignKeyToCapturedBank(Arg.Is<AssetBank>(bank => bank.Id == _capturedBankId));
    }

    [Test, Description("Both players are recomputed, because losing the bridge between two halves of an empire partitions the loser as surely as it gains the conqueror.")]
    public async Task ShouldRecomputeBothPlayersNetworksAsync()
    {
        await using (AscendantEmpireContext context = await DbFactory.CreateDbContextAsync())
        {
            await _conquestService.TransferOnConquestAsync(context, _capturedSettlementId, _conqueringPlayerId);
        }

        Assert.That(_recomputes.Select(recompute => recompute.PlayerId), Is.EquivalentTo([_losingPlayerId, _conqueringPlayerId]));
    }

    [Test, Description("Every recompute sees the hand-over already saved, since a recompute reads ownership from the database and would otherwise assess both players against banks they no longer hold.")]
    public async Task ShouldSaveTheHandOverBeforeAnyRecomputeRunsAsync()
    {
        await using (AscendantEmpireContext context = await DbFactory.CreateDbContextAsync())
        {
            await _conquestService.TransferOnConquestAsync(context, _capturedSettlementId, _conqueringPlayerId);
        }

        Assert.That(_recomputes, Is.Not.Empty);

        Assert.That(
            _recomputes.Select(recompute => recompute.BankOwnerInDatabase),
            Is.All.EqualTo(_conqueringPlayerId),
            "A recompute that still read the old owner would key both empires against ownership neither of them has.");
    }

    [Test, Description("A recompute that fails rolls the hand-over back with it, so a conquest never leaves a bank moved but never re-keyed.")]
    public async Task ShouldRollTheHandOverBackWhenARecomputeFailsAsync()
    {
        _connectivityService
            .RecomputeForPlayerAsync(Arg.Any<AscendantEmpireContext>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Fail("The networks could not be recomputed."));

        ServiceResultBase result;

        await using (AscendantEmpireContext context = await DbFactory.CreateDbContextAsync())
        {
            result = await _conquestService.TransferOnConquestAsync(context, _capturedSettlementId, _conqueringPlayerId);
        }

        AssetBank saved = await ReloadBankAsync(_capturedBankId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(saved.PlayerId, Is.EqualTo(_losingPlayerId), "The ownership move was committed to nothing, so the loser still holds it.");
            Assert.That(saved.NetworkKey, Is.EqualTo(_keyBeforeCapture));
        }
    }

    [Test, Description("A captured reserve is released in full where the conqueror has no policy for that Asset, because holding it would leave them stock they never asked to set aside.")]
    public async Task ShouldReleaseAWholeCapturedReserveWhenTheConquerorHasNoPolicyAsync()
    {
        await GiveCapturedBankAReserveAsync(_CAPTURED_OAK_RESERVE);

        await using (AscendantEmpireContext context = await DbFactory.CreateDbContextAsync())
        {
            ServiceResultBase result = await _conquestService.TransferOnConquestAsync(context, _capturedSettlementId, _conqueringPlayerId);

            Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));
        }

        (long quantity, long reserved) = await ReadBalanceAsync(_capturedBankId, _OAK_LOG_CODE);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(reserved, Is.Zero, "With no policy to hold it against, the whole reserve should have been released.");
            Assert.That(quantity, Is.EqualTo(_OAK_LOGS_HELD + _CAPTURED_OAK_RESERVE), "A released quantity becomes ordinary stock rather than vanishing.");
        }
    }

    [Test, Description("A captured reserve is held up to the conqueror's own target and the excess released, so the policy that decides is the conqueror's rather than the loser's.")]
    public async Task ShouldHoldACapturedReserveUpToTheConquerorsTargetAsync()
    {
        await GiveCapturedBankAReserveAsync(_CAPTURED_OAK_RESERVE);

        await using (AscendantEmpireContext context = await DbFactory.CreateDbContextAsync())
        {
            await SeedReservePolicyAsync(context, _conqueringPlayerId, _OAK_LOG_CODE, _CONQUEROR_OAK_TARGET);
        }

        await using (AscendantEmpireContext context = await DbFactory.CreateDbContextAsync())
        {
            ServiceResultBase result = await _conquestService.TransferOnConquestAsync(context, _capturedSettlementId, _conqueringPlayerId);

            Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));
        }

        (long quantity, long reserved) = await ReadBalanceAsync(_capturedBankId, _OAK_LOG_CODE);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(reserved, Is.EqualTo(_CONQUEROR_OAK_TARGET), "The target is the ceiling on what stays in reserve.");
            Assert.That(
                quantity,
                Is.EqualTo(_OAK_LOGS_HELD + (_CAPTURED_OAK_RESERVE - _CONQUEROR_OAK_TARGET)),
                "Only the excess over the target should have been released.");
        }
    }

    [Test, Description("A target already met by reserves the conqueror holds elsewhere leaves no headroom, so the captured reserve is released in full rather than counted twice.")]
    public async Task ShouldReleaseACapturedReserveWhenTheConquerorsTargetIsAlreadyMetElsewhereAsync()
    {
        await GiveCapturedBankAReserveAsync(_CAPTURED_OAK_RESERVE);

        await using (AscendantEmpireContext context = await DbFactory.CreateDbContextAsync())
        {
            await SeedReservePolicyAsync(context, _conqueringPlayerId, _OAK_LOG_CODE, _CONQUEROR_OAK_TARGET);

            // The conqueror's own Settlement, already holding the whole target in reserve.
            await SeedSettlementWithBankAsync(
                context,
                _structureTypeId,
                _conqueringPlayerId,
                "Oakfast",
                new Dictionary<string, long>(),
                new Dictionary<string, long> { [_OAK_LOG_CODE] = _CONQUEROR_OAK_TARGET });
        }

        await using (AscendantEmpireContext context = await DbFactory.CreateDbContextAsync())
        {
            ServiceResultBase result = await _conquestService.TransferOnConquestAsync(context, _capturedSettlementId, _conqueringPlayerId);

            Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));
        }

        (long quantity, long reserved) = await ReadBalanceAsync(_capturedBankId, _OAK_LOG_CODE);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(reserved, Is.Zero, "The target was already met elsewhere, so the captured reserve had no headroom to sit in.");
            Assert.That(quantity, Is.EqualTo(_OAK_LOGS_HELD + _CAPTURED_OAK_RESERVE), "The whole reserve should have been released.");
        }
    }

    /// <summary>
    /// Puts a reserve on the captured bank's Oak Log balance, which the fixture otherwise seeds holding none.
    /// </summary>
    private async Task GiveCapturedBankAReserveAsync(long rawReserved)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        AssetBankBalance balance = await context.AssetBankBalances
            .Where(row => row.AssetBankId == _capturedBankId && row.Asset.Code == _OAK_LOG_CODE)
            .SingleAsync();

        balance.ReservedQuantity = rawReserved;

        await context.SaveChangesAsync();
    }

    /// <summary>
    /// Builds the service against a connectivity substitute that reports what each recompute saw, and still writes a real key.
    /// </summary>
    private void BuildService()
    {
        var realConnectivity = new AssetNetworkConnectivityService(NullLogger<AssetNetworkConnectivityService>.Instance);

        _connectivityService = Substitute.For<IAssetNetworkConnectivityService>();

        _connectivityService
            .When(connectivity => connectivity.AssignKeyToCapturedBank(Arg.Any<AssetBank>()))
            .Do(call => realConnectivity.AssignKeyToCapturedBank(call.Arg<AssetBank>()));

        _connectivityService
            .RecomputeForPlayerAsync(Arg.Any<AscendantEmpireContext>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(call => ObserveRecomputeAsync(call.Arg<AscendantEmpireContext>(), call.ArgAt<int>(1)));

        _conquestService = new AssetBankConquestService(
            NullLogger<AssetBankConquestService>.Instance,
            _connectivityService,
            new AssetBankCreditService(
                new AssetBankCapacityService(NullLogger<AssetBankCapacityService>.Instance),
                NullLogger<AssetBankCreditService>.Instance));
    }

    /// <summary>
    /// Stands in for a recompute, recording which Player it was asked about and who the database says owns the captured bank at that moment.
    /// </summary>
    /// <remarks>
    /// Read through the service's own context, so the query runs inside the transaction the hand-over was saved in: a separate connection would block on the
    /// uncommitted row rather than answer.
    /// </remarks>
    private async Task<ServiceResultBase> ObserveRecomputeAsync(AscendantEmpireContext context, int playerId)
    {
        int ownerInDatabase = await context.AssetBanks
            .AsNoTracking()
            .Where(bank => bank.Id == _capturedBankId)
            .Select(bank => bank.PlayerId)
            .SingleAsync();

        _recomputes.Add(new RecomputeObservation(playerId, ownerInDatabase));

        return Succeed();
    }

    /// <summary>
    /// Stands in the one Structure kind the fixture's Settlements sit on, since the catalogue is authored data the app never creates.
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
    /// Builds a Settlement on a free tile, with the bank it would have been born with and the balances given.
    /// </summary>
    /// <returns>The Settlement's id, its bank's id, and the key the bank starts out carrying.</returns>
    private async Task<(int SettlementId, int AssetBankId, Guid NetworkKey)> SeedSettlementWithBankAsync(
        AscendantEmpireContext context,
        int structureTypeId,
        int playerId,
        string name,
        IReadOnlyDictionary<string, long> balances,
        IReadOnlyDictionary<string, long>? reserves = null)
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
            OwnedByPlayerId = playerId,
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
            FoundingPlayerId = playerId,
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
            PlayerId = playerId,
            SettlementId = settlement.Id,
            NetworkKey = Guid.NewGuid()
        };

        context.AssetBanks.Add(bank);
        await context.SaveChangesAsync();

        // Walked over the union of the two dictionaries, so a bank can be seeded holding nothing but a reserve - which is the state the release rule has to
        // find, and the one a drawdown must not delete.
        foreach (string assetCode in balances.Keys.Union(reserves?.Keys ?? []))
        {
            int assetId = await context.Assets.Where(asset => asset.Code == assetCode).Select(asset => asset.Id).SingleAsync();

            context.AssetBankBalances.Add(new AssetBankBalance
            {
                AssetBankId = bank.Id,
                AssetId = assetId,
                Quantity = balances.TryGetValue(assetCode, out long quantity) ? quantity : 0,
                ReservedQuantity = reserves is not null && reserves.TryGetValue(assetCode, out long reserved) ? reserved : 0
            });
        }

        await context.SaveChangesAsync();

        return (settlement.Id, bank.Id, bank.NetworkKey);
    }

    /// <summary>
    /// Gives a Player an empire-wide reserve intent for one Asset.
    /// </summary>
    private static async Task SeedReservePolicyAsync(AscendantEmpireContext context, int playerId, string assetCode, long targetQuantity)
    {
        int assetId = await context.Assets.Where(asset => asset.Code == assetCode).Select(asset => asset.Id).SingleAsync();

        context.PlayerStrategicReservePolicies.Add(new PlayerStrategicReservePolicy
        {
            PlayerId = playerId,
            AssetId = assetId,
            TargetQuantity = targetQuantity,
            DiversionPercent = 10
        });

        await context.SaveChangesAsync();
    }

    /// <summary>
    /// Reads one bank's spendable and reserved figures for one Asset, as the database has them.
    /// </summary>
    private async Task<(long Quantity, long ReservedQuantity)> ReadBalanceAsync(int assetBankId, string assetCode)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        var balance = await context.AssetBankBalances
            .AsNoTracking()
            .Where(row => row.AssetBankId == assetBankId && row.Asset.Code == assetCode)
            .Select(row => new { row.Quantity, row.ReservedQuantity })
            .SingleAsync();

        return (balance.Quantity, balance.ReservedQuantity);
    }

    private async Task<AssetBank> ReloadBankAsync(int assetBankId)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        return await context.AssetBanks.AsNoTracking().SingleAsync(bank => bank.Id == assetBankId);
    }

    private async Task<Dictionary<string, long>> ReadBalancesAsync(int assetBankId)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        List<BalanceRow> rows = await context.AssetBankBalances
            .AsNoTracking()
            .Where(balance => balance.AssetBankId == assetBankId)
            .Select(balance => new BalanceRow(balance.Asset.Code, balance.Quantity))
            .ToListAsync();

        return rows.ToDictionary(row => row.AssetCode, row => row.Quantity);
    }

    private sealed record BalanceRow(string AssetCode, long Quantity);

    private sealed record RecomputeObservation(int PlayerId, int BankOwnerInDatabase);
}
