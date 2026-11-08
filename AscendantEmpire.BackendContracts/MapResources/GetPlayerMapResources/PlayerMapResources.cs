namespace AscendantEmpire.BackendContracts.MapResources.GetPlayerMapResources;

/// <summary>
/// Every MapResource the calling Player has discovered, with a quantity for those they can see right now.
/// </summary>
/// <remarks>
/// Fetched for the whole map rather than per tile, so drawing the map is a lookup. Discovery decides what is in here; observation decides only which entries
/// carry a quantity. See "What a discovered MapResource shows" in <c>docs/design/campaign-map/map-resources.md</c>.
/// </remarks>
public record PlayerMapResources
{
    /// <summary>
    /// One entry per visible instance, ordered by tile and then slot. A tile holding two carries two entries.
    /// </summary>
    public required IReadOnlyCollection<PlayerMapResourceEntry> Resources { get; init; }
}
