namespace AscendantEmpire.Domain.Entities.Maps.CampaignMaps;

/// <summary>
/// A MapResource the map editor has placed on a gallery tile, which every game created from the map starts with.
/// </summary>
/// <remarks>
/// <para>
/// Game setup copies each placement into a <c>game.MapResourceInstance</c>, rolling whatever the editor left unset from the game's seed. See
/// <c>docs/design/campaign-map/map-resources.md</c>.
/// </para>
/// <para>
/// A tile holds at most two MapResources, one per <see cref="Slot"/>, and at most one of each type. Both are enforced by the database, as they are on the game's instances.
/// </para>
/// </remarks>
public class CampaignMapGalleryResource
{
    /// <summary>
    /// The lowest slot on a tile.
    /// </summary>
    public const byte MIN_SLOT = 1;

    /// <summary>
    /// The highest slot on a tile, and so the most MapResources a tile can hold.
    /// </summary>
    public const byte MAX_SLOT = 2;

    public int Id { get; set; }

    /// <summary>
    /// The gallery tile the MapResource is placed on.
    /// </summary>
    public int CampaignMapGalleryId { get; set; }

    /// <summary>
    /// What kind of MapResource it is (a <c>map.MapResourceType</c>).
    /// </summary>
    public int MapResourceTypeId { get; set; }

    /// <summary>
    /// Which of the tile's places it takes: <see cref="MIN_SLOT"/> or <see cref="MAX_SLOT"/>.
    /// </summary>
    public byte Slot { get; set; }

    /// <summary>
    /// The quantity the map editor chose for it, stored scaled by <c>ScaledAmount.Scale</c> (1,000), or <see langword="null"/> to roll it at game setup from the habitat's
    /// initial quantity range.
    /// </summary>
    /// <remarks>
    /// Only for a type that holds a single quantity: a Distribution starts at its scenario's amounts, and a stratified type's layers are always rolled.
    /// </remarks>
    public long? InitialQuantity { get; set; }

    /// <summary>
    /// The Distribution scenario the map editor chose, or <see langword="null"/> to pick one at game setup, weighted by the tile's Biome.
    /// </summary>
    /// <remarks>
    /// Only for a Distribution, and must be one of its type's own scenarios.
    /// </remarks>
    public int? MapResourceDistributionScenarioId { get; set; }
}
