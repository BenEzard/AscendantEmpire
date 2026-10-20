using AscendantEmpire.Application.Features.ForceOrders.Movement;
using AscendantEmpire.Application.Features.Maps.CampaignMaps.Classes;
using AscendantEmpire.BackendContracts.CampaignMaps;

namespace AscendantEmpire.Application.Features.Structures.Classes;

/// <summary>
/// Which tiles a completed Structure asserts over.
/// </summary>
/// <remarks>
/// The ground itself, plus the territory radius of the Settlement it belongs to - a camp owns what it stands on, a village a ring around it. See "Territory" in
/// <c>docs/design/settlements.md</c> and <c>SettlementTierRules</c> for where the radius is authored.
/// </remarks>
internal static class StructureAssertionRules
{
    /// <summary>
    /// Every tile a Structure at <paramref name="location"/> asserts over, its own tile included.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Through <see cref="SightRadius.GetSquare"/>, the same wrap-aware Chebyshev square sight is measured in, which already does all three things a territory radius
    /// needs: it includes the origin, it keeps a folded tile once on a map too small for the square, and it omits tiles off an edge that does not wrap. So a Settlement
    /// in a corner simply gets a smaller territory, with no edge case written anywhere.
    /// </para>
    /// <para>
    /// At radius 1 this is exactly the tile plus the eight neighbours <see cref="MapDirections.GetNeighbours"/> gives, which is pinned by a test so the two helpers
    /// cannot drift. Radius 0 is the single tile, which is the camp and every Structure that is nobody's Settlement.
    /// </para>
    /// </remarks>
    internal static IReadOnlyCollection<Coord> GetAssertedTiles(Coord location, int territoryRadius, CampaignMapGeometry geometry)
    {
        ArgumentNullException.ThrowIfNull(location);
        ArgumentNullException.ThrowIfNull(geometry);

        return SightRadius.GetSquare(location, territoryRadius, geometry);
    }
}
