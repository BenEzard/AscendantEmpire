using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Assets.Services;
using AscendantEmpire.Domain.Entities.Assets;
using AscendantEmpire.Domain.Entities.Settlements;
using AscendantEmpire.Domain.Entities.Units;
using AscendantEmpire.Domain.Enums.Assets;
using AscendantEmpire.Domain.Enums.Settlements;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Tests.Features.Assets;

/// <summary>
/// Tests for <see cref="AssetBankProvisioningService"/>: that every settlement is born with a bank, and what the staged rows look like.
/// </summary>
/// <remarks>
/// <para>
/// In-memory, per <c>testing.md</c>: the service does nothing but one LINQ query and one <c>Add</c>, and the one rule a database would enforce for it - a
/// settlement cannot have two banks - is checked in code here precisely so the refusal is a message rather than a unique index violation.
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

    /// <summary>A Unit no fixture gives a bank, for the removal that has nothing to remove.</summary>
    private const int _MISSING_UNIT_INSTANCE_ID = 99;

    /// <summary>The catalogue is not seeded here, and nothing these cases assert reads the Asset, so a bare id stands in on a balance row.</summary>
    private const int _ANY_ASSET_ID = 1;

    private AssetBankProvisioningService _service = null!;

    [SetUp]
    public void BuildService()
    {
        _service = new AssetBankProvisioningService(
            NullLogger<AssetBankProvisioningService>.Instance,
            new AssetNetworkConnectivityService(NullLogger<AssetNetworkConnectivityService>.Instance));
    }

    #region Settlement banks

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

        using (Assert.EnterMultipleScope())
        {
            Assert.That(bank.PlayerId, Is.EqualTo(_PLAYER_ID), "The bank belongs to the player who founded the settlement.");
            Assert.That(bank.Kind, Is.EqualTo(AssetBankKind.Settlement), "And it is a stockpile rather than cargo, which is what keeps it in Empire totals.");
            Assert.That(bank.SettlementId, Is.EqualTo(village.Id), "And it is that settlement's own store.");
            Assert.That(bank.UnitInstanceId, Is.Null, "Exactly one of the two references is set.");
            Assert.That(await readContext.AssetBankBalances.AnyAsync(), Is.False, "Nothing seeds a starting quantity, so the bank is born empty.");
        }
    }

    [Test, Description("A provisioned bank arrives keyed, because an unkeyed bank reaches nothing and the connectivity service is the only thing allowed to write the key.")]
    public async Task ShouldStageTheBankAlreadyKeyedAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        Settlement village = await AddSettlementAsync(context, SettlementTier.Village);

        ServiceResultBase result = await _service.ProvisionForNewSettlementAsync(context, _PLAYER_ID, village, CancellationToken.None);
        await context.SaveChangesAsync();

        Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));

        await using AscendantEmpireContext readContext = await DbFactory.CreateDbContextAsync();

        AssetBank bank = await readContext.AssetBanks.SingleAsync();

        Assert.That(bank.NetworkKey, Is.Not.EqualTo(Guid.Empty));
    }

    [Test, Description("A camp is born with a bank of its own, because the first settlement of a game is a camp and the Meeting Place built in it has to have somewhere to put what it produces.")]
    public async Task ShouldGiveACampABankOfItsOwnAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        Settlement camp = await AddSettlementAsync(context, SettlementTier.Camp);

        ServiceResultBase result = await _service.ProvisionForNewSettlementAsync(context, _PLAYER_ID, camp, CancellationToken.None);
        await context.SaveChangesAsync();

        Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));

        await using AscendantEmpireContext readContext = await DbFactory.CreateDbContextAsync();

        AssetBank bank = await readContext.AssetBanks.SingleAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(bank.PlayerId, Is.EqualTo(_PLAYER_ID), "The bank belongs to the player who founded the camp.");
            Assert.That(bank.SettlementId, Is.EqualTo(camp.Id), "And it is the camp's own store, rather than a village's it may not have.");
            Assert.That(await readContext.AssetBankBalances.AnyAsync(), Is.False, "Nothing seeds a starting quantity, so the bank is born empty.");
        }
    }

    [TestCase(SettlementTier.Camp, Description = "A camp banks for itself, and is the tier the first settlement of a game is.")]
    [TestCase(SettlementTier.Village, Description = "A village banks for itself.")]
    [TestCase(SettlementTier.Hamlet, Description = "A hamlet keeps the bank it had lower down, so growth never changes which bank a settlement draws on.")]
    [TestCase(SettlementTier.Town, Description = "A town banks for itself, so growth never leaves a settlement holding nothing.")]
    [TestCase(SettlementTier.City, Description = "A city banks for itself.")]
    [TestCase(SettlementTier.MajorCity, Description = "The top tier banks for itself, so no tier is overlooked.")]
    public async Task ShouldBankForEveryTierAsync(SettlementTier tier)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        Settlement settlement = await AddSettlementAsync(context, tier);

        ServiceResultBase result = await _service.ProvisionForNewSettlementAsync(context, _PLAYER_ID, settlement, CancellationToken.None);
        await context.SaveChangesAsync();

        Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));

        await using AscendantEmpireContext readContext = await DbFactory.CreateDbContextAsync();

        Assert.That(await readContext.AssetBanks.AnyAsync(bank => bank.SettlementId == settlement.Id), Is.True);
    }

    [Test, Description("The bank picks up an unsaved settlement's generated key, which is what lets a founding stage the settlement and its bank and commit them together.")]
    public async Task ShouldJoinTheBankToASettlementThatIsNotSavedYetAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        // Added but deliberately not saved, exactly as the settle handler leaves it: the settlement has no id for a bank to carry yet.
        Settlement village = BuildSettlement(SettlementTier.Village);
        context.Settlements.Add(village);

        ServiceResultBase result = await _service.ProvisionForNewSettlementAsync(context, _PLAYER_ID, village, CancellationToken.None);

        Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));

        // One save for both, which is the point of the exercise.
        await context.SaveChangesAsync();

        await using AscendantEmpireContext readContext = await DbFactory.CreateDbContextAsync();

        AssetBank bank = await readContext.AssetBanks.SingleAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(village.Id, Is.Not.Zero, "The settlement was given its key by the save.");
            Assert.That(bank.SettlementId, Is.EqualTo(village.Id), "And the bank carries that same key rather than a zero.");
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

    [Test, Description("A settlement that already has a bank is refused rather than given a second one, so a re-key mistaken for a founding says so instead of failing on a unique index violation.")]
    public async Task ShouldRefuseASettlementThatAlreadyDrawsOnABankAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        Settlement village = await AddSettlementAsync(context, SettlementTier.Village);

        var existingBank = new AssetBank
        {
            PlayerId = _OTHER_PLAYER_ID,
            Kind = AssetBankKind.Settlement,
            SettlementId = village.Id
        };

        context.AssetBanks.Add(existingBank);

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

    #endregion

    #region Unit cargo banks

    [Test, Description("A Unit's first capture gives it an empty cargo bank of its own, owned by the Unit's player and keyed into a network of one.")]
    public async Task ShouldGiveAUnitAnEmptyCargoBankOfItsOwnAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        UnitInstance unit = await AddUnitAsync(context);

        ServiceResult<AssetBank> result = await _service.ProvisionForUnitAsync(context, unit, CancellationToken.None);
        await context.SaveChangesAsync();

        Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));

        await using AscendantEmpireContext readContext = await DbFactory.CreateDbContextAsync();

        AssetBank bank = await readContext.AssetBanks.SingleAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(bank.PlayerId, Is.EqualTo(_PLAYER_ID), "Cargo belongs to the Unit's player, and a capture moves that column rather than the rows.");
            Assert.That(bank.Kind, Is.EqualTo(AssetBankKind.Unit));
            Assert.That(bank.UnitInstanceId, Is.EqualTo(unit.Id));
            Assert.That(bank.SettlementId, Is.Null, "Exactly one of the two references is set.");
            Assert.That(bank.NetworkKey, Is.Not.EqualTo(Guid.Empty), "Keyed by the connectivity service, like every other bank.");
            Assert.That(await readContext.AssetBankBalances.AnyAsync(), Is.False, "The bank is born empty; the capture that asked for it is what fills it.");
        }
    }

    [Test, Description("A Unit that already has a cargo bank gets that same bank back rather than a second one, because a capture cannot know whether the Unit has carried anything before.")]
    public async Task ShouldReturnTheUnitsExistingCargoBankAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        UnitInstance unit = await AddUnitAsync(context);

        var existingBank = new AssetBank
        {
            PlayerId = _PLAYER_ID,
            Kind = AssetBankKind.Unit,
            UnitInstanceId = unit.Id,
            NetworkKey = Guid.NewGuid()
        };

        context.AssetBanks.Add(existingBank);
        await context.SaveChangesAsync();

        ServiceResult<AssetBank> result = await _service.ProvisionForUnitAsync(context, unit, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));
            Assert.That(result.SuccessValue!.Id, Is.EqualTo(existingBank.Id));
            Assert.That(context.ChangeTracker.Entries<AssetBank>().Count(entry => entry.State == EntityState.Added), Is.Zero, "And nothing is staged.");
        }
    }

    [Test, Description("A bank staged earlier in the same unit of work is found too, since a query cannot see it and a second insert would fail the unique index.")]
    public async Task ShouldReturnACargoBankStagedInTheSameUnitOfWorkAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        UnitInstance unit = await AddUnitAsync(context);

        ServiceResult<AssetBank> first = await _service.ProvisionForUnitAsync(context, unit, CancellationToken.None);
        ServiceResult<AssetBank> second = await _service.ProvisionForUnitAsync(context, unit, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(second.HasSucceeded, Is.True, () => string.Join("; ", second.Errors));
            Assert.That(second.SuccessValue, Is.SameAs(first.SuccessValue!));
            Assert.That(context.ChangeTracker.Entries<AssetBank>().Count(), Is.EqualTo(1));
        }
    }

    [Test, Description("A Unit that has not been saved is refused, because nothing can be carried by a Force that is not fielded and the bank would have nothing to point at.")]
    public async Task ShouldRefuseAUnitThatIsNotSavedYetAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        ServiceResult<AssetBank> result = await _service.ProvisionForUnitAsync(context, BuildUnit(), CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(context.ChangeTracker.Entries<AssetBank>(), Is.Empty);
        }
    }

    [Test, Description("Removing a Unit's cargo stages the bank and every balance in it for deletion, because a load has nowhere to go when its carriers stop existing.")]
    public async Task ShouldRemoveACargoBankAndItsBalancesAsync()
    {
        int unitInstanceId;

        await using (AscendantEmpireContext arrangeContext = await DbFactory.CreateDbContextAsync())
        {
            UnitInstance unit = await AddUnitAsync(arrangeContext);
            unitInstanceId = unit.Id;

            ServiceResult<AssetBank> provisioned = await _service.ProvisionForUnitAsync(arrangeContext, unit, CancellationToken.None);
            await arrangeContext.SaveChangesAsync();

            arrangeContext.AssetBankBalances.Add(new AssetBankBalance
            {
                AssetBankId = provisioned.SuccessValue!.Id,
                AssetId = _ANY_ASSET_ID,
                Quantity = 1_000
            });

            await arrangeContext.SaveChangesAsync();
        }

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        ServiceResultBase result = await _service.RemoveForUnitAsync(context, unitInstanceId, CancellationToken.None);
        await context.SaveChangesAsync();

        await using AscendantEmpireContext readContext = await DbFactory.CreateDbContextAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));
            Assert.That(await readContext.AssetBanks.AnyAsync(), Is.False);
            Assert.That(await readContext.AssetBankBalances.AnyAsync(), Is.False, "A balance left behind would hold a foreign key to a bank that is gone.");
        }
    }

    [Test, Description("Removing the cargo of a Unit that never carried anything succeeds having staged nothing, since most Units never carry anything at all.")]
    public async Task ShouldSucceedRemovingCargoFromAUnitWithNoBankAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        ServiceResultBase result = await _service.RemoveForUnitAsync(context, _MISSING_UNIT_INSTANCE_ID, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));
            Assert.That(context.ChangeTracker.Entries<AssetBank>(), Is.Empty);
        }
    }

    #endregion

    #region Helpers

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

    /// <summary>A fielded Unit, saved, so it has the id a cargo bank is keyed on.</summary>
    private static async Task<UnitInstance> AddUnitAsync(AscendantEmpireContext context)
    {
        UnitInstance unit = BuildUnit();
        context.UnitInstances.Add(unit);

        await context.SaveChangesAsync();

        return unit;
    }

    /// <summary>An unsaved Unit, with the tile and template references the service never reads.</summary>
    private static UnitInstance BuildUnit()
    {
        return new UnitInstance
        {
            GameId = _GAME_ID,
            PlayerId = _PLAYER_ID,
            CampaignMapTileId = 1,
            UnitTemplateId = 1,
            InitialStrength = 100,
            HealthyCount = 100
        };
    }

    #endregion
}
