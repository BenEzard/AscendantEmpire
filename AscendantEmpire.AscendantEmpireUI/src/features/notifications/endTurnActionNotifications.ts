import type { PossibleAction } from "@/features/game/api/turnApi";
import type { NotificationGroup, NotificationGroupType, NotificationItem } from "./types";
import { notificationGroupTypeMeta } from "./types";
import unitIconSrc from "@/assets/units/centurion_40px.png";
import sandstoneBlockIconSrc from "@/assets/stone/sandstone-block_40px.png";

// RecordType's numeric values, which the API sends as `recordTypeId`. They are persisted on the server, so they can be relied on not to move.
const RECORD_TYPE_UNIT = 1;
const RECORD_TYPE_COMMAND = 2;
const RECORD_TYPE_STRUCTURE = 3;

/**
 * Works out which notification group an outstanding action belongs in.
 *
 * The notification groups are named for the *reason* something wants attention, while the API describes an action by its broad category and the kind
 * of record it concerns — so the two have to be reconciled here. A city under siege, for instance, is a notification but never an end-turn action,
 * which is why the groups are not simply the API's four categories.
 */
function groupTypeFor(action: PossibleAction): NotificationGroupType {
  switch (action.category) {
    case "Technology":
      return "research-selection";

    case "Diplomacy":
      return "trade-opportunity";

    case "Military":
    case "DomesticManagement":
    default:
      // Structures under military attention are the siege case; anything else military is a Force — a Unit or a Command — that still has orders to give.
      return action.recordTypeId === RECORD_TYPE_STRUCTURE ? "city-under-siege" : "unit-needs-orders";
  }
}

function iconFor(groupType: NotificationGroupType): { iconSrc: string; iconAlt: string } {
  switch (groupType) {
    case "unit-needs-orders":
      return { iconSrc: unitIconSrc, iconAlt: "Unit" };

    case "city-under-siege":
      return { iconSrc: sandstoneBlockIconSrc, iconAlt: "City" };

    default:
      return { iconSrc: sandstoneBlockIconSrc, iconAlt: "Resource" };
  }
}

/**
 * Turns the actions the server reported as outstanding into notification groups.
 *
 * The API returns a flat list; the panel shows one group per kind, in a fixed display order. Groups with nothing in them are dropped rather than
 * rendered empty, so a player with only movement left to do sees one group rather than four.
 */
export function buildEndTurnActionGroups(actions: PossibleAction[]): NotificationGroup[] {
  const byType = new Map<NotificationGroupType, NotificationItem[]>();

  for (const action of actions) {
    const groupType = groupTypeFor(action);
    const { iconSrc, iconAlt } = iconFor(groupType);

    const item: NotificationItem = {
      // The record id alone is not unique — a Unit and a Structure can share one, and one Force can raise more than one kind of action — so the id
      // carries the group and record type too. It also has to be stable across re-checks, or dismissing an item would not stick.
      id: `end-turn:${groupType}:${action.recordTypeId}:${action.id}`,
      label: action.description,
      iconSrc,
      iconAlt,
      location: action.xCoord != null && action.yCoord != null ? { x: action.xCoord, y: action.yCoord } : undefined,
      // Clicking pans to the tile; giving the Force its orders is done on the map, so there is no dialog to open yet.
      action: { kind: "none" },
      isHandled: false,
    };

    const existing = byType.get(groupType);
    if (existing) {
      existing.push(item);
    } else {
      byType.set(groupType, [item]);
    }
  }

  const groups: NotificationGroup[] = [];

  for (const [groupType, items] of byType) {
    groups.push({
      type: groupType,
      label: notificationGroupTypeMeta[groupType].label,
      items,
    });
  }

  return groups;
}

// Re-exported so the record type constants have one home, for whoever next needs to tell a Force from a Structure on the client.
export { RECORD_TYPE_UNIT, RECORD_TYPE_COMMAND, RECORD_TYPE_STRUCTURE };
