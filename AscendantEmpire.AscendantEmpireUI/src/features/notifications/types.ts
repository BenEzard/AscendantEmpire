export type NotificationGroupType = "unit-needs-orders" | "trade-opportunity" | "city-under-siege" | "research-selection";

export interface NotificationGroupTypeMeta {
  label: string;
  displayOrder: number;
}

export const notificationGroupTypeMeta: Record<NotificationGroupType, NotificationGroupTypeMeta> = {
  "unit-needs-orders": { label: "Unit needs orders", displayOrder: 10 },
  "city-under-siege": { label: "City Under Siege", displayOrder: 20 },
  "trade-opportunity": { label: "Trade Opportunities", displayOrder: 30 },
  "research-selection": { label: "Research", displayOrder: 40 },
};

export interface NotificationLocation {
  x: number;
  y: number;
}

export type NotificationItemAction =
  | { kind: "open-dialog"; dialog: "unit-orders" | "trade" | "city"; payload?: unknown }
  | { kind: "none" };

export interface NotificationItem {
  id: string;
  label: string;
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
