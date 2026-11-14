namespace AscendantEmpire.BackendContracts.Biomes;

/// <summary>
/// One thing a tile offers a Player to eat, measured in the individuals it feeds for a Turn.
/// </summary>
/// <remarks>
/// The parts of <see cref="TileSustenance.IndividualsFed"/>, in the same unit, so the breakdown the toolbar shows adds up to the total beside it. A biome
/// resource and a MapResource patch are both entries here; what tells them apart is <see cref="IsEstimated"/>.
/// </remarks>
public record TileFoodContribution
{
    /// <summary>
    /// What the Player calls it: a biome resource's name ("Forage"), or the MapResource type's display name ("Berries").
    /// </summary>
    public required string Label { get; init; }

    /// <summary>
    /// How many individuals this source alone feeds for a Turn, at the price the Forces' food set puts on its Asset.
    /// </summary>
    public required decimal IndividualsFed { get; init; }

    /// <summary>
    /// Whether the figure is fuzzed. True for every MapResource patch, whose quantity the Player only ever estimates, and false for the biome's own
    /// resources, which are their own effective values.
    /// </summary>
    public required bool IsEstimated { get; init; }
}
