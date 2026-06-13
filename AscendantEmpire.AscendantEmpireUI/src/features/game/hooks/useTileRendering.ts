import { useCallback, useMemo, type CSSProperties } from "react";
import type { Biome } from "@/types/map/Biome";
import type { Geography } from "@/types/map/Geography";
import type { TileCell } from "@/features/map/components/TileTypes";

interface UseTileRenderingOptions {
  biomes: Biome[] | undefined;
  geographies: Geography[] | undefined;
  selectedTile: { x: number; y: number } | null;
  observedTileKeys: Set<string>;
}

/**
 * Builds the biome/geography lookup maps and the tile style/title accessors for the
 * game map. Tile style layers a selection highlight and a fog-of-war overlay (for
 * tiles not currently observed) over the biome colour. The name maps are also returned
 * for callers that need them (e.g. the click toast).
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
      const fogOverlay = !isObserved ? "linear-gradient(rgba(80, 80, 80, 0.55), rgba(80, 80, 80, 0.55)), " : "";

      return {
        background: `${selectionOverlay}${fogOverlay}${baseBg}`,
        outline: isSelected ? "2px solid rgba(255, 255, 0, 0.8)" : undefined,
        outlineOffset: "-2px",
      };
    },
    [biomeColorByType, observedTileKeys, selectedTile],
  );

  const getTileTitle = useCallback(
    (t: TileCell) => {
      const biomeName = t.biomeTypeId != null ? (biomeNameByType.get(t.biomeTypeId) ?? "(unknown biome)") : "(no biome)";
      const geographyName = t.geographyTypeId != null ? (geographyNameByType.get(t.geographyTypeId) ?? "(unknown geography)") : "(no geography)";
      const label = biomeName === "Unknown" && geographyName === "Unknown" ? "Unknown" : `${biomeName}, ${geographyName}`;
      return `(${t.x}, ${t.y}); ${label}`;
    },
    [biomeNameByType, geographyNameByType],
  );

  return { biomeNameByType, geographyNameByType, getTileStyle, getTileTitle };
}
