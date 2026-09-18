namespace AscendantEmpire.Domain.Enums.Resources;

/// <summary>
/// How well a campaign map tile can feed the Forces on it, or about to move onto it, from the food available to their Player.
/// </summary>
/// <remarks>
/// <para>
/// Shown as the tile toolbar's background and as the hover marker while a Force is selected. Serialised as the member's name, so the frontend keys its colours off
/// <c>"Plenty"</c>, <c>"OneTurn"</c>, <c>"Insufficient"</c> and <c>"Exhausted"</c>.
/// </para>
/// <para>
/// A count of Turns is forecast Turn by Turn, with the tile regrowing at the end of each Round, for the Forces as they stand and nobody else feeding on the tile. See
/// "Support indicator" in <c>docs/design/campaign-map.md</c>.
/// </para>
/// </remarks>
public enum TileSupportLevel
{
    /// <summary>
    /// Enough food for two Turns or more.
    /// </summary>
    Plenty = 1,

    /// <summary>
    /// Enough food for one Turn, but not two.
    /// </summary>
    OneTurn = 2,

    /// <summary>
    /// Some food, but not enough to feed the Forces for a whole Turn.
    /// </summary>
    Insufficient = 3,

    /// <summary>
    /// Nothing available at all: the tile has been stripped, or its biome offers no food.
    /// </summary>
    Exhausted = 4
}
