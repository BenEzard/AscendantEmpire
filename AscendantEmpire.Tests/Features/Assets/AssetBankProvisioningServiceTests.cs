using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Assets.Services;
using AscendantEmpire.Domain.Entities.Assets;
using AscendantEmpire.Domain.Entities.Settlements;
using AscendantEmpire.Domain.Enums.Settlements;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace AscendantEmpire.Tests.Features.Assets;

/// <summary>
/// Tests for <see cref="AssetBankProvisioningService"/>: which tiers are born with a bank, and what the staged rows look like.
/// </summary>
/// <remarks>
/// <para>
/// In-memory, per <c>testing.md</c>: the service does nothing but one LINQ query and two <c>Add</c>s, and the one rule a database would enforce for it - a
/// settlement cannot hold two memberships - is checked in code here precisely so the refusal is a message rather than a primary key violation.
/// </para>
/// <para>
/// <b>No Game, Structure or Player rows are written.</b> Foreign keys are not enforced in memory and the service reads none of those tables, so the ids on the
/// settlements below are bare numbers - the same shape <see cref="AssetBankTestBase"/> uses.
/// </para>
/// </remarks>
[TestFixture]
internal class AssetBankProvisioningServiceTests : InMemoryTestBase
{
    private const int _GAME_ID = 1;
    private const int _PLAYER_ID = 7;
    private const int _OTHER_PLAYER_ID = 8;

    private AssetBankProvisioningService _service = null!;

    [SetUp]
    public void BuildService()
    {
        _service = new AssetBankProvisioningService(NullLogger<AssetBankProvisioningService>.Instance);
    }

    [Test, Description("A village is born with a bank of its own, owned by the founding player and holding nothing.")]
    public async Task ShouldGiveAVillageAnEmptyBankOfItsOwnAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        Settlement village = await AddSettlementAsync(context, SettlementTier.Village);

