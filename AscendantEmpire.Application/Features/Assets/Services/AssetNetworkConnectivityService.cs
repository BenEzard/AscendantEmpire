using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Assets.Classes;
using AscendantEmpire.Domain.Entities.Assets;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.Assets.Services;

/// <inheritdoc cref="IAssetNetworkConnectivityService" />
/// <remarks>
/// <para>
/// <b>A camp and its parent settlement are the only thing that networks two banks today.</b> Nothing else joins two Settlements - there is no road at all, and the river
/// and coastline walks are CM-64, RS-48 and RS-49 - so every other bank is a network of one. The re-key and the consistency check both read that single rule from
/// <see cref="BuildNetworks"/>, so the three transport tasks have one place to widen and the two can never disagree about what a fault is.
/// </para>
/// <para>
/// <b>The camp's designation is not read as ownership.</b> A parent the Player holds no bank for is not in the layout at all, so a camp whose parent has been conquered
/// falls out into a network of its own and its deliveries simply stop - see <c>Settlement.ParentSettlementId</c>, which never moves through any of this.
/// </para>
/// </remarks>
internal sealed class AssetNetworkConnectivityService(ILogger<AssetNetworkConnectivityService> logger) : IAssetNetworkConnectivityService
{
    public void AssignKeyToNewBank(AssetBank bank)
    {
        AssignFreshKey(bank);
    }

    public void AssignKeyToCapturedBank(AssetBank bank)
    {
        AssignFreshKey(bank);
    }

    public async Task<ServiceResultBase> RecomputeForPlayerAsync(AscendantEmpireContext context, int playerId, CancellationToken cancelToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Tracked, because these are the rows being written and the caller's SaveChangesAsync is what commits them.
        // Ordered by id so that the bank keeping a shared key is always the same one, and a second run has nothing left to change.
        List<AssetBank> banks = await context.AssetBanks
            .Where(bank => bank.PlayerId == playerId)
            .OrderBy(bank => bank.Id)
            .ToListAsync(cancelToken);

        List<BankRow> rows = await AddParentDesignationsAsync(
            context,
            [.. banks.Select(bank => new BankRow(bank.Id, bank.SettlementId, bank.NetworkKey, null))],
            cancelToken);

        int reKeyedCount = 0;

        foreach (NetworkGroup network in BuildNetworks(rows))
        {
            foreach (int rowIndex in network.RowIndexes)
            {
                AssetBank bank = banks[rowIndex];

                if (bank.NetworkKey != network.NetworkKey)
                {
                    bank.NetworkKey = network.NetworkKey;
                    reKeyedCount++;
                }
            }
        }

        if (reKeyedCount > 0)
        {
            logger.LogInformation("Re-keyed {ReKeyedCount} of Player Id {PlayerId}'s {BankCount} asset banks.", reKeyedCount, playerId, banks.Count);
        }

        return Succeed();
    }

    public async Task<ServiceResult<IReadOnlyCollection<AssetNetworkFault>>> FindFaultsForPlayerAsync(
        AscendantEmpireContext context,
        int playerId,
        CancellationToken cancelToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        List<BankRow> bankRows = await context.AssetBanks
            .AsNoTracking()
            .Where(bank => bank.PlayerId == playerId)
            .OrderBy(bank => bank.Id)
            .Select(bank => new BankRow(bank.Id, bank.SettlementId, bank.NetworkKey, null))
            .ToListAsync(cancelToken);

        List<BankRow> rows = await AddParentDesignationsAsync(context, bankRows, cancelToken);
        List<NetworkGroup> networks = BuildNetworks(rows);

        // Which network each bank belongs to, so a bank carrying somebody else's key can be told apart from one that has simply not joined its own.
        var networkIndexByRowIndex = new Dictionary<int, int>(rows.Count);

        for (int networkIndex = 0; networkIndex < networks.Count; networkIndex++)
        {
            foreach (int rowIndex in networks[networkIndex].RowIndexes)
            {
                networkIndexByRowIndex[rowIndex] = networkIndex;
            }
        }

        var faults = new List<AssetNetworkFault>();

        for (int networkIndex = 0; networkIndex < networks.Count; networkIndex++)
        {
            foreach (int rowIndex in networks[networkIndex].RowIndexes)
            {
                BankRow row = rows[rowIndex];

                if (row.NetworkKey == networks[networkIndex].NetworkKey)
                {
                    continue;
                }

                faults.Add(new AssetNetworkFault(row.AssetBankId, row.SettlementId, DescribeFault(rows, networkIndexByRowIndex, rowIndex, networkIndex)));
            }
        }

        return ServiceResult<IReadOnlyCollection<AssetNetworkFault>>.Succeed(faults);
    }

