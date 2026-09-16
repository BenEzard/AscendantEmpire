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
}
