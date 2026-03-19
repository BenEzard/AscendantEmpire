export const RESOURCE_INDICATOR_COLORS = {
  inactive: "#9ca3af",
  imported: "#60a5fa",
  exported: "#4ade80",
  strategicStockpile: "#111827",
} as const;

export const RESOURCE_TICKER_BACKGROUNDS = {
  default: "rgba(255, 255, 255, 0.85)",
  soonToBeObsolete: "#f4f4f5",
  demandNotMet: "#fee2e2",
} as const;

export const MANUFACTURED_RESOURCE_GRADES = ["bronze", "silver", "gold"] as const;

export type ManufacturedResourceGrade = (typeof MANUFACTURED_RESOURCE_GRADES)[number];

export function getResourceTickerBackground(args: { soonToBeObsolete?: boolean; demandNotMet?: boolean }): string {
  if (args.demandNotMet) return RESOURCE_TICKER_BACKGROUNDS.demandNotMet;
  if (args.soonToBeObsolete) return RESOURCE_TICKER_BACKGROUNDS.soonToBeObsolete;
  return RESOURCE_TICKER_BACKGROUNDS.default;
}
