namespace AscendantEmpire.Domain.Enums.Maps;

/// <summary>
/// Where a piece of a tile's food comes from, which decides what spending it writes.
/// </summary>
/// <remarks>
/// <para>
/// Computed and never stored. It exists because the amount taken from the biome and the amount taken from a patch standing on it are deducted in different
/// places - a <c>*Taken</c> column against a rate, and an instance's own quantity against a stock - while the feed that works them out treats them alike.
/// </para>
/// <para>
/// A <c>Bank</c> member is expected, for the Force asset bank a supply chain will draw on. It is not here yet because nothing banks food.
/// </para>
/// </remarks>
public enum FoodSourceType
{
    /// <summary>The tile's own biome, which replenishes at a rate rather than holding a stock.</summary>
    Tile = 1,

    /// <summary>A MapResource instance standing on the tile, which holds a quantity that spending lowers.</summary>
    MapResource = 2
}
