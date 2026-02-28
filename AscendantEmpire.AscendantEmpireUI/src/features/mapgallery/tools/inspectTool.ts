import type { TileCell } from "../types/TileTypes";

export interface InspectToolContext {
  openToast: (message: string, variant?: "success" | "danger" | "warning" | "info") => void;
  biomeNameByType: Map<number, string>;
  geographyNameByType: Map<number, string>;
}

export function onInspectTileClick(tile: TileCell, ctx: InspectToolContext) {
  const biomeName =
    tile.biomeTypeId != null ? (ctx.biomeNameByType.get(tile.biomeTypeId) ?? "(unknown biome)") : "(no biome)";

  const geographyName =
    tile.geographyTypeId != null
      ? (ctx.geographyNameByType.get(tile.geographyTypeId) ?? "(unknown geography)")
      : "(no geography)";

  ctx.openToast(`(${tile.x},${tile.y}) ${biomeName}, ${geographyName}`, "info");
}
