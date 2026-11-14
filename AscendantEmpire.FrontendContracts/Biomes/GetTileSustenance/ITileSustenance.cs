using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.FrontendContracts.Biomes.GetTileSustenance;

/// <summary>
/// The Forage, Hunt, Fish and Fodder available to the calling Player on one campaign map tile.
/// </summary>
/// <remarks>
/// <para>
/// Each value is what is left of the Player's own effective value once the tile's Player-neutral <c>Taken</c> amount is removed, so two Players can see different amounts on
/// the same tile. See "What a tile stores: the amount taken" in <c>docs/design/campaign-map/depletion.md</c>.
/// </para>
/// <para>
/// A <see langword="null"/> value means the resource does not exist in the tile's biome; <c>0</c> means it exists but nothing is left.
/// </para>
/// </remarks>
public interface ITileSustenance
{
    /// <summary>
    /// Whether the tile is in the Player's live sight. When it is not, every value is <see langword="null"/>: the amounts left on a tile change as anyone feeds on it, so
    /// reporting them on an unwatched tile would leak where other Players' Forces have been.
    /// </summary>
    bool IsObserved { get; }

    decimal? Forage { get; }

    decimal? Hunt { get; }

    decimal? Fish { get; }

    decimal? Fodder { get; }

    /// <summary>
    /// How many individuals everything on the tile feeds for one Turn, or <see langword="null"/> when there is no diet to price it against.
    /// </summary>
    /// <remarks>
    /// Priced against the food supply set the Player's Forces eat from, so a berry patch feeds fewer than the same amount of game would. A Player with no
    /// Force left that eats gets no figure, there being nothing to measure the tile's food in.
    /// </remarks>
    decimal? IndividualsFed { get; }

    /// <summary>
    /// Whether <see cref="IndividualsFed"/> is fuzzed, which it is as soon as any MapResource patch is part of it. The client marks such a figure with a tilde.
    /// </summary>
    bool IsTotalEstimated { get; }

    /// <summary>
    /// What the total is made of, in the same unit, biome resources first and then the patches standing on the tile.
    /// </summary>
    IReadOnlyList<ITileFoodContribution> FoodSources { get; }

    /// <summary>
    /// Whether the tile can feed the Player's Forces standing on it, serialised as the member's name. <c>Exhausted</c> for a stripped tile even with no Forces on it;
    /// otherwise <see langword="null"/> when no Forces are on it, or when the tile is not observed. Turns are forecast with end-of-Round regrowth, for the Forces as they stand.
    /// </summary>
    TileSupportLevel? SupportLevel { get; }
}
