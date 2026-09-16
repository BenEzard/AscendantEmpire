using AscendantEmpire.Domain.Enums.Resources;

namespace AscendantEmpire.FrontendContracts.Biomes.GetTileSupportLevels;

/// <summary>
/// One observed tile's support level.
/// </summary>
public interface ITileSupportLevelEntry
{
    int X { get; }

    int Y { get; }

    /// <summary>
    /// Whether the tile can feed the Forces, serialised as the member's name (<c>Plenty</c>, <c>OneTurn</c>, <c>Insufficient</c> or <c>Exhausted</c>). A count of Turns
    /// ignores regrowth and other Players feeding.
    /// </summary>
    TileSupportLevel SupportLevel { get; }
}
