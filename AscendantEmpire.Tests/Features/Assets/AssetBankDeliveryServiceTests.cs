using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Assets.Classes;
using AscendantEmpire.Application.Features.Assets.Services;
using AscendantEmpire.Domain.Enums.Settlements;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Tests.Features.Assets;

/// <summary>
/// Tests for <see cref="AssetBankDeliveryService"/>: that an eligible camp empties into the settlement it feeds, and that every way of being ineligible leaves
/// its stock exactly where it is.
/// </summary>
/// <remarks>
/// In-memory, per <c>testing.md</c>: the service reads two projections, adds and removes balance rows, and leaves the save to its caller. The composite key it
/// relies on is enforced in code - it looks a parent's row up before inserting one - rather than left to the database to refuse.
/// </remarks>
[TestFixture]
internal class AssetBankDeliveryServiceTests : AssetBankTestBase
{
    private const int _CAMP_SETTLEMENT_ID = 21;
    private const int _SECOND_CAMP_SETTLEMENT_ID = 22;
    private const int _PARENT_SETTLEMENT_ID = 23;

    /// <summary>A Settlement above camp tier that still carries a designation, which is what a camp that has grown would look like.</summary>
    private const int _GROWN_SETTLEMENT_ID = 24;

    private AssetBankDeliveryService _service = null!;

    [SetUp]
    public void BuildService()
    {
        _service = new AssetBankDeliveryService(NullLogger<AssetBankDeliveryService>.Instance);
    }

