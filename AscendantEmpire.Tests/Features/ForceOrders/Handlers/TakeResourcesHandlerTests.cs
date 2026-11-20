using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Assets.Services;
using AscendantEmpire.Application.Features.ForceOrders.ContextActions;
using AscendantEmpire.Application.Features.ForceOrders.ContextActions.Strategies;
using AscendantEmpire.Application.Features.ForceOrders.Handlers;
using AscendantEmpire.Application.Features.Game.Handlers;
using AscendantEmpire.Application.Features.Maps.MapResources.Services;
using AscendantEmpire.Application.Features.Turns.Services;
using AscendantEmpire.Application.Helpers.ScaledNumerics;
using AscendantEmpire.BackendContracts.ForceOrders.TakeResources;
using AscendantEmpire.BackendContracts.Turns.GetCurrentCampaignTurn;
using AscendantEmpire.Domain.Entities.Assets;
using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Entities.Maps.MapResources;
using AscendantEmpire.Domain.Entities.Units;
using AscendantEmpire.Domain.Enums.Assets;
using AscendantEmpire.Domain.Enums.Forces;
using AscendantEmpire.Domain.Enums.MapResources;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Tests.Features.ForceOrders.Handlers;

/// <summary>
/// Tests for <see cref="TakeResourcesHandler"/>: what loading a MapResource into a Force's cargo leaves behind, and what it refuses.
/// </summary>
/// <remarks>
/// <para>
/// On the containerised tier, for the reasons <c>testing.md</c> names: the take runs inside an explicit transaction, and the two rules that keep a Force
/// from ending up with two holds — <c>CK_AssetBank_KindTargetXor</c> and <c>UX_AssetBank_UnitInstance</c> — are database constraints the in-memory provider
/// does not have.
/// </para>
/// <para>
/// The <b>rules</b> are substituted, not the writers. <see cref="IForceContextActionService"/> stands in for <c>TakeResourcesStrategy</c>, which has its own
/// fixture and is re-run here only to prove the handler asks and obeys; the reader, the provisioning service and the cargo service are all real, because the
/// rows they write are what a take means.
/// </para>
/// <para>
/// The patch every test takes from yields 1.0 Berries per unit, so a credited balance reads as the units taken. The units come off
/// <see cref="TakeResourcesRules.UNITS_PER_HEALTHY_INDIVIDUAL"/>, which is tuning, so the figures are derived from it rather than written out.
/// </para>
/// </remarks>
[TestFixture]
internal class TakeResourcesHandlerTests : GameSqlServerTestBase
{
    private const int _MAP_WIDTH = 4;
    private const int _MAP_HEIGHT = 4;

    /// <summary>Enough movement that a Force paying its whole turn is a visible change rather than a no-op.</summary>
    private const long _STARTING_MOVEMENT = 2_000;

    /// <summary>1.0 Berries per unit of the patch, so a credited balance reads as the units taken.</summary>
    private const long _ONE_PER_UNIT = 1_000;

    private const string _BERRIES_ASSET_CODE = "Foodstuffs.PlantFoods.Berries";

    private int _gameId;
    private int _playerId;
    private int _otherPlayerId;
    private int _tileId;
    private int _otherTileId;
    private int _settlerTemplateId;
    private int _berriesAssetId;
    private int _mapResourceTypeId;

    private IForceContextActionService _contextActionService = null!;
    private ICampaignTurnService _campaignTurnService = null!;

