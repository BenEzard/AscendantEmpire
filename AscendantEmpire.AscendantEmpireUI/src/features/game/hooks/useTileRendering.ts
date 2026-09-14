import { useCallback, useMemo, type CSSProperties } from "react";
import type { Biome } from "@/types/map/Biome";
import type { Geography } from "@/types/map/Geography";
import type { TileCell } from "@/features/map/components/TileTypes";

interface UseTileRenderingOptions {
  biomes: Biome[] | undefined;
  geographies: Geography[] | undefined;
  selectedTile: { x: number; y: number } | null;
  observedTileKeys: ReadonlySet<string>;
}

/**
 * Ground the player has never been to. Near-opaque, because there is nothing underneath worth showing: an unexplored tile has no
 * real biome, so what it hides is a placeholder rather than terrain.
 */
const UNEXPLORED_FOG = "linear-gradient(rgba(48, 48, 48, 0.9), rgba(48, 48, 48, 0.9)), ";

/**
 * Ground the player has explored but has nothing watching right now. A light haze, deliberately much weaker than the fog above:
 * the terrain under it is real and the player is meant to read it, while still being able to tell at a glance that what they are
 * looking at is memory rather than a live view.
 */
const UNWATCHED_HAZE = "linear-gradient(rgba(90, 90, 90, 0.35), rgba(90, 90, 90, 0.35)), ";

/**
 * Builds the biome/geography lookup maps and the tile style/title accessors for the
 * game map. Tile style layers a selection highlight and a fog-of-war overlay over the
 * biome colour. The terrain label is also returned on its own, for the tile toolbar.
 *
 * The fog has two layers, because the map answers two different questions per tile.
 * *Explored* is permanent and rides on the tile itself: once the player has seen the
 * ground it stays known. *Observed* is live and comes from the observer set: it is only
 * true while one of the player's forces or structures is actually looking at the tile.
 * A tile is therefore drawn in one of three ways — unexplored (solid fog), explored but
 * unwatched (haze over real terrain), or watched (no overlay at all). Collapsing the two
 * into one flag is what made a step turn the whole map grey behind the force that moved.
 * See docs/fog-of-war.md.
 */
export function useTileRendering(opts: UseTileRenderingOptions) {
  const { biomes, geographies, selectedTile, observedTileKeys } = opts;

  const biomeColorByType = useMemo(() => {
    const m = new Map<number, string>();
    for (const b of biomes ?? []) {
      const c = (b.mapEditorHtmlColor ?? "").trim();
      if (c) m.set(b.biomeType, c);
    }
    return m;
  }, [biomes]);

  const biomeNameByType = useMemo(() => {
    const m = new Map<number, string>();
    for (const b of biomes ?? []) m.set(b.biomeType, b.name);
    return m;
  }, [biomes]);

  const geographyNameByType = useMemo(() => {
    const m = new Map<number, string>();
    for (const g of geographies ?? []) m.set(g.campaignMapGeographyType, g.name);
    return m;
  }, [geographies]);

  const getTileStyle = useCallback(
    (t: TileCell): CSSProperties => {
      const bg = t.biomeTypeId != null ? biomeColorByType.get(t.biomeTypeId) : undefined;
      const baseBg = bg ?? "rgba(0,0,0,0.03)";

      const isSelected = selectedTile != null && selectedTile.x === t.x && selectedTile.y === t.y;
      const isObserved = observedTileKeys.has(`${t.x},${t.y}`);

      const selectionOverlay = isSelected ? "linear-gradient(rgba(255, 255, 0, 0.35), rgba(255, 255, 0, 0.35)), " : "";

      // Watched ground gets nothing over it. Unexplored beats unwatched: a tile nobody is looking at can also be one the
      // player has never been to, and there is no sense hazing terrain that is already hidden.
      let fogOverlay = "";
      if (!t.isExplored) {
        fogOverlay = UNEXPLORED_FOG;
      } else if (!isObserved) {
        fogOverlay = UNWATCHED_HAZE;
      }

      return {
        background: `${selectionOverlay}${fogOverlay}${baseBg}`,
        outline: isSelected ? "2px solid rgba(255, 255, 0, 0.8)" : undefined,
        outlineOffset: "-2px",
      };
    },
    [biomeColorByType, observedTileKeys, selectedTile],
  );

  /**
   * Names a tile's terrain as "Biome, Geography".
   *
   * Collapses to a single "Unknown" when both are the Unknown placeholder an unexplored tile carries, rather than reading
   * "Unknown, Unknown". Shared by the hover title and the tile toolbar so the two always describe a tile the same way.
   */
  const getTileTerrainLabel = useCallback(
    (t: TileCell) => {
      const biomeName = t.biomeTypeId != null ? (biomeNameByType.get(t.biomeTypeId) ?? "(unknown biome)") : "(no biome)";
      const geographyName = t.geographyTypeId != null ? (geographyNameByType.get(t.geographyTypeId) ?? "(unknown geography)") : "(no geography)";

      return biomeName === "Unknown" && geographyName === "Unknown" ? "Unknown" : `${biomeName}, ${geographyName}`;
    },
    [biomeNameByType, geographyNameByType],
  );

  const getTileTitle = useCallback(
    (t: TileCell) => {
      const label = getTileTerrainLabel(t);

      // Only forces whose movement we were actually told about. The API masks the numbers for forces the player
      // doesn't own, so an enemy standing here contributes nothing to the tooltip rather than a blank or a guess.
      const movementLines = (t.forces ?? [])
        .filter((force) => force.campaignMovementRemaining != null && force.campaignMovementPoints != null)
        .map((force) => `Force #${force.id}: ${force.campaignMovementRemaining} / ${force.campaignMovementPoints} MP`);

      return [`(${t.x}, ${t.y}); ${label}`, ...movementLines].join("\n");
    },
    [getTileTerrainLabel],
  );

  return { getTileStyle, getTileTitle, getTileTerrainLabel };
}
