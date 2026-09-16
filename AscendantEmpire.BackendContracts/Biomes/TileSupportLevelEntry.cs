using AscendantEmpire.Domain.Enums.Resources;

namespace AscendantEmpire.BackendContracts.Biomes;

/// <summary>
/// One observed tile's support level.
/// </summary>
public record TileSupportLevelEntry
{
    public required int X { get; init; }

    public required int Y { get; init; }

    public required TileSupportLevel SupportLevel { get; init; }
}
