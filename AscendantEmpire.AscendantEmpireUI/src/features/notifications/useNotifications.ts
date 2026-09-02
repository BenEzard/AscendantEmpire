import { createContext, createElement, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from "react";
import type { PossibleAction } from "@/features/game/api/turnApi";
import type { NotificationGroup, NotificationGroupType } from "./types";
import { notificationGroupTypeMeta } from "./types";
import { buildEndTurnActionGroups } from "./endTurnActionNotifications";

export interface UseNotificationsResult {
  groups: NotificationGroup[];
  totalCount: number;
  /** Dismiss one item outright. */
  removeItem: (id: string) => void;
  /** Mark one item dealt with. Handled and removed amount to the same thing here: the item leaves the panel. */
  markHandled: (id: string) => void;
  clearAll: () => void;
}

const NotificationsContext = createContext<UseNotificationsResult | null>(null);

export interface NotificationsProviderProps {
  /**
   * The actions the server last reported as outstanding when the player asked to end their turn.
   *
   * The provider derives its groups from this rather than owning them, so the caller that made the request stays the single source of truth for what
   * the server said — and clearing it (an ended turn, a closed game) empties the panel with it.
   */
  endTurnActions: PossibleAction[];
  children: ReactNode;
}

function useNotificationsState(endTurnActions: PossibleAction[]): UseNotificationsResult {
  // Items the player has dealt with or waved away. Held apart from the groups because the groups are derived, so there is nowhere in them to record it.
  const [dismissedIds, setDismissedIds] = useState<ReadonlySet<string>>(() => new Set());

  // A fresh check makes previous dismissals meaningless: they were judgements about the last list, and anything still outstanding has just been
  // reported again. Without this an item waved away once would stay invisible for the rest of the game.
  useEffect(() => {
    setDismissedIds(new Set());
  }, [endTurnActions]);

  const removeItem = useCallback((id: string) => {
    setDismissedIds((prev) => new Set(prev).add(id));
  }, []);

  // Handling an item and removing it come to the same thing while every notification is an outstanding action: either way it stops asking.
  const markHandled = removeItem;

  const normalizedGroups = useMemo(() => {
    const filtered = buildEndTurnActionGroups(endTurnActions)
      .map((group) => ({ ...group, items: group.items.filter((item) => !item.isHandled && !dismissedIds.has(item.id)) }))
      .filter((group) => group.items.length > 0);

    filtered.sort((a, b) => {
      const aMeta = notificationGroupTypeMeta[a.type];
      const bMeta = notificationGroupTypeMeta[b.type];
      const orderDiff = (aMeta?.displayOrder ?? 0) - (bMeta?.displayOrder ?? 0);
      if (orderDiff !== 0) return orderDiff;
      return a.label.localeCompare(b.label);
    });

    return filtered;
  }, [endTurnActions, dismissedIds]);

  const clearAll = useCallback(() => {
    setDismissedIds(new Set(normalizedGroups.flatMap((group) => group.items.map((item) => item.id))));
  }, [normalizedGroups]);

  const totalCount = useMemo(() => normalizedGroups.reduce((sum, group) => sum + group.items.length, 0), [normalizedGroups]);

  // Safety: ensure unique group types (one group per type). If duplicates ever appear, later groups win.
  const uniqueByType = useMemo(() => {
    const byType = new Map<NotificationGroupType, NotificationGroup>();
    for (const group of normalizedGroups) {
      byType.set(group.type, group);
    }
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

export function NotificationsProvider({ endTurnActions, children }: NotificationsProviderProps) {
  const value = useNotificationsState(endTurnActions);
  return createElement(NotificationsContext.Provider, { value }, children);
}

export function useNotifications(): UseNotificationsResult {
  const ctx = useContext(NotificationsContext);
  if (!ctx) throw new Error("useNotifications must be used within <NotificationsProvider>");
  return ctx;
}
