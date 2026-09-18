import { SUPPORT_FORECAST_TURNS } from "@/features/map/api/tileSupportApi";
import type { TileSupportLevel } from "@/features/map/api/tileSustenanceApi";
import { SUPPORT_LEVEL_LABELS } from "./tileSupportLevelClass";

/**
 * The turns a tile can feed the forces, as the hover marker shows them: the digit, then `5+` once the tile outlasts the forecast.
 *
 * `undefined` (the server didn't say) shows nothing, leaving the marker as a plain coloured circle.
 */
export function formatSupportTurns(turns: number | null | undefined): string {
  if (turns === undefined) {
    return "";
  }

  return turns === null ? `${SUPPORT_FORECAST_TURNS}+` : String(turns);
}

/**
 * How the marker reads to the player, for its accessible name: the turns where there are some, otherwise the level's own wording.
 *
 * With no whole turn the level says more than a bare 0 would — "Cannot support the whole force" and "No food left" are different situations.
 */
export function supportTurnsLabel(supportLevel: TileSupportLevel, turns: number | null | undefined): string {
  if (turns === null) {
    return `Can support for more than ${SUPPORT_FORECAST_TURNS} turns`;
  }

  if (turns === undefined || turns === 0) {
    return SUPPORT_LEVEL_LABELS[supportLevel];
  }

  return `Can support for ${turns} ${turns === 1 ? "turn" : "turns"}`;
}
