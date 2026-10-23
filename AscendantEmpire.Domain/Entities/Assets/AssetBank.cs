using AscendantEmpire.Domain.Entities.Games;

namespace AscendantEmpire.Domain.Entities.Assets;

/// <summary>
/// One pooled store of Assets belonging to a Player, shared by every Settlement whose membership points at it.
/// </summary>
/// <remarks>
/// <para>
/// <b>One bank, many settlements</b> - the bank is the pool and <see cref="AssetBankSettlementMembership"/> is what joins a Settlement to it, so connecting
/// two settlements by road moves a membership and merging two banks is folding one's balances into the other and deleting it. There is deliberately no
/// second "bank group" table: a group would be a permanent row standing for a relationship that only ever lasts as long as the connection does.
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
    /// Connectivity is assessed per Player, so two neighbouring settlements of different Players never share a bank however well they are joined by road.
    /// </remarks>
    public int PlayerId { get; set; }

    /// <summary>
    /// A token shared by the banks of one connected network, so a regrouping can find its siblings without walking the road graph again.
    /// </summary>
    /// <remarks>
    /// Carried over from the original sketch and not yet read by anything. The connectivity work decides whether it is needed at all or whether the bank id
    /// alone is enough, so it is left in place rather than guessed at here.
    /// </remarks>
    public Guid NetworkKey { get; set; } = Guid.NewGuid();

    public ICollection<AssetBankBalance> Balances { get; set; } = [];

    public ICollection<AssetBankSettlementMembership> SettlementMemberships { get; set; } = [];

    public Player Player { get; set; } = null!;
}
