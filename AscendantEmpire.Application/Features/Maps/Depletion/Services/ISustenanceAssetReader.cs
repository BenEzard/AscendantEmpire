using AscendantEmpire.Application.Data;
using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.Application.Features.Maps.Depletion.Services;

/// <summary>
/// Resolves the Asset each biome sustenance resource is identified as to its database id.
/// </summary>
/// <remarks>
/// <para>
/// The codes themselves are fixed in <see cref="SustenanceResourceAssets"/>; only the ids have to be looked up, and they are the same for every Player.
/// </para>
/// <para>
/// Reads through the caller's context.
/// </para>
/// </remarks>
internal interface ISustenanceAssetReader
{
    /// <summary>
    /// The Asset id each resource is identified as.
    /// </summary>
    /// <returns>
    /// One entry per resource whose Asset exists. A resource whose Asset the catalogue has no row for is left out, and whatever the biome offers of it is
    /// then not food - which is a seeding fault rather than something to refuse a Turn over.
    /// </returns>
    Task<IReadOnlyDictionary<SustenanceResource, int>> GetAssetIdsAsync(AscendantEmpireContext context, CancellationToken cancelToken = default);
}
