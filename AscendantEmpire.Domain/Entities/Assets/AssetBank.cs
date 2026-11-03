using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Entities.Settlements;

namespace AscendantEmpire.Domain.Entities.Assets;

/// <summary>
/// One Settlement's store of Assets, owned by a Player and reachable by every bank sharing its <see cref="NetworkKey"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>One bank, one Settlement, for the Settlement's whole life.</b> <see cref="SettlementId"/> is a unique foreign key and never changes; a conquest moves
/// <see cref="PlayerId"/> instead, so a Settlement's stock travels with the Settlement. Per-Settlement capacity is what forces this shape: a pooled row has
/// no per-Settlement place for a cap to bind, and no way to spread a deposit across the Settlements paying into it.
/// </para>
/// <para>
/// <b>Sharing is the key, not the bank.</b> Banks carrying one <see cref="NetworkKey"/> may draw on each other, so connecting or disconnecting Settlements
/// is a re-key that moves no balance and creates or deletes no bank. There is deliberately no network table: a row standing for a relationship that lasts
/// only as long as the connection does would outlive what it describes.
/// </para>
/// <para>
/// <b>Every bank is a settlement bank.</b> There is no player-level stockpile variant: an Asset that is held per Player rather than per network says so on
/// its own catalogue row, as <c>Asset.StorageScope</c>, because that is a property of the Asset and not of a phase of the game - see
/// <see cref="Enums.Assets.AssetStorageScope"/>.
/// </para>
/// <para>See <c>docs/design/asset-banks.md</c> for the connectivity rules this shape exists to serve.</para>
/// </remarks>
public sealed class AssetBank
{
    public int Id { get; set; }

    /// <summary>
    /// The Player who owns the bank, and the only one who may draw on it.
    /// </summary>
    /// <remarks>
    /// Connectivity is assessed per Player, so two neighbouring settlements of different Players never share a network however well they are joined by road.
    /// This is also the column a conquest moves, which is what hands a captured Settlement's stock to its new owner - see
    /// <c>IAssetBankConquestService</c>, which moves it and then re-keys both empires' networks.
    /// </remarks>
    public int PlayerId { get; set; }

    /// <summary>
    /// The one Settlement whose store this is. Unique, and never changes.
    /// </summary>
    /// <remarks>
    /// The unique index on this column is what makes "a Settlement has exactly one Asset Bank at any time" a schema rule rather than one a service has to
    /// remember to check.
    /// </remarks>
    public int SettlementId { get; set; }

    /// <summary>
    /// The network this bank belongs to. Banks of one Player sharing a key may draw on each other.
    /// </summary>
    /// <remarks>
    /// Derived data with no referential integrity, so a stale key lets the wrong Settlements reach each other and nothing in the schema catches it. That is
    /// why <c>IAssetNetworkConnectivityService</c> is its only writer, and why there is deliberately no default here: a bank arrives as
    /// <see cref="Guid.Empty"/> - unkeyed - and the connectivity service is what keys it.
    /// </remarks>
    public Guid NetworkKey { get; set; }

    public ICollection<AssetBankBalance> Balances { get; set; } = [];

    public Player Player { get; set; } = null!;

    public Settlement Settlement { get; set; } = null!;
}
