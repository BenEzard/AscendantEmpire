using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.BackendContracts.Biomes;

/// <summary>
/// The Forage, Hunt, Fish and Fodder available to one Player on one campaign map tile.
/// </summary>
/// <remarks>
/// A <see langword="null"/> value means the resource does not exist in the tile's biome, or that the tile is not in the Player's live sight (see <see cref="IsObserved"/>).
/// </remarks>
public record TileSustenance
{
    /// <summary>
    /// A tile the Player cannot see right now: nothing about what is left on it is disclosed.
    /// </summary>
    public static TileSustenance NotObserved { get; } = new() { IsObserved = false };

    /// <summary>
    /// Whether the tile is in the Player's live sight. Every value is <see langword="null"/> when it is not.
    /// </summary>
    public required bool IsObserved { get; init; }

    public decimal? Forage { get; init; }

    public decimal? Hunt { get; init; }

    public decimal? Fish { get; init; }

    public decimal? Fodder { get; init; }

    /// <summary>
    /// How many individuals everything on the tile feeds for one Turn, or <see langword="null"/> when there is no diet to price it against.
    /// </summary>
    /// <remarks>
    /// Priced against the food <c>SupplySet</c> the Player's Forces eat from, so it answers "how many of mine would this feed" rather than adding unlike
    /// Assets together. A Player with no Force left that eats gets no figure: the tile's food is real, but nothing is there to measure it in.
    /// </remarks>
    public decimal? IndividualsFed { get; init; }

    /// <summary>
    /// Whether <see cref="IndividualsFed"/> is fuzzed, which it is as soon as any MapResource patch is part of it.
    /// </summary>
    public bool IsTotalEstimated { get; init; }

    /// <summary>
    /// What the total is made of, in the same unit, biome resources first and then the patches standing on the tile.
    /// </summary>
    public IReadOnlyList<TileFoodContribution> FoodSources { get; init; } = [];

    /// <summary>
    /// Whether the tile can feed the Player's Forces standing on it. <see cref="TileSupportLevel.Exhausted"/> for a stripped tile even with no Forces on it; otherwise
    /// <see langword="null"/> when no Forces are on it, or when the tile is not observed.
    /// </summary>
    public TileSupportLevel? SupportLevel { get; init; }
}
