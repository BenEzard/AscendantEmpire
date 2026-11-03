namespace AscendantEmpire.BackendContracts.Assets.GetAssetBanks;

/// <summary>
/// Every bank one Settlement can draw on: its own, and any other sharing its <see cref="NetworkKey"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b><see cref="SiteAssetBankId"/> is called out rather than left to be worked out</b> because the site's own bank is not interchangeable with the rest. A
/// settlement expense drains it first and takes only the remainder from the network, so a caller that cannot tell which bank is the site cannot say what a
/// build order will actually spend.
/// </para>
/// <para>
/// A network of one is the normal case and carries the same shape, so a caller has one thing to read whether or not the Settlement is connected to anything.
/// </para>
/// </remarks>
public record AssetNetworkHoldings
{
    /// <summary>
    /// The network every bank here shares. <see cref="System.Guid.Empty"/> when the site's bank has not been keyed yet.
    /// </summary>
    public required Guid NetworkKey { get; init; }

    /// <summary>
    /// The Player who owns every bank in the network, and the only one who may draw on them.
    /// </summary>
    public required int PlayerId { get; init; }

    /// <summary>
    /// The bank of the Settlement that was asked about, which is always one of <see cref="Banks"/>.
    /// </summary>
    public required int SiteAssetBankId { get; init; }

    /// <summary>
    /// Every bank in the network, in ascending bank id order.
    /// </summary>
    public required IReadOnlyCollection<AssetBankHoldings> Banks { get; init; }
}
