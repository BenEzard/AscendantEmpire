using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.FrontendContracts.Biomes.GetTileSupportLevels;

/// <summary>
/// One observed tile's support level, and how many Turns it can feed the Forces.
/// </summary>
public interface ITileSupportLevelEntry
{
    int X { get; }

    int Y { get; }

    /// <summary>
    /// Whether the tile can feed the Forces, serialised as the member's name (<c>Plenty</c>, <c>OneTurn</c>, <c>Insufficient</c> or <c>Exhausted</c>).
    /// </summary>
    TileSupportLevel SupportLevel { get; }

    /// <summary>
    /// The whole Turns the tile can feed the Forces, with the tile regrowing at the end of each Round: 0 to 5. <see langword="null"/> when it still feeds them after 5, which
    /// may mean indefinitely. It assumes nobody else feeds on the tile, which holds while the Forces stand on it.
    /// </summary>
    int? Turns { get; }
}
