export const ASSET_INDICATOR_COLORS = {
  inactive: "#c7d0e0",
  imported: "#60a5fa",
  exported: "#4ade80",
  strategicReserve: "#111827",
} as const;

export const ASSET_TICKER_BACKGROUNDS = {
  default: "rgba(255, 255, 255, 0.85)",
  // Red tint — asset is on its way out
  obsoleteIn: "#fee2e2",
  // Orange tint — current demand exceeds supply
  demandNotMet: "#ffedd5",
} as const;

/** The API's `AssetGrade` member names. The bronze/silver/gold shields a ticker wears are presentation for Low/Medium/High. */
export type AssetGrade = "None" | "Low" | "Medium" | "High";

export function getAssetTickerBackground(args: { obsoleteIn?: number; demandNotMet?: boolean }): string {
  // obsoleteIn takes priority over demandNotMet
  if (args.obsoleteIn != null && args.obsoleteIn > 0) return ASSET_TICKER_BACKGROUNDS.obsoleteIn;
  if (args.demandNotMet) return ASSET_TICKER_BACKGROUNDS.demandNotMet;
  return ASSET_TICKER_BACKGROUNDS.default;
}
