using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Assets.Classes;

namespace AscendantEmpire.Application.Features.Assets.Services;

/// <summary>
/// Resolves how much of each Asset a Player currently holds, from whichever store that Asset's scope says the figure lives in.
/// </summary>
/// <remarks>
/// <para>
/// <b>It routes on <c>Asset.StorageScope</c>,</b> the same column <see cref="IAssetBankDrawdownService"/> routes a debit on. A figure the ticker reports and
/// a figure the spender finds then come from the same place by construction, rather than by two lists of Assets staying in step - see
/// <see cref="Domain.Enums.Assets.AssetStorageScope"/>.
/// </para>
/// <para>
/// Its own service rather than part of the read handler because the two jobs are different: that handler is about the shape of the Assets tree, and this is
/// about the numbers hanging off it. It is also the seam <c>GetPlayerAssetsHandler</c>'s remarks name for the day the per-Round refetch costs too much - a
/// slim catalogue call plus a quantities call - so splitting the endpoint later moves no logic.
/// </para>
/// <para>
/// Reads on the caller's context, untracked, and writes nothing. Amounts come back as the whole numbers the UI and the API speak, rounded down, unlike
/// <c>AssetBankHolding.RawQuantity</c>, which stays scaled because a drawdown compares an expense against it.
/// </para>
/// </remarks>
internal interface IPlayerAssetQuantityReader
{
    /// <summary>
    /// Reads the whole amount held of each of the given Assets, keyed by Asset id.
    /// </summary>
    /// <remarks>
    /// <para>
    /// An Asset the Player holds none of is <b>absent</b> rather than present as a zero, so a caller reads a missing entry as "none of it". That is how a
    /// bank already reports an Asset it has never held, and it is what lets an Asset nothing sources yet cost nothing to answer for.
    /// </para>
    /// <para>
    /// Only the stores the requested scopes actually name are read, so a request holding no banked Asset opens no bank query.
    /// </para>
    /// </remarks>
    /// <param name="assets">The Assets to resolve, each carrying the scope that routes it. An empty collection reads nothing and answers nothing.</param>
    Task<IReadOnlyDictionary<int, int>> GetWholeAmountsByAssetIdAsync(
        AscendantEmpireContext context,
        int playerId,
        IReadOnlyCollection<AssetQuantityRequest> assets,
        CancellationToken cancelToken = default);

    /// <summary>
    /// Reads which Assets the Player holds a strategic reserve of, anywhere in their empire.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A set rather than a figure, because the only question anything asks today is whether a reserve exists: that is what the Assets ticker's indicator and
    /// <c>PlayerAsset.HasStrategicReserve</c> report. The amount deliberately does not come back here - it is out of every total until it is released, and
    /// handing it to the ticker alongside the spendable amount is how the two would get added together by mistake.
    /// </para>
    /// <para>
    /// An Asset absent from the set has no reserve. A Player with no settlement owns no banks and so reserves nothing, which comes back as an empty set.
    /// </para>
    /// </remarks>
    Task<IReadOnlySet<int>> GetAssetIdsHeldInReserveAsync(
        AscendantEmpireContext context,
        int playerId,
        CancellationToken cancelToken = default);
}
