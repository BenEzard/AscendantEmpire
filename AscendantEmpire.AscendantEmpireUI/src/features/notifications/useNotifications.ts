import { createContext, createElement, useCallback, useContext, useMemo, useState, type ReactNode } from "react";
import type { NotificationGroup, NotificationGroupType, NotificationItem } from "./types";
import { notificationGroupTypeMeta } from "./types";
import { dummyNotificationGroups } from "./dummyNotifications";

function groupUnhandedItems(group: NotificationGroup): NotificationItem[] {
  return group.items.filter((i) => !i.isHandled);
}

export interface UseNotificationsResult {
  groups: NotificationGroup[];
  totalCount: number;
  removeItem: (id: string) => void;
  markHandled: (id: string) => void;
  clearAll: () => void;
}

const NotificationsContext = createContext<UseNotificationsResult | null>(null);

function useNotificationsState(): UseNotificationsResult {
  const [groups, setGroups] = useState<NotificationGroup[]>(() => dummyNotificationGroups);

  const removeItem = useCallback((id: string) => {
    setGroups((prev) =>
      prev
        .map((g) => ({ ...g, items: g.items.filter((i) => i.id !== id) }))
        .filter((g) => g.items.length > 0),
    );
  }, []);

  const markHandled = useCallback((id: string) => {
    setGroups((prev) =>
      prev
        .map((g) => ({ ...g, items: g.items.map((i) => (i.id === id ? { ...i, isHandled: true } : i)) }))
        .map((g) => ({ ...g, items: groupUnhandedItems(g) }))
        .filter((g) => g.items.length > 0),
    );
  }, []);

  const clearAll = useCallback(() => setGroups([]), []);

  const normalizedGroups = useMemo(() => {
    const filtered = groups
      .map((g) => ({ ...g, items: groupUnhandedItems(g) }))
      .filter((g) => g.items.length > 0);

    filtered.sort((a, b) => {
      const aMeta = notificationGroupTypeMeta[a.type];
      const bMeta = notificationGroupTypeMeta[b.type];
      const orderDiff = (aMeta?.displayOrder ?? 0) - (bMeta?.displayOrder ?? 0);
      if (orderDiff !== 0) return orderDiff;
      return a.label.localeCompare(b.label);
    });

    return filtered;
  }, [groups]);

  const totalCount = useMemo(() => normalizedGroups.reduce((sum, g) => sum + g.items.length, 0), [normalizedGroups]);

  // Safety: ensure unique group types (your requirement: one group per type)
  // If duplicates ever appear (e.g. bad API merge), later groups win.
  const uniqueByType = useMemo(() => {
    const byType = new Map<NotificationGroupType, NotificationGroup>();
    for (const g of normalizedGroups) byType.set(g.type, g);
    return Array.from(byType.values());
  }, [normalizedGroups]);

  return {
    groups: uniqueByType,
    totalCount,
    removeItem,
    markHandled,
    clearAll,
  };
}

export function NotificationsProvider({ children }: { children: ReactNode }) {
  const value = useNotificationsState();
  return createElement(NotificationsContext.Provider, { value }, children);
}

export function useNotifications(): UseNotificationsResult {
  const ctx = useContext(NotificationsContext);
  if (!ctx) throw new Error("useNotifications must be used within <NotificationsProvider>");
  return ctx;
}
