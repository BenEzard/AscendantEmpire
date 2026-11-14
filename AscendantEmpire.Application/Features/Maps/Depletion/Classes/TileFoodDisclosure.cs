using AscendantEmpire.BackendContracts.Biomes;

namespace AscendantEmpire.Application.Features.Maps.Depletion.Classes;

/// <summary>
/// What one tile's food comes to for one Player, in the individuals it feeds for a Turn.
/// </summary>
/// <param name="IndividualsFed">The total, or <see langword="null"/> when the Player has nothing that eats and so no diet to price the tile against.</param>
/// <param name="IsTotalEstimated">Whether a MapResource patch is part of the total, which makes the whole of it fuzzed.</param>
/// <param name="Contributions">The parts of the total, in the same unit, biome resources first.</param>
internal sealed record TileFoodDisclosure(
    decimal? IndividualsFed,
    bool IsTotalEstimated,
    IReadOnlyList<TileFoodContribution> Contributions)
{
    /// <summary>
    /// Nothing to disclose: an unobserved tile, or a Player with no Force left that eats.
    /// </summary>
    public static TileFoodDisclosure None { get; } = new(null, false, []);
}
