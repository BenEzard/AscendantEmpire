using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Assets.Classes;
using AscendantEmpire.Application.Features.Assets.Services;
using AscendantEmpire.Domain.Entities.Assets;
using AscendantEmpire.Domain.Entities.Units;
using AscendantEmpire.Domain.Enums.Assets;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace AscendantEmpire.Tests.Features.Assets;

/// <summary>
/// Tests for <see cref="AssetBankCargoService"/>: what a credit into a Force's hold stages, and what it refuses.
/// </summary>
/// <remarks>
/// <para>
/// In-memory, per <c>testing.md</c>: the service does one LINQ query, a change-tracker sweep and a handful of <c>Add</c>s. The composite key a second row
/// for the same pair would violate is what the tracker sweep exists to avoid, and that avoidance is observable in memory.
/// </para>
/// <para>
/// <b>No Game, Player or Asset rows are written.</b> Foreign keys are not enforced in memory and the service reads none of those tables, so the ids below
/// are bare numbers.
/// </para>
/// </remarks>
[TestFixture]
internal class AssetBankCargoServiceTests : InMemoryTestBase
{
    private const int _GAME_ID = 1;
    private const int _PLAYER_ID = 7;

    /// <summary>The catalogue is not seeded here, and nothing these cases assert reads the Asset, so bare ids stand in on the balance rows.</summary>
    private const int _BERRIES_ASSET_ID = 1;
    private const int _FODDER_ASSET_ID = 2;

    private AssetBankCargoService _service = null!;

    [SetUp]
    public void BuildService()
    {
        _service = new AssetBankCargoService(NullLogger<AssetBankCargoService>.Instance);
    }

    #region Crediting a saved bank

    [Test, Description("A first take into an empty hold creates the balance row, which is the arrival nothing in the codebase wrote before F-34.")]
    public async Task ShouldCreateABalanceForAnAssetTheHoldDoesNotCarryAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        AssetBank bank = await AddCargoBankAsync(context);