    [SetUp]
    public async Task SetUpGameAsync()
    {
        await SeedGameAsync(mapWidth: _MAP_WIDTH, mapHeight: _MAP_HEIGHT, numberOfPlayers: 2);

        ServiceResult<int> setupResult = await _service.SetupGameAsync(
            new SetupGameRequest(GalleryMapId: _galleryIndexId, GameName: "Take Resources Game", NumberOfPlayers: 2, BattleMapSize: 50, GenerationSeed: null),
            CancellationToken.None);

        Assert.That(setupResult.HasSucceeded, Is.True, () => string.Join("; ", setupResult.Errors));

        _gameId = setupResult.SuccessValue;

        await using (AscendantEmpireContext context = await DbFactory.CreateDbContextAsync())
        {
            int[] playerIds = await context.Players.Where(player => player.GameId == _gameId).OrderBy(player => player.Id).Select(player => player.Id).ToArrayAsync();

            _playerId = playerIds[0];
            _otherPlayerId = playerIds[1];

            int[] tileIds = await context.CampaignMaps
                .Where(tile => tile.GameId == _gameId)
                .OrderBy(tile => tile.CampaignMapId)
                .Select(tile => tile.CampaignMapId)
                .Take(2)
                .ToArrayAsync();

            _tileId = tileIds[0];
            _otherTileId = tileIds[1];

            _settlerTemplateId = await context.UnitTemplates.Where(template => template.Code == "SETTLER").Select(template => template.Id).SingleAsync();
            _berriesAssetId = await context.Assets.Where(asset => asset.Code == _BERRIES_ASSET_CODE).Select(asset => asset.Id).SingleAsync();
        }

        _mapResourceTypeId = await SeedBerryTypeAsync();

        // The rules say yes unless a test says otherwise. The strategy itself is covered by TakeResourcesStrategyTests.
        _contextActionService = Substitute.For<IForceContextActionService>();

        _contextActionService
            .EvaluateActionAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<ForceActionType>(), Arg.Any<CancellationToken>())
            .Returns(ServiceResult<ForceActionAvailability>.Succeed(ForceActionAvailability.Available()));

