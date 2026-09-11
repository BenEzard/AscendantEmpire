import type { SightingReport } from "@/features/intelligence/api/intelligenceApi";
import { describeSightingReport, type SightingDescriptionContext } from "@/features/intelligence/describeSightingReport";
import type { NotificationGroup, NotificationItem } from "./types";
import { notificationGroupTypeMeta } from "./types";
import woodenWatchTowerIconSrc from "@/assets/structures/wooden-watch-tower_40px.png";

/**
 * Turns the player's intelligence feed into the notifications panel's "Sightings" group.
 *
 * Newest first, the way a feed reads, although the API lists them oldest first. Clicking one pans to the tile the force was last seen on, which is where the
 * player will want to look. Returns no group at all for an empty feed, so a quiet round adds nothing to the panel.
 */
export function buildSightingReportGroups(reports: readonly SightingReport[], context: SightingDescriptionContext): NotificationGroup[] {
  if (reports.length === 0) {
    return [];
  }

  const items: NotificationItem[] = [...reports].reverse().map((report) => {
    const lastSeen = report.path[report.path.length - 1];

    return {
      // The report id alone, prefixed so it can never collide with an end-turn item. Stable across refetches, so a dismissal sticks.
      id: `sighting:${report.reportId}`,
      label: describeSightingReport(report, context),
      detail: `Round ${report.roundNumber}`,
      iconSrc: woodenWatchTowerIconSrc,
      iconAlt: "Sighting",
      location: { x: lastSeen.x, y: lastSeen.y },
      action: { kind: "none" },
      isHandled: false,
    };
  });

  return [
    {
      type: "sighting-reports",
      label: notificationGroupTypeMeta["sighting-reports"].label,
      items,
    },
  ];
}
