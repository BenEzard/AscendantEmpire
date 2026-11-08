namespace AscendantEmpire.FrontendContracts.MapResources.GetPlayerMapResources;

/// <summary>
/// Every MapResource the calling Player has discovered, with a quantity for those they can see right now.
/// </summary>
/// <remarks>
/// <para>
/// Fetched once for the whole map, so drawing a tile is a lookup rather than a request per tile.
/// </para>
/// <para>
/// A resource the Player has not discovered is never included. One they have stays included wherever they go, because what they found is a fact about the map
/// they have learned — but it carries a quantity only while the tile is observed, since a quantity changes as it is extracted and would otherwise leak what
/// other Players have been doing there. <c>IsObserved</c> says which it is.
/// </para>
/// </remarks>
public interface IPlayerMapResources
{
    /// <summary>
    /// One entry per visible instance, ordered by tile and then slot.
    /// </summary>
    IReadOnlyCollection<IPlayerMapResourceEntry> Resources { get; }
}
