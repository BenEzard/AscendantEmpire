import { useCallback, useEffect, useMemo, useState } from "react";
import type { NotificationGroupType, NotificationItem } from "./types";
import { useNotifications } from "./useNotifications";
import NotificationButton from "./components/NotificationButton";
import NotificationPanel from "./components/NotificationPanel";

export interface NotificationsHostProps {
  toolbarBottomPx: number;
  onPanTo: (x: number, y: number) => void;
  onOpenDialog: (dialog: "unit-orders" | "trade" | "city" | "research", payload?: unknown) => void;
  onPanelOpenChange?: (open: boolean) => void;
}

export default function NotificationsHost({ toolbarBottomPx, onPanTo, onOpenDialog, onPanelOpenChange }: NotificationsHostProps) {
  const { groups, totalCount } = useNotifications();
  const [panelOpen, setPanelOpen] = useState(false);
  const [expandedGroups, setExpandedGroups] = useState<Set<NotificationGroupType>>(() => new Set());

  const visible = totalCount > 0;

  // If everything is handled/removed, the panel should not remain visible.
  useEffect(() => {
    if (totalCount === 0) setPanelOpen(false);
  }, [totalCount]);

  // Default expand all groups when panel opens for the first time.
  useEffect(() => {
    if (!panelOpen) return;
    setExpandedGroups((prev) => {
      if (prev.size > 0) return prev;
      return new Set(groups.map((g) => g.type));
    });
  }, [groups, panelOpen]);

  useEffect(() => {
    onPanelOpenChange?.(panelOpen);
  }, [panelOpen, onPanelOpenChange]);

  const togglePanel = useCallback(() => setPanelOpen((v) => !v), []);
  const closePanel = useCallback(() => setPanelOpen(false), []);

  const onToggleGroup = useCallback((groupType: NotificationGroupType) => {
    setExpandedGroups((prev) => {
      const next = new Set(prev);
      if (next.has(groupType)) next.delete(groupType);
      else next.add(groupType);
      return next;
    });
  }, []);

  const topForButton = useMemo(() => Math.round(toolbarBottomPx + 8), [toolbarBottomPx]);
  const topForPanel = useMemo(() => Math.round(toolbarBottomPx), [toolbarBottomPx]);

  const onClickItem = useCallback(
    (item: NotificationItem) => {
      if (item.location) onPanTo(item.location.x, item.location.y);
      if (item.action?.kind === "open-dialog") onOpenDialog(item.action.dialog, item.action.payload);
    },
    [onOpenDialog, onPanTo],
  );

  return (
    <>
      <NotificationButton visible={visible} count={totalCount} topPx={topForButton} onClick={togglePanel} />
      <NotificationPanel
        open={panelOpen && visible}
        topPx={topForPanel}
        groups={groups}
        expandedGroups={expandedGroups}
        onToggleGroup={onToggleGroup}
        onClose={closePanel}
        onClickItem={onClickItem}
      />
    </>
  );
}
