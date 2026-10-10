using AscendantEmpire.Domain.Entities.Maps.CampaignMaps;
using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.Application.Features.Maps.MapResources.Classes;

/// <summary>
/// One <c>map.MapResourceHabitat</c> row reduced to where it applies.
/// </summary>
/// <remarks>
/// A null <see cref="CampaignMapGeographyTypeId"/> is a Biome-wide row, covering every Geography in that Biome.
/// </remarks>
internal readonly record struct HabitatScope(BiomeType BiomeTypeId, CampaignMapGeographyType? CampaignMapGeographyTypeId);

/// <summary>
/// A MapResource already placed on a gallery tile, reduced to what deciding its fate takes.
/// </summary>
/// <param name="CarriesAuthorIntent">
/// Whether the map editor chose something about this placement that generating or repainting a tile must not throw away.
/// </param>
/// <remarks>
/// <see cref="CarriesAuthorIntent"/> is computed where the placement is read, so what counts as author intent is decided in one place and the rules below
/// never see a nullable column.
/// </remarks>
internal sealed record TilePlacementState(int Id, int MapResourceTypeId, byte Slot, bool CarriesAuthorIntent);

/// <summary>
/// A gallery tile, the terrain it will have, and everything already placed on it.
/// </summary>
/// <remarks>
/// The Biome and Geography are the values to decide against, so on the paint path they are what the tile is <i>becoming</i>, not what it was read as. A caller
/// that has to report a tile's original terrain keeps hold of it itself.
/// </remarks>
internal sealed record TileResourceState(
    int CampaignMapGalleryId,
    int XCoord,
    int YCoord,
    BiomeType BiomeTypeId,
    CampaignMapGeographyType CampaignMapGeographyTypeId,
    IReadOnlyCollection<TilePlacementState> Placements);

/// <summary>
/// Why a tile could not be brought into line with what its Biome implies.
/// </summary>
internal enum BiomeImpliedResourceRefusal
{
    /// <summary>
    /// The Biome implies the MapResource, but the tile's two slots are already taken by others.
    /// </summary>
    NoFreeSlot,

    /// <summary>
    /// The Biome no longer implies the MapResource, but the placement on it was authored in the map editor.
    /// </summary>
    AuthorIntentBlocksRemoval
}

/// <summary>
/// A tile whose placements were left exactly as they were, and why.
/// </summary>
/// <remarks>
/// Carries no terrain: a caller reporting a refusal needs the tile's <i>original</i> Biome and Geography, which it already holds, while the rules decided
/// against the terrain the tile is becoming.
/// </remarks>
internal sealed record RefusedTile(
    int CampaignMapGalleryId,
    int XCoord,
    int YCoord,
    BiomeImpliedResourceRefusal Reason);

/// <summary>
/// A <see cref="CampaignMapGalleryResource"/> to create, with the slot it takes.
/// </summary>
internal sealed record PlacementToAdd(int CampaignMapGalleryId, byte Slot);

/// <summary>
/// What would have to change for a set of tiles to hold a MapResourceType exactly where its habitats allow it.
/// </summary>
/// <remarks>
/// <para>
/// Decided entirely in memory by <c>BiomeImpliedResourceRules.Plan</c>, so the same rules answer for a freshly generated map and for a brush stroke in the map
/// editor, which then apply the answer in the way that suits them.
/// </para>
/// <para>
/// A tile that already holds the type contributes nothing, which is what makes applying a plan twice harmless.
/// </para>
/// </remarks>
internal sealed record BiomeImpliedResourcePlan(
    int MapResourceTypeId,
    IReadOnlyCollection<PlacementToAdd> ToAdd,
    IReadOnlyCollection<int> PlacementIdsToDelete,
    IReadOnlyCollection<RefusedTile> Refusals)
{
    /// <summary>
    /// Nothing to do, because the MapResourceType is not in this database.
    /// </summary>
    /// <remarks>
    /// Also the answer to fall back on when the habitats could not be read: a map's tiles are correct with or without the MapResources their Biomes imply.
    /// </remarks>
    public static BiomeImpliedResourcePlan None { get; } = new(0, [], [], []);

    /// <summary>
    /// Whether applying this plan would change nothing and refuse nothing.
    /// </summary>
    public bool IsEmpty => ToAdd.Count == 0 && PlacementIdsToDelete.Count == 0 && Refusals.Count == 0;
}