    [Test, Description("An eligible camp's whole store moves to its parent and the camp is left holding no balance row at all, which is what 'deliver everything, keep nothing' means.")]
    public async Task ShouldMoveEveryBalanceFromACampIntoItsParentSettlement()
    {
        Guid networkKey = Guid.NewGuid();

        await CreateSettlementAsync(_CAMP_SETTLEMENT_ID, SettlementTier.Camp, parentSettlementId: _PARENT_SETTLEMENT_ID);
        await CreateSettlementAsync(_PARENT_SETTLEMENT_ID);

        int campBankId = await CreateBankAsync(_playerId, _CAMP_SETTLEMENT_ID, new Dictionary<string, long>
        {
            [_OAK_LOG_CODE] = 4,
            [_CEDAR_LOG_CODE] = 6
        }, networkKey);

        int parentBankId = await CreateBankAsync(_playerId, _PARENT_SETTLEMENT_ID, new Dictionary<string, long>(), networkKey);

        AssetDeliveryOutcome outcome = await DeliverAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await ReadBalancesAsync(campBankId), Is.Empty);
            Assert.That(await ReadBalancesAsync(parentBankId), Is.EquivalentTo(new Dictionary<string, long>
            {
                [_OAK_LOG_CODE] = 4,
                [_CEDAR_LOG_CODE] = 6
            }));
            Assert.That(outcome, Is.EqualTo(new AssetDeliveryOutcome(1, 2)));
        }
    }

    [Test, Description("A delivery adds to what the parent already holds of that Asset rather than inserting a second row, since the balance's key is the bank and the Asset together.")]
    public async Task ShouldAddToABalanceTheParentAlreadyHolds()
    {
        Guid networkKey = Guid.NewGuid();

        await CreateSettlementAsync(_CAMP_SETTLEMENT_ID, SettlementTier.Camp, parentSettlementId: _PARENT_SETTLEMENT_ID);
        await CreateSettlementAsync(_PARENT_SETTLEMENT_ID);

        await CreateBankAsync(_playerId, _CAMP_SETTLEMENT_ID, new Dictionary<string, long> { [_OAK_LOG_CODE] = 4 }, networkKey);
        int parentBankId = await CreateBankAsync(_playerId, _PARENT_SETTLEMENT_ID, new Dictionary<string, long> { [_OAK_LOG_CODE] = 10 }, networkKey);

        await DeliverAsync();

        Assert.That(await ReadBalancesAsync(parentBankId), Is.EquivalentTo(new Dictionary<string, long> { [_OAK_LOG_CODE] = 14 }));
    }

    [Test, Description("Two camps feeding one parent both empty into it, so a village gathers what every camp around it produced.")]
    public async Task ShouldMergeTwoCampsIntoOneParent()
    {
        Guid networkKey = Guid.NewGuid();

        await CreateSettlementAsync(_CAMP_SETTLEMENT_ID, SettlementTier.Camp, parentSettlementId: _PARENT_SETTLEMENT_ID);
        await CreateSettlementAsync(_SECOND_CAMP_SETTLEMENT_ID, SettlementTier.Camp, parentSettlementId: _PARENT_SETTLEMENT_ID);
        await CreateSettlementAsync(_PARENT_SETTLEMENT_ID);

        await CreateBankAsync(_playerId, _CAMP_SETTLEMENT_ID, new Dictionary<string, long> { [_OAK_LOG_CODE] = 4 }, networkKey);
        await CreateBankAsync(_playerId, _SECOND_CAMP_SETTLEMENT_ID, new Dictionary<string, long> { [_OAK_LOG_CODE] = 7 }, networkKey);
        int parentBankId = await CreateBankAsync(_playerId, _PARENT_SETTLEMENT_ID, new Dictionary<string, long>(), networkKey);

        AssetDeliveryOutcome outcome = await DeliverAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await ReadBalancesAsync(parentBankId), Is.EquivalentTo(new Dictionary<string, long> { [_OAK_LOG_CODE] = 11 }));
            Assert.That(outcome.CampsEmptied, Is.EqualTo(2));
        }
    }

    [Test, Description("A camp cut off from its parent keeps everything it holds, which is the behaviour that makes a severed road harmless rather than lossy.")]
    public async Task ShouldLeaveACampAloneWhenItsNetworkIsSevered()
    {
        await CreateSettlementAsync(_CAMP_SETTLEMENT_ID, SettlementTier.Camp, parentSettlementId: _PARENT_SETTLEMENT_ID);
        await CreateSettlementAsync(_PARENT_SETTLEMENT_ID);

        int campBankId = await CreateBankAsync(_playerId, _CAMP_SETTLEMENT_ID, new Dictionary<string, long> { [_OAK_LOG_CODE] = 4 });
        int parentBankId = await CreateBankAsync(_playerId, _PARENT_SETTLEMENT_ID, new Dictionary<string, long>());

        AssetDeliveryOutcome outcome = await DeliverAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await ReadBalancesAsync(campBankId), Is.EquivalentTo(new Dictionary<string, long> { [_OAK_LOG_CODE] = 4 }));
            Assert.That(await ReadBalancesAsync(parentBankId), Is.Empty);
            Assert.That(outcome, Is.EqualTo(new AssetDeliveryOutcome(0, 0)));
        }
    }

    [Test, Description("The designation survives a severed network, so reconnecting resumes delivery rather than needing the camp to be attached again.")]
    public async Task ShouldLeaveTheDesignationAloneWhenTheNetworkIsSevered()
    {
        await CreateSettlementAsync(_CAMP_SETTLEMENT_ID, SettlementTier.Camp, parentSettlementId: _PARENT_SETTLEMENT_ID);
        await CreateSettlementAsync(_PARENT_SETTLEMENT_ID);

        await CreateBankAsync(_playerId, _CAMP_SETTLEMENT_ID, new Dictionary<string, long> { [_OAK_LOG_CODE] = 4 });
        await CreateBankAsync(_playerId, _PARENT_SETTLEMENT_ID, new Dictionary<string, long>());

        await DeliverAsync();

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        int? parentSettlementId = await context.Settlements
            .Where(settlement => settlement.Id == _CAMP_SETTLEMENT_ID)
            .Select(settlement => settlement.ParentSettlementId)
            .SingleAsync();

        Assert.That(parentSettlementId, Is.EqualTo(_PARENT_SETTLEMENT_ID));
    }

    [Test, Description("A settlement with no parent settlement delivers nothing even inside a shared network, because a village is nobody's camp.")]
    public async Task ShouldLeaveASettlementAloneWhenItHasNoParentSettlement()
    {
        Guid networkKey = Guid.NewGuid();

        await CreateSettlementAsync(_CAMP_SETTLEMENT_ID, SettlementTier.Camp);
        await CreateSettlementAsync(_PARENT_SETTLEMENT_ID);

        int campBankId = await CreateBankAsync(_playerId, _CAMP_SETTLEMENT_ID, new Dictionary<string, long> { [_OAK_LOG_CODE] = 4 }, networkKey);

        int parentBankId = await CreateBankAsync(_playerId, _PARENT_SETTLEMENT_ID, new Dictionary<string, long>(), networkKey);

        AssetDeliveryOutcome outcome = await DeliverAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await ReadBalancesAsync(campBankId), Is.EquivalentTo(new Dictionary<string, long> { [_OAK_LOG_CODE] = 4 }));
            Assert.That(await ReadBalancesAsync(parentBankId), Is.Empty);
            Assert.That(outcome, Is.EqualTo(new AssetDeliveryOutcome(0, 0)));
        }
    }

    [Test, Description("A settlement above camp tier delivers nothing even carrying a designation, so a camp that grows does not keep emptying itself into its old parent.")]
    public async Task ShouldLeaveASettlementAloneWhenItIsNotACamp()
    {
        Guid networkKey = Guid.NewGuid();

        await CreateSettlementAsync(_GROWN_SETTLEMENT_ID, SettlementTier.Village, parentSettlementId: _PARENT_SETTLEMENT_ID);
        await CreateSettlementAsync(_PARENT_SETTLEMENT_ID);

        int grownBankId = await CreateBankAsync(_playerId, _GROWN_SETTLEMENT_ID, new Dictionary<string, long> { [_OAK_LOG_CODE] = 4 }, networkKey);
        int parentBankId = await CreateBankAsync(_playerId, _PARENT_SETTLEMENT_ID, new Dictionary<string, long>(), networkKey);

        AssetDeliveryOutcome outcome = await DeliverAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await ReadBalancesAsync(grownBankId), Is.EquivalentTo(new Dictionary<string, long> { [_OAK_LOG_CODE] = 4 }));
            Assert.That(await ReadBalancesAsync(parentBankId), Is.Empty);
            Assert.That(outcome, Is.EqualTo(new AssetDeliveryOutcome(0, 0)));
        }
    }

    [Test, Description("A camp designated to feed another camp delivers nothing, because emptying the middle of a chain first would destroy the stock behind it.")]
    public async Task ShouldLeaveACampAloneWhenItsParentIsAlsoACamp()
    {
        Guid networkKey = Guid.NewGuid();

        await CreateSettlementAsync(_CAMP_SETTLEMENT_ID, SettlementTier.Camp, parentSettlementId: _SECOND_CAMP_SETTLEMENT_ID);
        await CreateSettlementAsync(_SECOND_CAMP_SETTLEMENT_ID, SettlementTier.Camp);

        int campBankId = await CreateBankAsync(_playerId, _CAMP_SETTLEMENT_ID, new Dictionary<string, long> { [_OAK_LOG_CODE] = 4 }, networkKey);
        int parentCampBankId = await CreateBankAsync(_playerId, _SECOND_CAMP_SETTLEMENT_ID, new Dictionary<string, long>(), networkKey);

        AssetDeliveryOutcome outcome = await DeliverAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await ReadBalancesAsync(campBankId), Is.EquivalentTo(new Dictionary<string, long> { [_OAK_LOG_CODE] = 4 }));
            Assert.That(await ReadBalancesAsync(parentCampBankId), Is.Empty);
            Assert.That(outcome, Is.EqualTo(new AssetDeliveryOutcome(0, 0)));
        }
    }

    [Test, Description("A camp whose parent has been conquered delivers nothing, because connectivity is per Player and the two can no longer share a network.")]
    public async Task ShouldLeaveACampAloneWhenItsParentSettlementBelongsToAnotherPlayer()
    {
        Guid networkKey = Guid.NewGuid();

        await CreateSettlementAsync(_CAMP_SETTLEMENT_ID, SettlementTier.Camp, parentSettlementId: _PARENT_SETTLEMENT_ID);
        await CreateSettlementAsync(_PARENT_SETTLEMENT_ID);

        int campBankId = await CreateBankAsync(_playerId, _CAMP_SETTLEMENT_ID, new Dictionary<string, long> { [_OAK_LOG_CODE] = 4 }, networkKey);
        int parentBankId = await CreateBankAsync(_otherPlayerId, _PARENT_SETTLEMENT_ID, new Dictionary<string, long>(), networkKey);

        AssetDeliveryOutcome outcome = await DeliverAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await ReadBalancesAsync(campBankId), Is.EquivalentTo(new Dictionary<string, long> { [_OAK_LOG_CODE] = 4 }));
            Assert.That(await ReadBalancesAsync(parentBankId), Is.Empty);
            Assert.That(outcome, Is.EqualTo(new AssetDeliveryOutcome(0, 0)));
        }
    }

    [Test, Description("Two unkeyed banks are not connected, because Guid.Empty is the absence of a key rather than a network two banks share.")]
    public async Task ShouldNotTreatTwoUnkeyedBanksAsConnected()
    {
        await CreateSettlementAsync(_CAMP_SETTLEMENT_ID, SettlementTier.Camp, parentSettlementId: _PARENT_SETTLEMENT_ID);
        await CreateSettlementAsync(_PARENT_SETTLEMENT_ID);

        int campBankId = await CreateBankAsync(_playerId, _CAMP_SETTLEMENT_ID, new Dictionary<string, long> { [_OAK_LOG_CODE] = 4 }, Guid.Empty);
        await CreateBankAsync(_playerId, _PARENT_SETTLEMENT_ID, new Dictionary<string, long>(), Guid.Empty);

        AssetDeliveryOutcome outcome = await DeliverAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await ReadBalancesAsync(campBankId), Is.EquivalentTo(new Dictionary<string, long> { [_OAK_LOG_CODE] = 4 }));
            Assert.That(outcome, Is.EqualTo(new AssetDeliveryOutcome(0, 0)));
        }
    }

    [Test, Description("A second run in one Turn moves the same stock once, which is what makes a delivery safe inside a pipeline that has no retry story.")]
    public async Task ShouldMoveTheStockOnceWhenRunTwice()
    {
        Guid networkKey = Guid.NewGuid();

        await CreateSettlementAsync(_CAMP_SETTLEMENT_ID, SettlementTier.Camp, parentSettlementId: _PARENT_SETTLEMENT_ID);
        await CreateSettlementAsync(_PARENT_SETTLEMENT_ID);

        await CreateBankAsync(_playerId, _CAMP_SETTLEMENT_ID, new Dictionary<string, long> { [_OAK_LOG_CODE] = 4 }, networkKey);
        int parentBankId = await CreateBankAsync(_playerId, _PARENT_SETTLEMENT_ID, new Dictionary<string, long>(), networkKey);

        await DeliverAsync();
        AssetDeliveryOutcome secondOutcome = await DeliverAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await ReadBalancesAsync(parentBankId), Is.EquivalentTo(new Dictionary<string, long> { [_OAK_LOG_CODE] = 4 }));
            Assert.That(secondOutcome, Is.EqualTo(new AssetDeliveryOutcome(0, 0)), "The camp has nothing left for the second run to move.");
        }
    }

    /// <summary>Delivers on its own context and saves, which is what the end-of-Turn process does.</summary>
    private async Task<AssetDeliveryOutcome> DeliverAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        ServiceResult<AssetDeliveryOutcome> result = await _service.DeliverCampProductionAsync(context, _GAME_ID, _playerId, CancellationToken.None);

        Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));

        await context.SaveChangesAsync();

        return result.SuccessValue!;
    }
}
