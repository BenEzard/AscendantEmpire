using AscendantEmpire.Application.Features.Maps.MapResources.Classes;
using AscendantEmpire.Domain.Entities.Maps.CampaignMaps;
using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.Application.Features.Maps.MapResources.Services;

/// <summary>
/// Decides which tiles should hold the MapResource their Biome implies, and which placements should go.
/// </summary>
/// <remarks>
/// <para>
/// Pure and in-memory on purpose. Generating a map and painting a tile in the map editor write their placements in completely different ways - a bulk insert
/// of tens of thousands of rows, against a handful of tracked changes inside a transaction - but they must <i>decide</i> identically, or a forest drawn by
/// hand ends up holding something a generated forest does not. Keeping the decision here is what guarantees that, and what lets every rule below be pinned
/// without a database.
/// </para>
/// <para>
/// See <c>docs/design/campaign-map/map-resources.md</c>.
/// </para>
/// </remarks>
internal static class BiomeImpliedResourceRules
{
    /// <summary>
    /// Whether a MapResourceType's habitats cover a tile's Biome and Geography.
    /// </summary>
    /// <remarks>
    /// A Biome-wide habitat (no Geography) covers every Geography in that Biome; a habitat naming a Geography covers only that one. No covering row means the
    /// type can never exist on the tile. The same rule is applied as a database query, for one tile at a time, by
    /// <c>PlaceGalleryMapResourceHandler</c> - change both together.
    /// </remarks>
    internal static bool HasHabitat(IReadOnlyCollection<HabitatScope> habitats, BiomeType biome, CampaignMapGeographyType geography)
    {
        return habitats.Any(habitat => habitat.BiomeTypeId == biome
            && (habitat.CampaignMapGeographyTypeId == null || habitat.CampaignMapGeographyTypeId == geography));
    }

    /// <summary>
    /// What would have to change for each of these tiles to hold the MapResourceType exactly where its habitats allow it.
    /// </summary>
    /// <param name="mapResourceTypeId">The type being kept in step with the tiles' Biomes.</param>
    /// <param name="habitats">Every habitat of that type, which is what decides the Biomes it belongs in.</param>
    /// <param name="tiles">The tiles to consider, carrying the terrain they will have once any paint is applied.</param>
    /// <remarks>
    /// <para>
    /// A tile the habitats cover gains a placement in its first free slot, unless it already holds the type, or unless both of its slots are taken by other
    /// MapResources - the tile is not wrong in that case, it is just full, so it is refused rather than failed.
    /// </para>
    /// <para>
    /// A tile the habitats no longer cover loses its placement, unless the map editor pinned a scenario or set a quantity on it. That is authored data, so it
    /// is refused instead and left for the author to clear.
    /// </para>
    /// </remarks>
    internal static BiomeImpliedResourcePlan Plan(
        int mapResourceTypeId,
        IReadOnlyCollection<HabitatScope> habitats,
        IReadOnlyCollection<TileResourceState> tiles)
    {
        List<PlacementToAdd> toAdd = [];
        List<int> placementIdsToDelete = [];
        List<RefusedTile> refusals = [];

        foreach (TileResourceState tile in tiles)
        {
            bool belongsHere = HasHabitat(habitats, tile.BiomeTypeId, tile.CampaignMapGeographyTypeId);
            TilePlacementState? existing = tile.Placements.SingleOrDefault(placement => placement.MapResourceTypeId == mapResourceTypeId);

            if (belongsHere)
            {
                // Already there: nothing to do. This is what keeps re-running generation, or painting the same tiles twice, from challenging
                // UX_CampaignMapGalleryResource_Tile_Type.
                if (existing != null)
                {
                    continue;
                }

                byte? freeSlot = FindFreeSlot(tile);

                if (freeSlot == null)
                {
                    refusals.Add(Refuse(tile, BiomeImpliedResourceRefusal.NoFreeSlot));
                    continue;
                }

                toAdd.Add(new PlacementToAdd(tile.CampaignMapGalleryId, freeSlot.Value));
                continue;
            }

            // Does not belong here, and is not here. The common case for every tile of a Biome that never holds the type.
            if (existing == null)
            {
                continue;
            }

            if (existing.CarriesAuthorIntent)
            {
                refusals.Add(Refuse(tile, BiomeImpliedResourceRefusal.AuthorIntentBlocksRemoval));
                continue;
            }

            placementIdsToDelete.Add(existing.Id);
        }

        return new BiomeImpliedResourcePlan(mapResourceTypeId, toAdd, placementIdsToDelete, refusals);
    }

    /// <summary>
    /// The lowest slot on the tile that nothing holds, or <see langword="null"/> when it is full.
    /// </summary>
    /// <remarks>
    /// Computed rather than assumed to be the first slot, so the same rule is right on a freshly generated tile and on one an author has already placed
    /// something on. Mirrors the slot choice in <c>PlaceGalleryMapResourceHandler</c>.
    /// </remarks>
    private static byte? FindFreeSlot(TileResourceState tile)
    {
        return Enumerable.Range(CampaignMapGalleryResource.MIN_SLOT, CampaignMapGalleryResource.MAX_SLOT)
            .Select(slot => (byte)slot)
            .Where(slot => tile.Placements.Any(placement => placement.Slot == slot) == false)
            .Select(slot => (byte?)slot)
            .FirstOrDefault();
    }

    /// <summary>
    /// Records a tile whose placements were left exactly as they were, with the reason.
    /// </summary>
    private static RefusedTile Refuse(TileResourceState tile, BiomeImpliedResourceRefusal reason)
    {
        return new RefusedTile(tile.CampaignMapGalleryId, tile.XCoord, tile.YCoord, reason);
    }
}
