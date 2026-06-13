import { useCallback, useMemo, type CSSProperties } from "react";
import type { Biome } from "@/types/map/Biome";
import type { Geography } from "@/types/map/Geography";
import type { TileCell } from "@/features/mapgallery/types/TileTypes";
import { pickTextColorOnBg } from "@/util/color";

interface UseTileRenderingOptions {
  biomes: Biome[] | undefined;
  geographies: Geography[] | undefined;
  /** Set of "x,y" keys currently under the brush preview, or null when not painting. */
  previewKeySet: Set<string> | null;
}

/**
 * Builds the biome/geography lookup maps and the tile style/title accessors used by
 * the editor's TileGrid. The lookups are also returned for callers that need names
 * (e.g. the inspect tool).
 */
export function useTileRendering(opts: UseTileRenderingOptions) {
  const { biomes, geographies, previewKeySet } = opts;

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
      const preview = previewKeySet?.has(`${t.x},${t.y}`) ?? false;
      const previewStroke = bg ? pickTextColorOnBg(bg) : "#000000";

      return {
        background: bg ?? "rgba(0,0,0,0.03)",
        boxShadow: preview ? `inset 0 0 0 2px ${previewStroke}` : undefined,
      };
    },
    [biomeColorByType, previewKeySet],
  );

  const getTileTitle = useCallback(
    (t: TileCell) => {
      const biomeName = t.biomeTypeId != null ? (biomeNameByType.get(t.biomeTypeId) ?? "(unknown biome)") : "(no biome)";
      const geographyName = t.geographyTypeId != null ? (geographyNameByType.get(t.geographyTypeId) ?? "(unknown geography)") : "(no geography)";
      return `(${t.x}, ${t.y}); ${biomeName}, ${geographyName}`;
    },
    [biomeNameByType, geographyNameByType],
  );

  return { biomeColorByType, biomeNameByType, geographyNameByType, getTileStyle, getTileTitle };
}
