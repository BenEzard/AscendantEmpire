import type { TileSupportLevel } from "@/features/map/api/tileSustenanceApi";

/**
 * The CSS modifier for each support level, shared by the tile toolbar and the hover marker so a level is always drawn in the same colour in both. The colours
 * themselves live in index.css.
 */
const SUPPORT_LEVEL_MODIFIERS: Record<TileSupportLevel, string> = {
  Plenty: "plenty",
  OneTurn: "one-turn",
  Insufficient: "insufficient",
  Exhausted: "exhausted",
};

/** The class for a support level under a block, e.g. `tile-toolbar--support-one-turn`; null when there is no level to show. */
export function tileSupportLevelClass(block: string, supportLevel: TileSupportLevel | null | undefined): string | null {
  return supportLevel == null ? null : `${block}--support-${SUPPORT_LEVEL_MODIFIERS[supportLevel]}`;
}

/** How each level reads to the player, for the marker's accessible name and tooltip. */
export const SUPPORT_LEVEL_LABELS: Record<TileSupportLevel, string> = {
  Plenty: "Can support for 2+ turns",
  OneTurn: "Can support for 1 turn",
  Insufficient: "Cannot support the whole force",
  Exhausted: "No food left",
};