        _campaignTurnService = Substitute.For<ICampaignTurnService>();
    }

    #region One Force taking

    [Test, Description("The whole of a take in one pass: the patch is charged, the hold is created and credited, and the Force has paid its turn for it.")]
    public async Task ShouldChargeThePatchCreditTheHoldAndSpendTheTurnAsync()
    {
        int playerTurnId = await OpenFirstPlayerTurnAsync(_gameId);
        ItIsThisPlayersTurn(playerTurnId);

        int instanceId = await SeedPatchAsync(quantityWhole: PlentifulQuantity);
        int forceId = await SeedForceAsync(healthyCount: 10);

        ServiceResult<TakeResourcesResult> result = await HandleAsync([forceId], instanceId);

        Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        MapResourceInstance patch = await context.MapResourceInstances.SingleAsync(instance => instance.Id == instanceId);
        UnitInstance force = await context.UnitInstances.SingleAsync(unit => unit.Id == forceId);
        AssetBank bank = await context.AssetBanks.SingleAsync(candidate => candidate.UnitInstanceId == forceId);
        AssetBankBalance balance = await context.AssetBankBalances.SingleAsync(row => row.AssetBankId == bank.Id);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(patch.Quantity, Is.EqualTo(ScaledAmount.FromWhole(PlentifulQuantity - WholeUnitsFor(10)).RawValue),
                "Ten healthy individuals carried off their share.");
            Assert.That(force.CampaignMovementRemaining, Is.Zero, "Loading up costs the Force its whole turn.");
            Assert.That(bank.Kind, Is.EqualTo(AssetBankKind.Unit), "The hold is cargo, which is what keeps it out of Empire totals.");
            Assert.That(bank.SettlementId, Is.Null, "Exactly one of the two references is set, as the kind/target constraint demands.");
            Assert.That(balance.AssetId, Is.EqualTo(_berriesAssetId));
            Assert.That(balance.Quantity, Is.EqualTo(ScaledAmount.FromWhole(WholeUnitsFor(10)).RawValue), "At 1.0 per unit the credit is the units themselves.");
            Assert.That(result.SuccessValue!.UnitsTaken, Is.EqualTo(WholeUnitsFor(10)));
            Assert.That(result.SuccessValue.IsExhausted, Is.False);
            Assert.That(result.SuccessValue.Gained.Single().AssetDisplayName, Is.EqualTo("Berries"));
            Assert.That(result.SuccessValue.Gained.Single().Amount, Is.EqualTo(WholeUnitsFor(10)));
        }
    }

    [Test, Description("A patch smaller than the Force can carry is emptied rather than overdrawn, and reported exhausted so the player knows the button will be gone.")]
    public async Task ShouldEmptyAPatchSmallerThanTheForceCanCarryAsync()
    {
        int playerTurnId = await OpenFirstPlayerTurnAsync(_gameId);
        ItIsThisPlayersTurn(playerTurnId);

        // Half of what the Force could carry, so the patch's own figure is the whole of the take.
        int quantityWhole = WholeUnitsFor(10) / 2;

        int instanceId = await SeedPatchAsync(quantityWhole);
        int forceId = await SeedForceAsync(healthyCount: 10);

        ServiceResult<TakeResourcesResult> result = await HandleAsync([forceId], instanceId);

        Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        MapResourceInstance patch = await context.MapResourceInstances.SingleAsync(instance => instance.Id == instanceId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(patch.Quantity, Is.Zero);
            Assert.That(result.SuccessValue!.UnitsTaken, Is.EqualTo(quantityWhole));
            Assert.That(result.SuccessValue.IsExhausted, Is.True, "A renewable patch is dormant rather than gone, but there is nothing left to take.");
        }
    }

    [Test, Description("A second take tops up the hold the Force already has, since provisioning hands back the existing bank rather than failing the unique index.")]
    public async Task ShouldReuseTheHoldOnASecondTakeAsync()
    {
        int playerTurnId = await OpenFirstPlayerTurnAsync(_gameId);
        ItIsThisPlayersTurn(playerTurnId);

        int instanceId = await SeedPatchAsync(quantityWhole: PlentifulQuantity);
        int forceId = await SeedForceAsync(healthyCount: 10);

        Assert.That((await HandleAsync([forceId], instanceId)).HasSucceeded, Is.True);

        // The first take spent the Force's turn; the strategy is substituted, so only the handler's own writes are under test here.
        await RestoreMovementAsync(forceId);

        ServiceResult<TakeResourcesResult> second = await HandleAsync([forceId], instanceId);

        Assert.That(second.HasSucceeded, Is.True, () => string.Join("; ", second.Errors));

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await context.AssetBanks.CountAsync(bank => bank.UnitInstanceId == forceId), Is.EqualTo(1), "One hold per Force, whatever it has carried before.");
            Assert.That((await context.AssetBankBalances.SingleAsync()).Quantity, Is.EqualTo(ScaledAmount.FromWhole(WholeUnitsFor(10) * 2).RawValue));
            Assert.That((await context.MapResourceInstances.SingleAsync(instance => instance.Id == instanceId)).Quantity,
                Is.EqualTo(ScaledAmount.FromWhole(PlentifulQuantity - (WholeUnitsFor(10) * 2)).RawValue));
        }
    }

    [Test, Description("Both of a type's Assets are credited, because a take gives everything the yield rows name rather than making the player pick one.")]
    public async Task ShouldCreditEveryYieldOfThePatchAsync()
    {
        int playerTurnId = await OpenFirstPlayerTurnAsync(_gameId);
        ItIsThisPlayersTurn(playerTurnId);

        int hidesAssetId = await AddYieldAsync("RawMaterials.RawMaterials.Hides", amountPerUnit: _ONE_PER_UNIT / 2);

        int instanceId = await SeedPatchAsync(quantityWhole: PlentifulQuantity);
        int forceId = await SeedForceAsync(healthyCount: 10);

        ServiceResult<TakeResourcesResult> result = await HandleAsync([forceId], instanceId);

        Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        Dictionary<int, long> quantityByAssetId = await context.AssetBankBalances.ToDictionaryAsync(row => row.AssetId, row => row.Quantity);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(quantityByAssetId[_berriesAssetId], Is.EqualTo(ScaledAmount.FromWhole(WholeUnitsFor(10)).RawValue));
            Assert.That(quantityByAssetId[hidesAssetId], Is.EqualTo(ScaledAmount.FromWhole(WholeUnitsFor(10) / 2).RawValue), "Half an Asset per unit.");
            Assert.That(result.SuccessValue!.Gained, Has.Exactly(2).Items);
        }
    }

    #endregion

    #region Several Forces taking

    [Test, Description("Forces are served in ascending id against a dwindling patch, so the second comes away with only what the first left.")]
    public async Task ShouldGiveTheSecondForceWhatTheFirstLeftAsync()
    {
        int playerTurnId = await OpenFirstPlayerTurnAsync(_gameId);
        ItIsThisPlayersTurn(playerTurnId);

        // One and a half Forces' worth, against two Forces of equal size.
        int quantityWhole = WholeUnitsFor(10) + (WholeUnitsFor(10) / 2);

        int instanceId = await SeedPatchAsync(quantityWhole);

        int firstForceId = await SeedForceAsync(healthyCount: 10);
        int secondForceId = await SeedForceAsync(healthyCount: 10);

        // Listed out of order, so the ascending-id rule has to come from the handler rather than from however the client listed them.
        ServiceResult<TakeResourcesResult> result = await HandleAsync([secondForceId, firstForceId], instanceId);

        Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        Dictionary<int, long> carriedByForceId = await context.AssetBanks
            .Where(bank => bank.UnitInstanceId != null)
            .Join(context.AssetBankBalances, bank => bank.Id, balance => balance.AssetBankId, (bank, balance) => new { bank.UnitInstanceId, balance.Quantity })
            .ToDictionaryAsync(row => row.UnitInstanceId!.Value, row => row.Quantity);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(carriedByForceId[firstForceId], Is.EqualTo(ScaledAmount.FromWhole(WholeUnitsFor(10)).RawValue), "The lower id loads up first.");
            Assert.That(carriedByForceId[secondForceId], Is.EqualTo(ScaledAmount.FromWhole(quantityWhole - WholeUnitsFor(10)).RawValue),
                "The second gets only the remainder.");
            Assert.That((await context.MapResourceInstances.SingleAsync(instance => instance.Id == instanceId)).Quantity, Is.Zero);
            Assert.That(result.SuccessValue!.UnitsTaken, Is.EqualTo(quantityWhole), "The total is what came off the patch, not what one Force carried.");
        }
    }

    [Test, Description("A Force that arrives at an emptied patch has done nothing, so it keeps its turn and is given no hold — but is still named in the result.")]
    public async Task ShouldLeaveAForceThatGetsNothingUntouchedAsync()
    {
        int playerTurnId = await OpenFirstPlayerTurnAsync(_gameId);
        ItIsThisPlayersTurn(playerTurnId);

        // Exactly one Force's load, against two.
        int instanceId = await SeedPatchAsync(WholeUnitsFor(10));

        int firstForceId = await SeedForceAsync(healthyCount: 10);
        int emptyHandedForceId = await SeedForceAsync(healthyCount: 10);

        ServiceResult<TakeResourcesResult> result = await HandleAsync([firstForceId, emptyHandedForceId], instanceId);

        Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        UnitInstance emptyHanded = await context.UnitInstances.SingleAsync(unit => unit.Id == emptyHandedForceId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(emptyHanded.CampaignMovementRemaining, Is.EqualTo(_STARTING_MOVEMENT), "It took nothing, so it has nothing to pay for.");
            Assert.That(await context.AssetBanks.AnyAsync(bank => bank.UnitInstanceId == emptyHandedForceId), Is.False,
                "And it is given no hold, since a hold with nothing in it is not worth a row.");
            Assert.That(result.SuccessValue!.Forces.Single(outcome => outcome.UnitInstanceId == emptyHandedForceId).UnitsTaken, Is.Zero,
                "It is still reported, because the player has to be told who came away empty-handed.");
        }
    }

    [Test, Description("Each taking Force gets a hold of its own, which is the rule the unique index on the Unit reference enforces.")]
    public async Task ShouldGiveEachTakingForceItsOwnHoldAsync()
    {
        int playerTurnId = await OpenFirstPlayerTurnAsync(_gameId);
        ItIsThisPlayersTurn(playerTurnId);

        int instanceId = await SeedPatchAsync(quantityWhole: PlentifulQuantity);

        int firstForceId = await SeedForceAsync(healthyCount: 10);
        int secondForceId = await SeedForceAsync(healthyCount: 10);

        Assert.That((await HandleAsync([firstForceId, secondForceId], instanceId)).HasSucceeded, Is.True);

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        int[] holdOwners = await context.AssetBanks
            .Where(bank => bank.UnitInstanceId != null)
            .Select(bank => bank.UnitInstanceId!.Value)
            .ToArrayAsync();

        Assert.That(holdOwners, Is.EquivalentTo(new[] { firstForceId, secondForceId }));
    }

    #endregion

    #region Refusals

    [Test, Description("Another player's turn refuses the order, since the toolbar does not check whose turn it is and the write path has to.")]
    public async Task ShouldRefuseWhenItIsAnotherPlayersTurnAsync()
    {
        int playerTurnId = await OpenFirstPlayerTurnAsync(_gameId);
        ItIsTheOtherPlayersTurn(playerTurnId);

        int instanceId = await SeedPatchAsync(quantityWhole: PlentifulQuantity);
        int forceId = await SeedForceAsync(healthyCount: 10);

        ServiceResult<TakeResourcesResult> result = await HandleAsync([forceId], instanceId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.Forbidden));
            Assert.That(result.Errors, Has.One.Contains("turn, so you cannot take resources yet"));
        }

        await AssertNothingWasTakenAsync(instanceId, quantityWhole: PlentifulQuantity);
    }

    [Test, Description("A Force belonging to someone else is refused outright, so a stolen id cannot load another player's Force up.")]
    public async Task ShouldRefuseAnotherPlayersForceAsync()
    {
        int playerTurnId = await OpenFirstPlayerTurnAsync(_gameId);
        ItIsThisPlayersTurn(playerTurnId);

        int instanceId = await SeedPatchAsync(quantityWhole: PlentifulQuantity);
        int foreignForceId = await SeedForceAsync(healthyCount: 10, playerId: _otherPlayerId);

        ServiceResult<TakeResourcesResult> result = await HandleAsync([foreignForceId], instanceId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.Forbidden));
        }

        await AssertNothingWasTakenAsync(instanceId, quantityWhole: PlentifulQuantity);
    }

    [Test, Description("A Force id that matches nothing is not found, which is what stops an order against a Force that has since been wiped out.")]
    public async Task ShouldRefuseAForceThatDoesNotExistAsync()
    {
        int playerTurnId = await OpenFirstPlayerTurnAsync(_gameId);
        ItIsThisPlayersTurn(playerTurnId);

        int instanceId = await SeedPatchAsync(quantityWhole: PlentifulQuantity);

        ServiceResult<TakeResourcesResult> result = await HandleAsync([999_999], instanceId);

        Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.NotFound));

        await AssertNothingWasTakenAsync(instanceId, quantityWhole: PlentifulQuantity);
    }

    [Test, Description("The strategy's refusal is passed through word for word, so the player reads the same reason the disabled button would have shown.")]
    public async Task ShouldRefuseWithTheStrategysOwnReasonsAsync()
    {
        int playerTurnId = await OpenFirstPlayerTurnAsync(_gameId);
        ItIsThisPlayersTurn(playerTurnId);

        _contextActionService
            .EvaluateActionAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<ForceActionType>(), Arg.Any<CancellationToken>())
            .Returns(ServiceResult<ForceActionAvailability>.Succeed(
                ForceActionAvailability.Blocked(["Settler has already moved this turn. Taking resources costs a full turn."])));

        int instanceId = await SeedPatchAsync(quantityWhole: PlentifulQuantity);
        int forceId = await SeedForceAsync(healthyCount: 10);

        ServiceResult<TakeResourcesResult> result = await HandleAsync([forceId], instanceId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(result.Errors, Has.One.Contains("has already moved this turn"));
        }

        await AssertNothingWasTakenAsync(instanceId, quantityWhole: PlentifulQuantity);
    }

    [Test, Description("A patch this player has not discovered is refused, since the reader is asked again rather than the dialog's view being trusted.")]
    public async Task ShouldRefuseAPatchThePlayerHasNotDiscoveredAsync()
    {
        int playerTurnId = await OpenFirstPlayerTurnAsync(_gameId);
        ItIsThisPlayersTurn(playerTurnId);

        int instanceId = await SeedPatchAsync(quantityWhole: PlentifulQuantity, isDiscovered: false);
        int forceId = await SeedForceAsync(healthyCount: 10);

        ServiceResult<TakeResourcesResult> result = await HandleAsync([forceId], instanceId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(result.Errors, Has.One.Contains("nothing of that resource left to take here"));
        }

        await AssertNothingWasTakenAsync(instanceId, quantityWhole: PlentifulQuantity);
    }

    [Test, Description("A patch on another tile is refused, so a stale instance id from the dialog cannot be taken from across the map.")]
    public async Task ShouldRefuseAPatchOnAnotherTileAsync()
    {
        int playerTurnId = await OpenFirstPlayerTurnAsync(_gameId);
        ItIsThisPlayersTurn(playerTurnId);

        int instanceId = await SeedPatchAsync(quantityWhole: PlentifulQuantity, tileId: _otherTileId);
        int forceId = await SeedForceAsync(healthyCount: 10);

        ServiceResult<TakeResourcesResult> result = await HandleAsync([forceId], instanceId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(result.Errors, Has.One.Contains("nothing of that resource left to take here"));
        }

        await AssertNothingWasTakenAsync(instanceId, quantityWhole: PlentifulQuantity);
    }

    [Test, Description("One resource cannot be taken from two tiles at once, so a selection spanning them is refused rather than having a tile picked for it.")]
    public async Task ShouldRefuseASelectionSpanningTwoTilesAsync()
    {
        int playerTurnId = await OpenFirstPlayerTurnAsync(_gameId);
        ItIsThisPlayersTurn(playerTurnId);

        int instanceId = await SeedPatchAsync(quantityWhole: PlentifulQuantity);

        int hereForceId = await SeedForceAsync(healthyCount: 10);
        int thereForceId = await SeedForceAsync(healthyCount: 10, tileId: _otherTileId);

        ServiceResult<TakeResourcesResult> result = await HandleAsync([hereForceId, thereForceId], instanceId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(result.Errors, Has.One.Contains("not all on the same tile"));
        }

        await AssertNothingWasTakenAsync(instanceId, quantityWhole: PlentifulQuantity);
    }

    [Test, Description("An empty selection is a malformed request, caught by validation rather than reaching the database as an order over nothing.")]
    public async Task ShouldRefuseAnEmptySelectionAsync()
    {
        int playerTurnId = await OpenFirstPlayerTurnAsync(_gameId);
        ItIsThisPlayersTurn(playerTurnId);

        int instanceId = await SeedPatchAsync(quantityWhole: PlentifulQuantity);

        ServiceResult<TakeResourcesResult> result = await HandleAsync([], instanceId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(result.Errors, Has.One.Contains("At least one Force Id is required."));
        }

        await AssertNothingWasTakenAsync(instanceId, quantityWhole: PlentifulQuantity);
    }

    [Test, Description("The same Force listed twice is one Force, or a duplicated id would load it up twice off one patch.")]
    public async Task ShouldTreatARepeatedForceIdAsOneForceAsync()
    {
        int playerTurnId = await OpenFirstPlayerTurnAsync(_gameId);
        ItIsThisPlayersTurn(playerTurnId);

        int instanceId = await SeedPatchAsync(quantityWhole: PlentifulQuantity);
        int forceId = await SeedForceAsync(healthyCount: 10);

        ServiceResult<TakeResourcesResult> result = await HandleAsync([forceId, forceId], instanceId);

        Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.SuccessValue!.UnitsTaken, Is.EqualTo(WholeUnitsFor(10)));
            Assert.That((await context.MapResourceInstances.SingleAsync(instance => instance.Id == instanceId)).Quantity,
                Is.EqualTo(ScaledAmount.FromWhole(PlentifulQuantity - WholeUnitsFor(10)).RawValue));
        }
    }

    #endregion

    #region Fixtures

    /// <summary>Whole units a Force of this many healthy individuals carries off, where the patch holds enough.</summary>
    /// <remarks>
    /// Read off <see cref="TakeResourcesRules"/> rather than written out, since the per-individual rate is tuning standing in for a carry weight that does
    /// not exist yet, and retuning it must not falsify every figure here.
    /// </remarks>
    private static int WholeUnitsFor(int healthyCount)
    {
        return (int)ScaledAmount.FromRaw(TakeResourcesRules.CalculateUnitsSoughtRaw(healthyCount)).ToWholeRoundedDown();
    }

    /// <summary>A patch no selection here can empty, so a test about the write is never also a test about the clamp.</summary>
    private static int PlentifulQuantity => WholeUnitsFor(1_000);

    private void ItIsThisPlayersTurn(int playerTurnId)
    {
        _campaignTurnService
            .GetCurrentCampaignTurnAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(ServiceResult<CurrentCampaignTurn>.Succeed(new CurrentCampaignTurn
            {
                PlayerId = _playerId,
                PlayerTurnId = playerTurnId,
                CurrentRoundNumber = 1,
                FactionName = "Us"
            }));
    }

    private void ItIsTheOtherPlayersTurn(int playerTurnId)
    {
        _campaignTurnService
            .GetCurrentCampaignTurnAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(ServiceResult<CurrentCampaignTurn>.Succeed(new CurrentCampaignTurn
            {
                PlayerId = _otherPlayerId,
                PlayerTurnId = playerTurnId,
                CurrentRoundNumber = 1,
                FactionName = "Them"
            }));
    }

    private async Task<ServiceResult<TakeResourcesResult>> HandleAsync(IReadOnlyCollection<int> forceIds, int mapResourceInstanceId)
    {
        var handler = new TakeResourcesHandler(
            NullLogger<TakeResourcesHandler>.Instance,
            DbFactory,
            _campaignTurnService,
            _contextActionService,
            new TakeableMapResourceReader(NullLogger<TakeableMapResourceReader>.Instance),
            new AssetBankProvisioningService(
                NullLogger<AssetBankProvisioningService>.Instance,
                new AssetNetworkConnectivityService(NullLogger<AssetNetworkConnectivityService>.Instance)),
            new AssetBankCargoService(NullLogger<AssetBankCargoService>.Instance));

        return await handler.Handle(new TakeResourcesRequest(_gameId, _playerId, forceIds, mapResourceInstanceId), CancellationToken.None);
    }

    /// <summary>Asserts the patch is untouched and nothing was loaded, which is what a refusal has to leave behind.</summary>
    private async Task AssertNothingWasTakenAsync(int mapResourceInstanceId, int quantityWhole)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        MapResourceInstance patch = await context.MapResourceInstances.SingleAsync(instance => instance.Id == mapResourceInstanceId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(patch.Quantity, Is.EqualTo(ScaledAmount.FromWhole(quantityWhole).RawValue));
            Assert.That(await context.AssetBanks.AnyAsync(bank => bank.UnitInstanceId != null), Is.False, "No hold is created by a refused order.");
        }
    }

    /// <summary>
    /// The berry patch type every test takes from: gatherable, freely worked, yielding 1.0 Berries per unit.
    /// </summary>
    /// <remarks>
    /// Authored here rather than run through <c>BerriesSeedData</c>, because the seed also drives habitats and instantiation, and these tests place their
    /// own patch exactly where the Forces stand.
    /// </remarks>
    private async Task<int> SeedBerryTypeAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        var category = new MapResourceCategory { Code = "plant-foods", DisplayName = "Plant Foods", SortOrder = 1 };
        context.MapResourceCategories.Add(category);

        await context.SaveChangesAsync();

        var type = new MapResourceType
        {
            Code = "test-berries",
            DisplayName = "Berry Patch",
            MapResourceCategoryId = category.Id,
            SortOrder = 1,
            IconPath = "/map-resources/berries.png",
            IsRenewable = true,
            ExtractionMethod = ExtractionMethod.Gather,
            DiscoveryLikelihood = 1m
        };

        context.MapResourceTypes.Add(type);
        await context.SaveChangesAsync();

        context.MapResourceTypeYields.Add(new MapResourceTypeYield
        {
            MapResourceTypeId = type.Id,
            AssetId = _berriesAssetId,
            AmountPerUnit = _ONE_PER_UNIT,
            IsEnabledByDefault = true
        });

        await context.SaveChangesAsync();

        return type.Id;
    }

    /// <summary>Adds a second yield to the berry patch type, and returns the Asset's id.</summary>
    private async Task<int> AddYieldAsync(string assetCode, long amountPerUnit)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        int assetId = await context.Assets.Where(asset => asset.Code == assetCode).Select(asset => asset.Id).SingleAsync();

        context.MapResourceTypeYields.Add(new MapResourceTypeYield
        {
            MapResourceTypeId = _mapResourceTypeId,
            AssetId = assetId,
            AmountPerUnit = amountPerUnit,
            IsEnabledByDefault = true
        });

        await context.SaveChangesAsync();

        return assetId;
    }

    /// <summary>A patch on the fixture's tile, discovered by the taking player unless told otherwise.</summary>
    private async Task<int> SeedPatchAsync(int quantityWhole, bool isDiscovered = true, int? tileId = null)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        var instance = new MapResourceInstance
        {
            GameId = _gameId,
            CampaignMapId = tileId ?? _tileId,
            MapResourceTypeId = _mapResourceTypeId,
            Slot = MapResourceInstance.MIN_SLOT,
            Quantity = ScaledAmount.FromWhole(quantityWhole).RawValue,
            CreatedOnRound = 1
        };

        context.MapResourceInstances.Add(instance);
        await context.SaveChangesAsync();

        if (isDiscovered)
        {
            context.PlayerKnownMapResources.Add(new PlayerKnownMapResource
            {
                GameId = _gameId,
                PlayerId = _playerId,
                MapResourceInstanceId = instance.Id,
                DiscoveredOnRound = 1
            });

            await context.SaveChangesAsync();
        }

        return instance.Id;
    }

    /// <summary>A settler standing on the fixture's tile holding a full turn's movement, saved and ready to load up.</summary>
    private async Task<int> SeedForceAsync(int healthyCount, int? playerId = null, int injuredCount = 0, int? tileId = null)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        var force = new UnitInstance
        {
            GameId = _gameId,
            PlayerId = playerId ?? _playerId,
            CampaignMapTileId = tileId ?? _tileId,
            UnitTemplateId = _settlerTemplateId,
            InitialStrength = healthyCount + injuredCount,
            HealthyCount = healthyCount,
            InjuredCount = injuredCount,
            CampaignMovementRemaining = _STARTING_MOVEMENT
        };

        context.UnitInstances.Add(force);
        await context.SaveChangesAsync();

        return force.Id;
    }

    /// <summary>Gives a Force its movement back, so a second order can be judged on its own writes rather than on the first one's cost.</summary>
    private async Task RestoreMovementAsync(int forceId)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        UnitInstance force = await context.UnitInstances.SingleAsync(unit => unit.Id == forceId);
        force.CampaignMovementRemaining = _STARTING_MOVEMENT;

        await context.SaveChangesAsync();
    }

    #endregion
}
