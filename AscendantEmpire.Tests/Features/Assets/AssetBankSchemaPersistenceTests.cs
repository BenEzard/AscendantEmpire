using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Game.Handlers;
using AscendantEmpire.Domain.Entities.Assets;
using AscendantEmpire.Domain.Enums.Assets;

using Microsoft.EntityFrameworkCore;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Tests.Features.Assets;

/// <summary>
/// Tests what the <c>AssetBank</c> table itself refuses: a bank whose Kind disagrees with what it points at, and a second bank for one Unit.
/// </summary>
/// <remarks>
/// <para>
/// On the containerised tier because every rule here is a database rule and nothing else enforces any of them. The in-memory provider has no check
/// constraints and ignores unique indexes, so these cases would pass there whatever the schema said - and what they guard against is the one thing the
/// discriminator exists to prevent: a bank reachable by one set of queries and invisible to the other.
/// </para>
/// <para>
/// Games are built by the real <c>GameSetupService</c>, which fields the starting Units a cargo bank is keyed on. It founds no Settlement, so the cases that
/// name one use an id no row has: the check constraint bites before any foreign key is consulted.
/// </para>
/// </remarks>
[TestFixture]
internal class AssetBankSchemaPersistenceTests : GameSqlServerTestBase
{
    private const int _MAP_WIDTH = 4;
    private const int _MAP_HEIGHT = 4;

    /// <summary>A Settlement id no row has, for the cases the check constraint refuses before the foreign key is reached.</summary>
    private const int _ANY_SETTLEMENT_ID = 999_999;

    private int _playerId;
    private int _unitInstanceId;
    private int _otherUnitInstanceId;

    [SetUp]
    public async Task SetUpGameAsync()
    {
        await SeedGameAsync(mapWidth: _MAP_WIDTH, mapHeight: _MAP_HEIGHT, numberOfPlayers: 2);

        ServiceResult<int> setupResult = await _service.SetupGameAsync(
            new SetupGameRequest(GalleryMapId: _galleryIndexId, GameName: "Asset Bank Schema Game", NumberOfPlayers: 2, BattleMapSize: 50, GenerationSeed: null),
            CancellationToken.None);

        Assert.That(setupResult.HasSucceeded, Is.True, () => string.Join("; ", setupResult.Errors));

        int gameId = setupResult.SuccessValue;

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        _playerId = await context.Players
            .Where(player => player.GameId == gameId)
            .OrderBy(player => player.Id)
            .Select(player => player.Id)
            .FirstAsync();

        int[] unitIds = [.. await context.UnitInstances
            .Where(unit => unit.PlayerId == _playerId)
            .OrderBy(unit => unit.Id)
            .Select(unit => unit.Id)
            .Take(2)
            .ToListAsync()];

        Assert.That(unitIds, Has.Length.EqualTo(2), "The cases need two fielded Units: one to key a bank on, and one to prove the index is per Unit.");

        _unitInstanceId = unitIds[0];
        _otherUnitInstanceId = unitIds[1];
    }

    #region The Kind and what it points at

    [Test, Description("A bank claiming to be a Settlement's while naming a Unit is refused, so cargo cannot be filed where the Empire-wide reads would count it.")]
    public async Task ShouldRejectASettlementBankNamingAUnitAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        context.AssetBanks.Add(BuildBank(AssetBankKind.Settlement, settlementId: null, unitInstanceId: _unitInstanceId));

        AssertRefusedByTheXorConstraint(context);
    }

    [Test, Description("A bank claiming to be a Unit's while naming a Settlement is refused, which is the same mistake the other way about and would hide a stockpile from every total.")]
    public async Task ShouldRejectACargoBankNamingASettlementAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        context.AssetBanks.Add(BuildBank(AssetBankKind.Unit, settlementId: _ANY_SETTLEMENT_ID, unitInstanceId: null));

        AssertRefusedByTheXorConstraint(context);
    }

    [Test, Description("A bank naming both a Settlement and a Unit is refused, because one bank is one thing's store and no reader could say whose.")]
    public async Task ShouldRejectABankNamingBothASettlementAndAUnitAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        context.AssetBanks.Add(BuildBank(AssetBankKind.Unit, settlementId: _ANY_SETTLEMENT_ID, unitInstanceId: _unitInstanceId));

        AssertRefusedByTheXorConstraint(context);
    }

    [Test, Description("A bank naming neither is refused, so a row cannot exist as nobody's store while still being owned and still holding balances.")]
    public async Task ShouldRejectABankNamingNeitherASettlementNorAUnitAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        context.AssetBanks.Add(BuildBank(AssetBankKind.Unit, settlementId: null, unitInstanceId: null));

        AssertRefusedByTheXorConstraint(context);
    }

    #endregion

    #region One bank per Unit

    [Test, Description("A Unit cannot be given a second cargo bank, which is what makes 'one load per Unit' a schema rule rather than one the provisioning service has to remember.")]
    public async Task ShouldRejectASecondCargoBankForOneUnitAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        context.AssetBanks.Add(BuildBank(AssetBankKind.Unit, settlementId: null, unitInstanceId: _unitInstanceId));
        await context.SaveChangesAsync();

        context.AssetBanks.Add(BuildBank(AssetBankKind.Unit, settlementId: null, unitInstanceId: _unitInstanceId));

        DbUpdateException? exception = Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());

        Assert.That(exception!.InnerException?.Message, Does.Contain("UX_AssetBank_UnitInstance"));
    }

    [Test, Description("Two Units each get their own cargo bank, so the filtered index pins one per Unit rather than one in the whole database.")]
    public async Task ShouldAllowACargoBankForEachUnitAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        context.AssetBanks.Add(BuildBank(AssetBankKind.Unit, settlementId: null, unitInstanceId: _unitInstanceId));
        context.AssetBanks.Add(BuildBank(AssetBankKind.Unit, settlementId: null, unitInstanceId: _otherUnitInstanceId));

        await context.SaveChangesAsync();

        await using AscendantEmpireContext readContext = await DbFactory.CreateDbContextAsync();

        Assert.That(await readContext.AssetBanks.CountAsync(bank => bank.Kind == AssetBankKind.Unit), Is.EqualTo(2));
    }

    #endregion

    #region Helpers

    /// <summary>A bank of the given shape, owned and keyed, so that nothing but the rule under test can refuse it.</summary>
    private AssetBank BuildBank(AssetBankKind kind, int? settlementId, int? unitInstanceId)
    {
        return new AssetBank
        {
            PlayerId = _playerId,
            Kind = kind,
            SettlementId = settlementId,
            UnitInstanceId = unitInstanceId,
            NetworkKey = Guid.NewGuid()
        };
    }

    private static void AssertRefusedByTheXorConstraint(AscendantEmpireContext context)
    {
        DbUpdateException? exception = Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());

        Assert.That(exception!.InnerException?.Message, Does.Contain("CK_AssetBank_KindTargetXor"));
    }

    #endregion
}
