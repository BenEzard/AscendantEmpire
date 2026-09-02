import { useMemo } from "react";
import type { PlayerCampaignMapObserverResultDto } from "@/features/map/api/playerCampaignMapApi";

/**
 * The `observerRecordType` of the whole-map pseudo-observer, which is `RecordType.None` on the API.
 *
 * The map read hands back terrain the player explored earlier under a filler observer that stands nowhere and watches nothing —
 * it exists only to carry remembered ground in the same shape as the real observers. It must not light those tiles as currently
 * seen, or the live layer would cover the whole explored map and the two fog layers would collapse back into one.
 */
const WHOLE_MAP_OBSERVER_RECORD_TYPE = 0;

/**
 * Derives the live half of the fog of war: every tile some force or structure of the player's is watching right now.
 *
 * The other half — what the player has explored — is not here. It belongs to the tiles themselves, as `isExplored`, because it
 * is permanent and per-tile; this set is the opposite in both respects. It is replaced wholesale every time the server describes
 * the player's view, so ground a force has stopped watching drops out of it and falls back under haze.
 *
 * The observers come from the tiles cache, which both the map read and a force step write to. That is deliberate: the fog has
 * one source of truth, and neither path can leave the map showing what the other last saw.
 */
export function useFogOfWar(observers: PlayerCampaignMapObserverResultDto[] | undefined) {
  /**
   * The `"x,y"` key of every tile currently in sight.
   *
   * Memoised on the observers alone. The set is handed to every memoised tile through `getTileStyle`, so a fresh one on each
   * render would re-render the whole grid whenever anything unrelated on the page changed.
   */
  const observedTileKeys = useMemo(() => {
    const keys = new Set<string>();

    for (const observer of observers ?? []) {
      // The filler observer's tiles are remembered terrain, not live sight.
      if (observer.observerRecordType === WHOLE_MAP_OBSERVER_RECORD_TYPE) {
        continue;
      }

      for (const tile of observer.seenTiles ?? []) {
        keys.add(`${tile.xCoord},${tile.yCoord}`);
      }
    }

    return keys;
  }, [observers]);

  return { observedTileKeys };
}
