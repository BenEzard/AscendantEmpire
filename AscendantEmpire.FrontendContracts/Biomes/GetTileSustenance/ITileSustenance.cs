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
    /// Whether the tile can feed the Player's Forces standing on it, serialised as the member's name. <c>Exhausted</c> for a stripped tile even with no Forces on it;
    /// otherwise <see langword="null"/> when no Forces are on it, or when the tile is not observed. Turns are forecast with end-of-Round regrowth, for the Forces as they stand.
    /// </summary>
    TileSupportLevel? SupportLevel { get; }
}
