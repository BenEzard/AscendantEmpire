using AscendantEmpire.Application.Helpers.ScaledNumerics;

namespace AscendantEmpire.Application.Features.Maps.Depletion.Services;

/// <summary>
/// Spreads a tile's pooled food shortfall across the Forces that fed on it.
/// </summary>
/// <remarks>
/// <para>
/// The Forces on a tile feed from one pool, so a shortfall belongs to the tile rather than to whichever Force happened to be processed last. Each Force carries a share in
/// proportion to its troops - which is also its share of the need, at 1 troop = 1 food - so every Force on the tile goes equally short.
/// </para>
/// <para>
/// Split in thousandths by <see cref="ProportionalSplitter"/>, so the shares sum exactly to the shortfall and the result does not depend on the order the Forces are listed in.
/// </para>
/// </remarks>
internal static class ShortfallAllocator
{
    /// <summary>
    /// Each Force's share of <paramref name="shortfall"/>.
    /// </summary>
    /// <param name="shortfall">The tile's pooled shortfall. Must not be negative.</param>
    /// <param name="troopsByForceId">The troops in each Force on the tile, keyed by Unit Instance Id.</param>
    /// <returns>Every Force's share, keyed by Unit Instance Id; 0 for every Force when there is no shortfall.</returns>
    public static Dictionary<int, ScaledAmount> Allocate(ScaledAmount shortfall, IReadOnlyDictionary<int, long> troopsByForceId)
    {
        Dictionary<int, long> sharesRaw = ProportionalSplitter.Split(shortfall.RawValue, troopsByForceId);

        return sharesRaw.ToDictionary(entry => entry.Key, entry => ScaledAmount.FromRaw(entry.Value));
    }
}
