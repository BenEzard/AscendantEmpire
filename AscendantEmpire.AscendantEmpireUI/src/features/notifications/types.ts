export type NotificationGroupType = "unit-needs-orders" | "sighting-reports" | "trade-opportunity" | "city-under-siege" | "research-selection";

export interface NotificationGroupTypeMeta {
  label: string;
  displayOrder: number;
}

export const notificationGroupTypeMeta: Record<NotificationGroupType, NotificationGroupTypeMeta> = {
  "unit-needs-orders": { label: "Unit needs orders", displayOrder: 10 },
  // Straight after orders: what other players were seen doing is the other thing a player needs before deciding their own moves.
  "sighting-reports": { label: "Sightings", displayOrder: 15 },
  "city-under-siege": { label: "City Under Siege", displayOrder: 20 },
  "trade-opportunity": { label: "Trade Opportunities", displayOrder: 30 },
  "research-selection": { label: "Research", displayOrder: 40 },
};

export interface NotificationLocation {
  x: number;
  y: number;
}

export type NotificationItemAction =
  | { kind: "open-dialog"; dialog: "unit-orders" | "trade" | "city" | "research"; payload?: unknown }
  | { kind: "none" };

export interface NotificationItem {
  id: string;
  label: string;
  /** A secondary line beneath the label — the round a sighting happened in, say. */
  detail?: string;
  iconSrc?: string;
  iconAlt?: string;
  location?: NotificationLocation;
  action?: NotificationItemAction;
  isHandled: boolean;
}

export interface NotificationGroup {
  type: NotificationGroupType;
  label: string;
  iconSrc?: string;
  iconAlt?: string;
  items: NotificationItem[];
}
