namespace AscendantEmpire.Application.Features.Settlements.Classes;

/// <summary>
/// How many of a Settlement's civilians work one of its territory tiles.
/// </summary>
/// <remarks>
/// <para>
/// <b>The whole population works every tile.</b> A Settlement of 100 staffs each of its nine tiles with 100 civilians, which is deliberately unrealistic and
/// is the first thing about the harvest to refine - see "Who does the work" in <c>docs/design/settlements.md</c>.
/// </para>
/// <para>
/// A class of its own, for one line, because it is the seam. Allocating civilians to tiles - a count per Settlement and tile, with a tile taking a share of
/// its regrowth proportional to how fully it is staffed - changes this and nothing else: the harvest already asks one place how many civilians work a tile.
/// </para>
/// </remarks>
internal static class SettlementWorkAllocation
{
    /// <summary>
    /// The civilians working one territory tile.
    /// </summary>
    /// <param name="population">The Settlement's whole population.</param>
    /// <returns>The number working the tile, which is never negative.</returns>
    public static long CiviliansOnTile(int population)
    {
        // A Settlement with nobody in it works nothing. Negative is not a population any more than it is a workforce, so it answers the same.
        return population <= 0 ? 0 : population;
    }
}
