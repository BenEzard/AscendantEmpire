using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.BackendContracts.Biomes;

/// <summary>
/// One observed tile's support level, and how many Turns it can feed the Forces.
/// </summary>
public record TileSupportLevelEntry
{
    public required int X { get; init; }

    public required int Y { get; init; }

    public required TileSupportLevel SupportLevel { get; init; }

    /// <summary>
    /// The whole Turns the tile can feed the Forces, regrowth included: 0 to 5. <see langword="null"/> when it still feeds them after 5, which may mean indefinitely.
    /// </summary>
    public required int? Turns { get; init; }
}