    /// <summary>
    /// Fills in each bank's parent designation, which is the one thing besides the key that decides what network it belongs to.
    /// </summary>
    /// <remarks>
    /// A second query rather than a projection through <c>AssetBank.Settlement</c>: that foreign key is required, so the join would be an inner one and a bank whose
    /// Settlement row cannot be read would vanish from the layout instead of being reported as the fault it is.
    /// </remarks>
    private static async Task<List<BankRow>> AddParentDesignationsAsync(AscendantEmpireContext context, List<BankRow> bankRows, CancellationToken cancelToken)
    {
        if (bankRows.Count == 0)
        {
            return bankRows;
        }

        int[] settlementIds = [.. bankRows.Select(row => row.SettlementId)];

        List<ParentRow> parentRows = await context.Settlements
            .AsNoTracking()
            .Where(settlement => settlementIds.Contains(settlement.Id) && settlement.ParentSettlementId != null)
            .Select(settlement => new ParentRow(settlement.Id, settlement.ParentSettlementId!.Value))
            .ToListAsync(cancelToken);

        if (parentRows.Count == 0)
        {
            return bankRows;
        }

        Dictionary<int, int> parentBySettlementId = parentRows.ToDictionary(row => row.SettlementId, row => row.ParentSettlementId);

        return
        [
            .. bankRows.Select(row => row with
            {
                ParentSettlementId = parentBySettlementId.TryGetValue(row.SettlementId, out int parentSettlementId) ? parentSettlementId : null
            })
        ];
    }

    /// <summary>
    /// Groups a Player's banks into the networks they should be in, and gives each network the key it should carry.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The single place the connectivity rule lives, read by the re-key and by the consistency check alike. Today that rule is: a bank shares a network with its parent
    /// settlement's bank and with nothing else. CM-64, RS-48 and RS-49 each widen it by adding edges here.
    /// </para>
    /// <para>
    /// <b>Deterministic.</b> <paramref name="rows"/> arrives in ascending bank id order, so a network's first row is its lowest bank id, and that row's existing key is
    /// the one the whole network keeps. The same empire therefore always comes out with the same keys, which is what makes a second run change nothing.
    /// </para>
    /// </remarks>
    private static List<NetworkGroup> BuildNetworks(IReadOnlyList<BankRow> rows)
    {
        int[] componentOf = new int[rows.Count];
        var rowIndexBySettlementId = new Dictionary<int, int>(rows.Count);

        for (int rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            componentOf[rowIndex] = rowIndex;
            rowIndexBySettlementId[rows[rowIndex].SettlementId] = rowIndex;
        }

        // A camp joins its parent settlement's network, which is what lets it spend from the parent's stock and is half the gate the per-Turn delivery runs behind. Only where
        // the parent has a bank in this set: connectivity is assessed one Player at a time, so a parent somebody else now holds joins nothing.
        for (int rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            if (rows[rowIndex].ParentSettlementId is not int parentSettlementId)
            {
                continue;
            }

            if (rowIndexBySettlementId.TryGetValue(parentSettlementId, out int parentRowIndex))
            {
                Join(componentOf, rowIndex, parentRowIndex);
            }
        }

        var networks = new List<NetworkGroup>();
        var networkIndexByComponent = new Dictionary<int, int>();
        var keysInUse = new HashSet<Guid>();

        for (int rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            int component = FindComponent(componentOf, rowIndex);

            if (networkIndexByComponent.TryGetValue(component, out int networkIndex) == false)
            {
                Guid networkKey = rows[rowIndex].NetworkKey;

                // Guid.Empty is the absence of a key rather than a network two banks share, and a key another network has already taken cannot be shared across both.
                if (networkKey == Guid.Empty || keysInUse.Add(networkKey) == false)
                {
                    networkKey = Guid.NewGuid();
                    keysInUse.Add(networkKey);
                }

                networkIndex = networks.Count;
                networks.Add(new NetworkGroup(networkKey, []));
                networkIndexByComponent[component] = networkIndex;
            }

            networks[networkIndex].RowIndexes.Add(rowIndex);
        }

        return networks;
    }

