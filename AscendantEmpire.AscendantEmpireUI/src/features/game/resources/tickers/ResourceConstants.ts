export const RESOURCE_INDICATOR_COLORS = {
  inactive: "#c7d0e0",
  imported: "#60a5fa",
  exported: "#4ade80",
  strategicStockpile: "#111827",
} as const;

export const RESOURCE_TICKER_BACKGROUNDS = {
  default: "rgba(255, 255, 255, 0.85)",
  soonToBeObsolete: "#f4f4f5",
  demandNotMet: "#fee2e2",
} as const;

export type ResourceGrade = "none" | "bronze" | "silver" | "gold";

export function getResourceTickerBackground(args: { soonToBeObsolete?: boolean; demandNotMet?: boolean }): string {
  if (args.demandNotMet) return RESOURCE_TICKER_BACKGROUNDS.demandNotMet;
  if (args.soonToBeObsolete) return RESOURCE_TICKER_BACKGROUNDS.soonToBeObsolete;
  return RESOURCE_TICKER_BACKGROUNDS.default;
}
