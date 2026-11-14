using AscendantEmpire.Application.Data;

namespace AscendantEmpire.Application.Features.Maps.Depletion.Services;

/// <summary>
/// Reads what a <c>SupplySet</c> holds: which Assets satisfy it, and what one unit of the need costs in each.
/// </summary>
/// <remarks>
/// <para>
/// Membership is what decides whether a thing on a tile is food at all, so this is read once per feed and shared by every tile in it.
/// </para>
/// <para>
/// Reads through the caller's context.
/// </para>
/// </remarks>
internal interface ISupplySetMembershipReader
{
    /// <summary>
    /// What one unit of each set's need costs in each of its Assets, keyed by set id and then by Asset id.
    /// </summary>
    /// <param name="supplySetIds">The sets to read. A set that does not exist comes back as an empty membership rather than being absent.</param>
    Task<IReadOnlyDictionary<int, IReadOnlyDictionary<int, long>>> GetAmountsPerUnitAsync(
        AscendantEmpireContext context,
        IReadOnlyCollection<int> supplySetIds,
        CancellationToken cancelToken = default);
}