    /// <summary>
    /// Names how one bank's key disagrees with the network it belongs to.
    /// </summary>
    /// <remarks>
    /// The caller has already established that it disagrees. Carrying a key some other network also carries is the dangerous case, because that is the one where two
    /// sets of Settlements can reach each other; being alone when it should be grouped only costs the Player reach.
    /// </remarks>
    private static AssetNetworkFaultKind DescribeFault(
        IReadOnlyList<BankRow> rows,
        Dictionary<int, int> networkIndexByRowIndex,
        int rowIndex,
        int networkIndex)
    {
        Guid networkKey = rows[rowIndex].NetworkKey;

        if (networkKey == Guid.Empty)
        {
            return AssetNetworkFaultKind.Unkeyed;
        }

        bool keyIsHeldElsewhere = Enumerable
            .Range(0, rows.Count)
            .Any(otherRowIndex => rows[otherRowIndex].NetworkKey == networkKey && networkIndexByRowIndex[otherRowIndex] != networkIndex);

        return keyIsHeldElsewhere ? AssetNetworkFaultKind.SharedKey : AssetNetworkFaultKind.Unjoined;
    }

    /// <summary>Puts two rows in one network.</summary>
    /// <remarks>The lower root wins, so a network's representative is always its lowest row, and therefore its lowest bank id.</remarks>
    private static void Join(int[] componentOf, int first, int second)
    {
        int firstRoot = FindComponent(componentOf, first);
        int secondRoot = FindComponent(componentOf, second);

        if (firstRoot == secondRoot)
        {
            return;
        }

        if (firstRoot < secondRoot)
        {
            componentOf[secondRoot] = firstRoot;
        }
        else
        {
            componentOf[firstRoot] = secondRoot;
        }
    }

    /// <summary>Which network a row has ended up in, following its chain of joins to the root.</summary>
    private static int FindComponent(int[] componentOf, int rowIndex)
    {
        while (componentOf[rowIndex] != rowIndex)
        {
            componentOf[rowIndex] = componentOf[componentOf[rowIndex]];
            rowIndex = componentOf[rowIndex];
        }

        return rowIndex;
    }

    /// <summary>
    /// Puts a bank into a network of its own.
    /// </summary>
    /// <remarks>
    /// One implementation behind the two public entry points, which differ in why they are called rather than in what they do: a founding has no network to
    /// join yet, and a conquest must not keep the one it was taken out of.
    /// </remarks>
    private static void AssignFreshKey(AssetBank bank)
    {
        ArgumentNullException.ThrowIfNull(bank);

        bank.NetworkKey = Guid.NewGuid();
    }

    /// <summary>One bank's connectivity inputs: what it is keyed to now, and which Settlement it feeds.</summary>
    private sealed record BankRow(int AssetBankId, int SettlementId, Guid NetworkKey, int? ParentSettlementId);

    /// <summary>One Settlement's parent designation, as read off the Settlement rows.</summary>
    private sealed record ParentRow(int SettlementId, int ParentSettlementId);

    /// <summary>One network: the key its banks should carry, and which rows of the layout belong to it, in ascending bank id order.</summary>
    private sealed record NetworkGroup(Guid NetworkKey, List<int> RowIndexes);
}
