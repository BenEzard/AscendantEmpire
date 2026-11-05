using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Assets.Classes;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.Assets.Services;

/// <summary>
/// How much more of each storage class a Settlement can take, for the credit paths that must not overfill one.
/// </summary>
/// <remarks>
/// <para>
/// <b>Capacity keys on (Settlement, StorageClass).</b> One lookup, with no polymorphic key and no narrowest-match walk, because every Asset carries exactly one
/// required <c>Asset.StorageClassId</c>. It binds per Settlement and not per network: a network is what a Settlement may draw <i>on</i>, not somewhere to put
/// things.
/// </para>
/// <para>
/// <b>Nothing is stored.</b> Capacity is derived on demand from the Settlement's tier and what its bank holds, so there is no capacity table, no row to migrate
/// and no cache to invalidate. See <see cref="SettlementStorageCapacity"/> for the two-level arithmetic.
/// </para>
/// <para>
/// <b>The pre-flight query and the enforcement are the same object.</b> Production asks before it extracts and places afterwards through the same
/// <see cref="SettlementStorageCapacity"/>, so no second code path can answer differently from the one that commits.
/// </para>
/// </remarks>
internal interface IAssetBankCapacityService
{
    /// <summary>
    /// Builds each named Settlement's capacity, keyed by Settlement id.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Works on the caller's context and saves nothing</b>, like the rest of the asset bank services, so a caller can ask and then credit inside one unit of
    /// work.
    /// </para>
    /// </remarks>
    /// <param name="settlementIds">The Settlements to answer for. A Settlement with no row, or of a tier with no authored pool, fails the whole call.</param>
    Task<ServiceResult<IReadOnlyDictionary<int, SettlementStorageCapacity>>> GetSettlementCapacitiesAsync(
        AscendantEmpireContext context,
        IReadOnlyCollection<int> settlementIds,
        CancellationToken cancelToken = default);
}