        ServiceResultBase result = await _service.CreditAsync(context, bank, [new CargoCredit(_BERRIES_ASSET_ID, 5_000)], CancellationToken.None);
        await context.SaveChangesAsync();

        Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));

        await using AscendantEmpireContext readContext = await DbFactory.CreateDbContextAsync();

        AssetBankBalance balance = await readContext.AssetBankBalances.SingleAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(balance.AssetBankId, Is.EqualTo(bank.Id));
            Assert.That(balance.AssetId, Is.EqualTo(_BERRIES_ASSET_ID));
            Assert.That(balance.Quantity, Is.EqualTo(5_000));
        }
    }

    [Test, Description("A second take of the same Asset adds to the row the Force already carries, because the key is the bank and the Asset together.")]
    public async Task ShouldTopUpABalanceTheHoldAlreadyCarriesAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        AssetBank bank = await AddCargoBankAsync(context);

        context.AssetBankBalances.Add(new AssetBankBalance { AssetBankId = bank.Id, AssetId = _BERRIES_ASSET_ID, Quantity = 2_000 });
        await context.SaveChangesAsync();

        ServiceResultBase result = await _service.CreditAsync(context, bank, [new CargoCredit(_BERRIES_ASSET_ID, 3_000)], CancellationToken.None);
        await context.SaveChangesAsync();

        Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));

        await using AscendantEmpireContext readContext = await DbFactory.CreateDbContextAsync();

        AssetBankBalance balance = await readContext.AssetBankBalances.SingleAsync();

        Assert.That(balance.Quantity, Is.EqualTo(5_000), "The arrival is added to what was there, not written over it.");
    }

    [Test, Description("Several credits of one Asset are one arrival, so they are pooled rather than racing to create two rows for the same pair.")]
    public async Task ShouldPoolSeveralCreditsOfTheSameAssetAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        AssetBank bank = await AddCargoBankAsync(context);

        ServiceResultBase result = await _service.CreditAsync(
            context,
            bank,
            [new CargoCredit(_BERRIES_ASSET_ID, 1_000), new CargoCredit(_BERRIES_ASSET_ID, 2_500)],
            CancellationToken.None);

        await context.SaveChangesAsync();

        Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));

        await using AscendantEmpireContext readContext = await DbFactory.CreateDbContextAsync();

        AssetBankBalance balance = await readContext.AssetBankBalances.SingleAsync();

        Assert.That(balance.Quantity, Is.EqualTo(3_500));
    }

    [Test, Description("A MapResource with two yields loads both, since a take credits every Asset its type gives rather than picking one.")]
    public async Task ShouldCreditEveryAssetInTheOrderAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        AssetBank bank = await AddCargoBankAsync(context);

        ServiceResultBase result = await _service.CreditAsync(
            context,
            bank,
            [new CargoCredit(_FODDER_ASSET_ID, 400), new CargoCredit(_BERRIES_ASSET_ID, 900)],
            CancellationToken.None);

        await context.SaveChangesAsync();

        Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));

        await using AscendantEmpireContext readContext = await DbFactory.CreateDbContextAsync();

        Dictionary<int, long> quantityByAssetId = await readContext.AssetBankBalances
            .ToDictionaryAsync(balance => balance.AssetId, balance => balance.Quantity);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(quantityByAssetId[_BERRIES_ASSET_ID], Is.EqualTo(900));
            Assert.That(quantityByAssetId[_FODDER_ASSET_ID], Is.EqualTo(400));
        }
    }

    #endregion

    #region Crediting a bank staged in the same unit of work

    [Test, Description("A hold provisioned in the same unit of work has no id yet, so the row must be hung off the navigation or the save would orphan it.")]
    public async Task ShouldCreditABankStagedInTheSameUnitOfWorkAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        UnitInstance unit = await AddUnitAsync(context);

        var bank = new AssetBank
        {
            PlayerId = _PLAYER_ID,
            Kind = AssetBankKind.Unit,
            UnitInstanceId = unit.Id,
            NetworkKey = Guid.NewGuid()
        };

        context.AssetBanks.Add(bank);

        ServiceResultBase result = await _service.CreditAsync(context, bank, [new CargoCredit(_BERRIES_ASSET_ID, 7_000)], CancellationToken.None);
        await context.SaveChangesAsync();

        Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));

        await using AscendantEmpireContext readContext = await DbFactory.CreateDbContextAsync();

        AssetBankBalance balance = await readContext.AssetBankBalances.SingleAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(balance.AssetBankId, Is.EqualTo(bank.Id), "The id EF filled in on save is what the row ends up carrying.");
            Assert.That(balance.Quantity, Is.EqualTo(7_000));
        }
    }

    [Test, Description("Two credits into a brand-new hold must find the row the first one staged, because a second row for the same pair would fail the composite key.")]
    public async Task ShouldTopUpARowStagedByAnEarlierCreditAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        UnitInstance unit = await AddUnitAsync(context);

        var bank = new AssetBank
        {
            PlayerId = _PLAYER_ID,
            Kind = AssetBankKind.Unit,
            UnitInstanceId = unit.Id,
            NetworkKey = Guid.NewGuid()
        };

        context.AssetBanks.Add(bank);

        await _service.CreditAsync(context, bank, [new CargoCredit(_BERRIES_ASSET_ID, 1_000)], CancellationToken.None);
        await _service.CreditAsync(context, bank, [new CargoCredit(_BERRIES_ASSET_ID, 4_000)], CancellationToken.None);

        Assert.That(context.ChangeTracker.Entries<AssetBankBalance>().Count(), Is.EqualTo(1), "One row, topped up, rather than two for the same pair.");

        await context.SaveChangesAsync();

        await using AscendantEmpireContext readContext = await DbFactory.CreateDbContextAsync();

        Assert.That((await readContext.AssetBankBalances.SingleAsync()).Quantity, Is.EqualTo(5_000));
    }

    #endregion

    #region Refusals and no-ops

    [Test, Description("A settlement's stockpile is not a hold, and crediting one here would route around the capacity, levelling and network rules that stockpile owes.")]
    public async Task ShouldRefuseASettlementBankAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        var settlementBank = new AssetBank
        {
            PlayerId = _PLAYER_ID,
            Kind = AssetBankKind.Settlement,
            SettlementId = 1,
            NetworkKey = Guid.NewGuid()
        };

        context.AssetBanks.Add(settlementBank);
        await context.SaveChangesAsync();

        ServiceResultBase result = await _service.CreditAsync(context, settlementBank, [new CargoCredit(_BERRIES_ASSET_ID, 1_000)], CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(context.ChangeTracker.Entries<AssetBankBalance>(), Is.Empty, "And nothing is staged.");
        }
    }

    [Test, Description("A yield whose share rounded away to nothing must leave no row behind, so an empty hold does not fill up with zeroes a player then reads as cargo.")]
    [TestCase(0L, Description = "A share that rounded to nothing.")]
    [TestCase(-500L, Description = "Bad data, which must not be written as a debit.")]
    public async Task ShouldStageNothingForANonPositiveCreditAsync(long rawQuantity)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        AssetBank bank = await AddCargoBankAsync(context);

        ServiceResultBase result = await _service.CreditAsync(context, bank, [new CargoCredit(_BERRIES_ASSET_ID, rawQuantity)], CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));
            Assert.That(context.ChangeTracker.Entries<AssetBankBalance>(), Is.Empty);
        }
    }

    [Test, Description("The handler owns the transaction and the single save, so a credit that saved itself would commit half an order.")]
    public async Task ShouldNotSaveAnythingItselfAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        AssetBank bank = await AddCargoBankAsync(context);

        await _service.CreditAsync(context, bank, [new CargoCredit(_BERRIES_ASSET_ID, 6_000)], CancellationToken.None);

        Assert.That(context.ChangeTracker.Entries<AssetBankBalance>().Count(entry => entry.State == EntityState.Added), Is.EqualTo(1),
            "The row is staged and left staged.");

        await using AscendantEmpireContext readContext = await DbFactory.CreateDbContextAsync();

        Assert.That(await readContext.AssetBankBalances.AnyAsync(), Is.False, "And nothing reached the database.");
    }

    #endregion

    #region Fixtures

    /// <summary>A fielded Force with a saved cargo bank, which is the state every take after the first one starts from.</summary>
    private static async Task<AssetBank> AddCargoBankAsync(AscendantEmpireContext context)
    {
        UnitInstance unit = await AddUnitAsync(context);

        var bank = new AssetBank
        {
            PlayerId = _PLAYER_ID,
            Kind = AssetBankKind.Unit,
            UnitInstanceId = unit.Id,
            NetworkKey = Guid.NewGuid()
        };

        context.AssetBanks.Add(bank);
        await context.SaveChangesAsync();

        return bank;
    }

    /// <summary>A fielded Force, saved, so it has the id a cargo bank is keyed on.</summary>
    private static async Task<UnitInstance> AddUnitAsync(AscendantEmpireContext context)
    {
        var unit = new UnitInstance
        {
            GameId = _GAME_ID,
            PlayerId = _PLAYER_ID,
            CampaignMapTileId = 1,
            UnitTemplateId = 1,
            InitialStrength = 100,
            HealthyCount = 100
        };

        context.UnitInstances.Add(unit);
        await context.SaveChangesAsync();

        return unit;
    }

    #endregion
}
