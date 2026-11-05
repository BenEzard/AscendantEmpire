using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Assets.Handlers;
using AscendantEmpire.Application.Features.DatabaseSeeding.SeedData;
using AscendantEmpire.Application.Helpers.ScaledNumerics;
using AscendantEmpire.Domain.Entities.Assets;
using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Entities.Settlements;
using AscendantEmpire.Domain.Entities.Technologies;
using AscendantEmpire.Domain.Enums.Games;
using AscendantEmpire.Domain.Enums.Settlements;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace AscendantEmpire.Tests.Features.Assets;

/// <summary>
/// A seeded asset catalogue and two Players, for the fixtures that read and spend from asset banks.
/// </summary>
/// <remarks>
/// <para>
/// In-memory: both the reader and the drawdown service stick to LINQ, Add/Remove and SaveChanges, and every rule either enforces is in code rather than in a
/// database constraint. The catalogue is seeded through the real upsert, so the codes, sort orders and storage scopes the fixtures use are production's.
/// </para>
/// <para>
/// <b>No GameIndex rows are written, and a Settlement row only where a fixture asks for one.</b> The bank's Settlement foreign key is not enforced in memory, and
/// neither the reader nor the drawdown reads the Settlement itself - the bank's own PlayerId is what says who may draw on it - so settlement ids are otherwise bare
/// numbers. Connectivity and the delivery do read it, for the camp's parent designation, and call <see cref="CreateSettlementAsync"/> for the rows they need.
/// </para>
/// </remarks>
internal abstract class AssetBankTestBase : InMemoryTestBase
{
    // The Logs group and three of its Assets. Seeded sort orders space by 10 in declaration order, which puts the spend order at Oak, Generic, Cedar.
    protected const string _GROUP_LOGS_CODE = "ConstructionMaterials.Wood.Logs";
    protected const string _OAK_LOG_CODE = "ConstructionMaterials.Wood.Logs.OakLog";
    protected const string _GENERIC_LOG_CODE = "ConstructionMaterials.Wood.Logs.GenericLog";
    protected const string _CEDAR_LOG_CODE = "ConstructionMaterials.Wood.Logs.CedarLog";

    // A second group, used whenever a test needs an Asset the Logs ordering must not reach.
    protected const string _OAK_PLANK_CODE = "ConstructionMaterials.Wood.Planks.OakPlank";

    // The two Assets held per Player rather than in a bank, and one that is derived from what already counts it.
    protected const string _TECHNOLOGY_EXPERIENCE_CODE = "Society.Experience.Technology";
    protected const string _CULTURE_EXPERIENCE_CODE = "Society.Experience.Culture";
    protected const string _CIVILIANS_CODE = "Society.Population.MyPeople.Civilians";

    protected const int _GAME_ID = 1;

    /// <summary>
    /// The Player every test spends and reads as.
    /// </summary>
    protected int _playerId;

    /// <summary>
    /// A second Player in the same Game, so "another Player's bank is not reachable" can be pinned rather than assumed.
    /// </summary>
    protected int _otherPlayerId;

    [SetUp]
    public async Task SeedCatalogueAndPlayersAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        // Every Asset names a storage class against a non-null FK, so the classes have to be committed before the catalogue that resolves against them.
        var storageClasses = new UpsertStorageClassesHandler(NullLogger<UpsertStorageClassesHandler>.Instance, context);
        ServiceResultBase classesSeeded = await storageClasses.Handle(
            new UpsertStorageClassesRequest(StorageClassSeedData.Build()),
            CancellationToken.None);
        Assert.That(classesSeeded.HasSucceeded, Is.True, () => string.Join("; ", classesSeeded.Errors));

        var upsert = new UpsertAssetsHandler(NullLogger<UpsertAssetsHandler>.Instance, context);
        ServiceResultBase seeded = await upsert.Handle(new UpsertAssetsRequest(AssetSeedData.Build()), CancellationToken.None);
        Assert.That(seeded.HasSucceeded, Is.True, () => string.Join("; ", seeded.Errors));