        ServiceResultBase result = await _service.ProvisionForNewSettlementAsync(context, _PLAYER_ID, village, CancellationToken.None);
        await context.SaveChangesAsync();

        Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));

        await using AscendantEmpireContext readContext = await DbFactory.CreateDbContextAsync();

        AssetBank bank = await readContext.AssetBanks.SingleAsync();
        AssetBankSettlementMembership membership = await readContext.AssetBankSettlementMemberships.SingleAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(bank.PlayerId, Is.EqualTo(_PLAYER_ID), "The bank belongs to the player who founded the settlement.");
            Assert.That(membership.SettlementId, Is.EqualTo(village.Id), "And the settlement draws on it.");
            Assert.That(membership.AssetBankId, Is.EqualTo(bank.Id));
            Assert.That(await readContext.AssetBankBalances.AnyAsync(), Is.False, "Nothing seeds a starting quantity, so the bank is born empty.");
        }
    }

    [Test, Description("A camp gets no bank at all, because it draws on the village it attaches to rather than banking for itself.")]
    public async Task ShouldGiveACampNoBankAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        Settlement camp = await AddSettlementAsync(context, SettlementTier.Camp);

        ServiceResultBase result = await _service.ProvisionForNewSettlementAsync(context, _PLAYER_ID, camp, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));
            Assert.That(context.ChangeTracker.Entries<AssetBank>(), Is.Empty, "Nothing is even staged, so a settle path founding a camp has nothing to undo.");
            Assert.That(context.ChangeTracker.Entries<AssetBankSettlementMembership>(), Is.Empty);
        }
    }

    [TestCase(SettlementTier.Village, Description = "A village is the lowest tier that banks for itself.")]
    [TestCase(SettlementTier.Hamlet, Description = "A hamlet keeps the bank a village had, so the rule is a threshold rather than a single tier.")]
    [TestCase(SettlementTier.Town, Description = "A town banks for itself, so growth never leaves a settlement holding nothing.")]
    [TestCase(SettlementTier.City, Description = "A city banks for itself.")]
    [TestCase(SettlementTier.MajorCity, Description = "The top tier banks for itself, so no tier above the threshold is overlooked.")]
    public async Task ShouldBankForEveryTierFromVillageUpAsync(SettlementTier tier)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        Settlement settlement = await AddSettlementAsync(context, tier);

        ServiceResultBase result = await _service.ProvisionForNewSettlementAsync(context, _PLAYER_ID, settlement, CancellationToken.None);
        await context.SaveChangesAsync();

        Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));

        await using AscendantEmpireContext readContext = await DbFactory.CreateDbContextAsync();

        Assert.That(await readContext.AssetBankSettlementMemberships.AnyAsync(m => m.SettlementId == settlement.Id), Is.True);
    }

    [Test, Description("The membership picks up an unsaved settlement's generated key, which is what lets a founding stage the settlement and its bank and commit them together.")]
    public async Task ShouldJoinTheMembershipToASettlementThatIsNotSavedYetAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        // Added but deliberately not saved, exactly as the settle handler leaves it: the settlement has no id to point a membership at yet.
        Settlement village = BuildSettlement(SettlementTier.Village);
        context.Settlements.Add(village);

        ServiceResultBase result = await _service.ProvisionForNewSettlementAsync(context, _PLAYER_ID, village, CancellationToken.None);

        Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));

        // One save for both, which is the point of the exercise.
        await context.SaveChangesAsync();

        await using AscendantEmpireContext readContext = await DbFactory.CreateDbContextAsync();

        AssetBankSettlementMembership membership = await readContext.AssetBankSettlementMemberships.SingleAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(village.Id, Is.Not.Zero, "The settlement was given its key by the save.");
            Assert.That(membership.SettlementId, Is.EqualTo(village.Id), "And the membership carries that same key rather than a zero.");
        }
    }

    [Test, Description("Nothing is written until the caller saves, so a founding that rolls back takes the bank with it.")]
    public async Task ShouldStageTheBankWithoutSavingItAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        Settlement village = await AddSettlementAsync(context, SettlementTier.Village);

        ServiceResultBase result = await _service.ProvisionForNewSettlementAsync(context, _PLAYER_ID, village, CancellationToken.None);

        Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));

        await using AscendantEmpireContext readContext = await DbFactory.CreateDbContextAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await readContext.AssetBanks.AnyAsync(), Is.False, "The bank is staged on the caller's context, not written.");
            Assert.That(context.ChangeTracker.Entries<AssetBank>().Count(), Is.EqualTo(1), "But it is staged.");
        }
    }

    [Test, Description("A settlement already drawing on a bank is refused rather than given a second one, so a merge mistaken for a founding says so instead of failing on a key violation.")]
    public async Task ShouldRefuseASettlementThatAlreadyDrawsOnABankAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        Settlement village = await AddSettlementAsync(context, SettlementTier.Village);

        var existingBank = new AssetBank
        {
            PlayerId = _OTHER_PLAYER_ID
        };

        context.AssetBanks.Add(existingBank);
        context.AssetBankSettlementMemberships.Add(new AssetBankSettlementMembership
        {
            Bank = existingBank,
            SettlementId = village.Id
        });

        await context.SaveChangesAsync();

        ServiceResultBase result = await _service.ProvisionForNewSettlementAsync(context, _PLAYER_ID, village, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.InternalServerError), "A caller's mistake about what it is doing, not a player's.");
            Assert.That(result.Errors, Has.One.Contains($"Asset Bank Id {existingBank.Id}"), "The message names the bank it already draws on.");
            Assert.That(context.ChangeTracker.Entries<AssetBank>().Count(), Is.EqualTo(1), "And no second bank is staged.");
        }
    }

    /// <summary>A settlement of the given tier, saved, so it has the id a membership would be written against.</summary>
    private static async Task<Settlement> AddSettlementAsync(AscendantEmpireContext context, SettlementTier tier)
    {
        Settlement settlement = BuildSettlement(tier);
        context.Settlements.Add(settlement);

        await context.SaveChangesAsync();

        return settlement;
    }

    /// <summary>An unsaved settlement of the given tier, with the founding stamps the service never reads.</summary>
    private static Settlement BuildSettlement(SettlementTier tier)
    {
        return new Settlement
        {
            GameId = _GAME_ID,
            StructureInstanceId = 1,
            FoundingPlayerId = _PLAYER_ID,
            Name = tier == SettlementTier.Camp ? null : $"{tier}hold",
            Tier = tier,
            Population = 100,
            FoundedOnRoundNumber = 1,
            FoundedOnTurnId = 1
        };
    }
}
