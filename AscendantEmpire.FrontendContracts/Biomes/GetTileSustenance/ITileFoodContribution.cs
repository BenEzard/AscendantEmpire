namespace AscendantEmpire.FrontendContracts.Biomes.GetTileSustenance;

/// <summary>
/// One thing a tile offers the calling Player to eat, measured in the individuals it feeds for a Turn.
/// </summary>
/// <remarks>
/// The parts of <see cref="ITileSustenance.IndividualsFed"/>, in the same unit, so the breakdown adds up to the total beside it.
/// </remarks>
public interface ITileFoodContribution
{
    /// <summary>
    /// What the Player calls it: a biome resource's name ("Forage"), or the MapResource type's display name ("Berries").
    /// </summary>
    string Label { get; }

    /// <summary>
    /// How many individuals this source alone feeds for a Turn, at the price the Forces' food set puts on its Asset.
    /// </summary>
    decimal IndividualsFed { get; }

    /// <summary>
    /// Whether the figure is fuzzed, which every MapResource patch's is and no biome resource's is. The client marks an estimated figure with a tilde.
    /// </summary>
    bool IsEstimated { get; }
}
