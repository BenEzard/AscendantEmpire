using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Entities.Settlements;
using AscendantEmpire.Domain.Entities.Units;
using AscendantEmpire.Domain.Enums.Assets;

namespace AscendantEmpire.Domain.Entities.Assets;

/// <summary>
/// A store of Assets, owned by a Player and reachable by every bank sharing its <see cref="NetworkKey"/>: either one Settlement's stockpile or one Unit's cargo.
/// </summary>
/// <remarks>
/// <para>
/// <b>Two kinds, one table.</b> <see cref="Kind"/> says which, and exactly one of <see cref="SettlementId"/> and <see cref="UnitInstanceId"/> is set to match
/// it - a check constraint rather than a convention, since a bank pointing at both or at neither would be reachable by one set of queries and invisible to the
/// other. A unit bank is cargo: it is deliberately excluded from Empire totals and from Empire-wide spending, which is the whole reason the discriminator is
/// stored - see <see cref="AssetBankKind"/>.
/// </para>
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
/// <b>There is still no player-level bank.</b> An Asset held per Player rather than per network says so on its own catalogue row, as
/// <c>Asset.StorageScope</c>, because that is a property of the Asset and not of a phase of the game - see <see cref="AssetStorageScope"/>. A unit bank is
/// not a variant of that: it has a Force to point at and a location on the map, which is exactly what a player-level bank would have lacked.
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
    /// Whether this is a Settlement's stockpile or a Unit's cargo.
    /// </summary>
    /// <remarks>
    /// The column every Empire-wide read and every Empire-wide expense filters on. See <see cref="AssetBankKind"/> for why it is stored rather than inferred
    /// from which of the two references is set.
    /// </remarks>
    public AssetBankKind Kind { get; set; }

    /// <summary>
    /// The one Settlement whose store this is, or null on a unit bank. Unique, and never changes.
    /// </summary>
    /// <remarks>
    /// The filtered unique index on this column is what makes "a Settlement has exactly one Asset Bank at any time" a schema rule rather than one a service
    /// has to remember to check. Set exactly when <see cref="Kind"/> is <see cref="AssetBankKind.Settlement"/>, which the check constraint enforces.
    /// </remarks>
    public int? SettlementId { get; set; }

    /// <summary>
    /// The one Unit whose cargo this is, or null on a settlement bank. Unique, and never changes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A Unit rather than a Force.</b> Force is a category and not a type - a Force is either a <see cref="UnitInstance"/> or a <c>Command</c>, with no
    /// shared base and no shared table - so there is nothing for a single foreign key to hang on. A Command's pool is the network of its Units' banks, and
    /// Units joining or leaving a Command take their cargo with them without a row moving. <c>ForceOrderParticipant</c> resolves the same dichotomy down to
    /// a Unit the same way.
    /// </para>
    /// <para>
    /// Capacity is also a per-Unit fact - healthy soldiers times the per-soldier carry weight of the template they were built from - so a Command has no
    /// capacity of its own to record here anyway.
    /// </para>
    /// </remarks>
    public int? UnitInstanceId { get; set; }

    /// <summary>
    /// The network this bank belongs to. Banks of one Player sharing a key may draw on each other.
    /// </summary>
    /// <remarks>
    /// Derived data with no referential integrity, so a stale key lets the wrong Settlements reach each other and nothing in the schema catches it. That is
    /// why <c>IAssetNetworkConnectivityService</c> is its only writer, and why there is deliberately no default here: a bank arrives as
    /// <see cref="Guid.Empty"/> - unkeyed - and the connectivity service is what keys it.
    /// <para>
    /// <b>A unit bank is a network of one today.</b> The design pools a Command's Units' banks under one key, but nothing in the codebase forms a Command
    /// yet - no code writes <c>UnitInstance.CommandId</c> - so the recompute leaves unit banks on the key they were born with. The operation that forms a
    /// Command is what will group them.
    /// </para>
    /// </remarks>
    public Guid NetworkKey { get; set; }

    public ICollection<AssetBankBalance> Balances { get; set; } = [];

    public Player Player { get; set; } = null!;

    public Settlement? Settlement { get; set; }

    public UnitInstance? UnitInstance { get; set; }
}
