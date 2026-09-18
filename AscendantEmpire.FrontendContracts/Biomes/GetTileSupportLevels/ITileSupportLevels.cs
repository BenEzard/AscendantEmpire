namespace AscendantEmpire.FrontendContracts.Biomes.GetTileSupportLevels;

/// <summary>
/// The support level of every tile in the calling Player's live sight, for the Forces they have selected.
/// </summary>
/// <remarks>
/// <para>
/// Fetched once per selection, so the hover marker is a lookup rather than a request per hovered tile. A tile's need is the selected Forces plus the Player's own Forces
/// already standing on it. See "Support indicator" in <c>docs/design/campaign-map/support-indicator.md</c>.
/// </para>
/// <para>
/// Tiles outside live sight are never included: what is left on a tile changes as anyone feeds on it, so a level there would leak where other Players' Forces have been.
/// </para>
/// </remarks>
public interface ITileSupportLevels
{
    /// <summary>
    /// One entry per observed tile with a level. A tile with food but nobody to feed has none, and is left out.
    /// </summary>
    IReadOnlyCollection<ITileSupportLevelEntry> Tiles { get; }
}
