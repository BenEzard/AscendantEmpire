namespace AscendantEmpire.BackendContracts.Biomes;

/// <summary>
/// The support level of every tile in a Player's live sight, for the selected Forces - what the hover marker draws.
/// </summary>
public record TileSupportLevels
{
    /// <summary>
    /// One entry per observed tile with something to say. A tile with food but nobody to feed has no level, and is left out.
    /// </summary>
    public required IReadOnlyCollection<TileSupportLevelEntry> Tiles { get; init; }
}