        Player player = BuildPlayer("Tester");
        Player otherPlayer = BuildPlayer("Rival");
        context.Players.AddRange(player, otherPlayer);

        await context.SaveChangesAsync();

        _playerId = player.Id;
        _otherPlayerId = otherPlayer.Id;
    }

    /// <summary>
    /// Creates a bank for a Player, keys it on the named Settlement, and credits the given balances.
    /// </summary>
    /// <remarks>
    /// Balances are given in whole units and scaled on the way in, because that is how a test reads: "4 Oak Logs" rather than 4,000. Pass a fractional
    /// figure through <see cref="ScaledAmount"/> directly where a test needs sub-unit stock.
    /// </remarks>
    /// <param name="balances">Asset code to whole quantity. An Asset left out is one the bank has no row for at all.</param>
    /// <param name="networkKey">
    /// The network to put the bank in, so a fixture can build two banks that can reach each other. Left null, the bank gets a network of its own; pass
    /// <see cref="Guid.Empty"/> for a bank the connectivity service has not keyed yet.
    /// </param>
    protected async Task<int> CreateBankAsync(
        int playerId,
        int settlementId,
        IReadOnlyDictionary<string, long> balances,
        Guid? networkKey = null)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        var bank = new AssetBank
        {
            PlayerId = playerId,
            SettlementId = settlementId,
            NetworkKey = networkKey ?? Guid.NewGuid()
        };

        context.AssetBanks.Add(bank);
        await context.SaveChangesAsync();

        Dictionary<string, int> assetIdsByCode = await ReadAssetIdsAsync(context, [.. balances.Keys]);

        foreach ((string assetCode, long wholeQuantity) in balances)
        {
            context.AssetBankBalances.Add(new AssetBankBalance
            {
                AssetBankId = bank.Id,
                AssetId = assetIdsByCode[assetCode],
                Quantity = ScaledAmount.FromWhole(wholeQuantity).RawValue
            });
        }

        await context.SaveChangesAsync();

        return bank.Id;
    }

    /// <summary>
    /// Writes the Settlement row a bank's <c>SettlementId</c> points at, for the services that read a settlement's tier or its parent designation.
    /// </summary>
    /// <remarks>
    /// The id is set explicitly rather than generated, because a fixture arranges its banks against settlement ids of its own choosing and the two have to agree. The
    /// structure, tile and Turn stamps are bare numbers: nothing that reads these rows looks at them, and no foreign key is enforced in memory.
    /// </remarks>
    /// <param name="parentSettlementId">The Settlement this one delivers its production to, or null for one that feeds nobody.</param>
    protected async Task CreateSettlementAsync(
        int settlementId,
        SettlementTier tier = SettlementTier.Village,
        int? parentSettlementId = null,
        int? foundingPlayerId = null)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        context.Settlements.Add(new Settlement
        {
            Id = settlementId,
            GameId = _GAME_ID,
            StructureInstanceId = settlementId,
            FoundingPlayerId = foundingPlayerId ?? _playerId,
            Name = tier == SettlementTier.Camp ? null : $"Settlement {settlementId}",
            Tier = tier,
            Population = 25,
            FoundedOnRoundNumber = 1,
            FoundedOnTurnId = 1,
            ParentSettlementId = parentSettlementId
        });

        await context.SaveChangesAsync();
    }

    /// <summary>
    /// Writes the Player's research state, which is the player-level store a Technology experience expense is spent out of.
    /// </summary>
    protected async Task CreateResearchStateAsync(int playerId, int unspentDirectedPoints, int unspentDiffusionPoints = 0)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        context.PlayerResearchStates.Add(new PlayerResearchState
        {
            PlayerId = playerId,
            GameId = _GAME_ID,
            UnspentDirectedPoints = unspentDirectedPoints,
            UnspentDiffusionPoints = unspentDiffusionPoints,
            TechPointsPerPopulationSize = 1
        });

        await context.SaveChangesAsync();
    }

    /// <summary>
    /// Looks up one catalogue Asset's id by its code.
    /// </summary>
    protected async Task<int> GetAssetIdAsync(string assetCode)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        Dictionary<string, int> assetIdsByCode = await ReadAssetIdsAsync(context, [assetCode]);

        return assetIdsByCode[assetCode];
    }

    /// <summary>
    /// Looks up one catalogue AssetGroup's id by its code.
    /// </summary>
    protected async Task<int> GetAssetGroupIdAsync(string groupCode)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        int groupId = await context.AssetGroups
            .Where(group => group.Code == groupCode)
            .Select(group => group.Id)
            .SingleAsync();

        return groupId;
    }

    /// <summary>
    /// Reads what one bank now holds, keyed by Asset code, with Assets the bank has no row for left out.
    /// </summary>
    /// <remarks>Whole units, matching how <see cref="CreateBankAsync"/> takes them, so an assertion reads in the same figures the arrangement did.</remarks>
    protected async Task<Dictionary<string, long>> ReadBalancesAsync(int assetBankId)
    {
        Dictionary<string, long> rawBalances = await ReadRawBalancesAsync(assetBankId);

        return rawBalances.ToDictionary(balance => balance.Key, balance => ScaledAmount.FromRaw(balance.Value).ToWholeRoundedDown());
    }

    /// <summary>
    /// Reads what one bank now holds in raw scaled units, keyed by Asset code, with Assets the bank has no row for left out.
    /// </summary>
    /// <remarks>
    /// For the cases whose expected figures are not whole. A levelled Empire-wide spread normally leaves a balance a fraction of a unit off a round number,
    /// and <see cref="ReadBalancesAsync"/> would round that away before the assertion ever saw it.
    /// </remarks>
    protected async Task<Dictionary<string, long>> ReadRawBalancesAsync(int assetBankId)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        // Projected into the query before the dictionary is built. ToDictionaryAsync runs its selectors client-side against materialised entities, where the
        // Asset navigation has not been loaded, so keying straight off balance.Asset.Code would read a null.
        List<BalanceRow> rows = await context.AssetBankBalances
            .Where(balance => balance.AssetBankId == assetBankId)
            .Select(balance => new BalanceRow(balance.Asset.Code, balance.Quantity))
            .ToListAsync();

        return rows.ToDictionary(row => row.AssetCode, row => row.Quantity);
    }

    /// <summary>
    /// Reads the Player's unspent research and diffusion points back out of the store.
    /// </summary>
    protected async Task<(int DirectedPoints, int DiffusionPoints)> ReadResearchPointsAsync(int playerId)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        PlayerResearchState state = await context.PlayerResearchStates.SingleAsync(row => row.PlayerId == playerId);

        return (state.UnspentDirectedPoints, state.UnspentDiffusionPoints);
    }

    private static async Task<Dictionary<string, int>> ReadAssetIdsAsync(AscendantEmpireContext context, IReadOnlyCollection<string> assetCodes)
    {
        Dictionary<string, int> assetIdsByCode = await context.Assets
            .Where(asset => assetCodes.Contains(asset.Code))
            .ToDictionaryAsync(asset => asset.Code, asset => asset.Id);

        string[] missingCodes = [.. assetCodes.Except(assetIdsByCode.Keys)];

        Assert.That(missingCodes, Is.Empty, () => $"the seeded catalogue has no Asset for code(s): {string.Join(", ", missingCodes)}");

        return assetIdsByCode;
    }

    private sealed record BalanceRow(string AssetCode, long Quantity);

    private static Player BuildPlayer(string rulerName)
    {
        return new Player
        {
            GameId = _GAME_ID,
            RulerTitle = Player.DEFAULT_RULER_TITLE,
            RulerName = rulerName,
            FactionName = $"{rulerName}s",
            PeopleName = $"{rulerName} People",
            Demonym = $"{rulerName}ian",
            CultureName = $"{rulerName} Culture",
            Language = $"{rulerName}ish",
            RulerPortraitPath = "/portraits/01_western_king.png",
            Status = PlayerStatus.Active
        };
    }
}
