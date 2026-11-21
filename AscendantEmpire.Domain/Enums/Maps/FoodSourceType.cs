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
/// Only what the tile offers. What a Force eats out of its own load is not keyed here: that take is identified by the bank and the Asset it came out of
/// rather than by anything on the map, and it is spent by <c>ICarriedFoodConsumptionService</c> after the tile has given everything it has.
/// </para>
/// </remarks>
public enum FoodSourceType
{
    /// <summary>The tile's own biome, which replenishes at a rate rather than holding a stock.</summary>
    Tile = 1,

    /// <summary>A MapResource instance standing on the tile, which holds a quantity that spending lowers.</summary>
    MapResource = 2
}
