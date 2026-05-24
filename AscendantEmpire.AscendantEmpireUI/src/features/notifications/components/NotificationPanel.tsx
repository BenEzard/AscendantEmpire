import { useMemo } from "react";
import type { NotificationGroup, NotificationItem, NotificationGroupType } from "../types";
import NotificationGroupView from "./NotificationGroup";

export interface NotificationPanelProps {
  open: boolean;
  topPx: number;
  groups: NotificationGroup[];
  expandedGroups: ReadonlySet<NotificationGroupType>;
  onToggleGroup: (groupType: NotificationGroupType) => void;
  onClose: () => void;
  onClickItem: (item: NotificationItem) => void;
}

export default function NotificationPanel({ open, topPx, groups, expandedGroups, onToggleGroup, onClose, onClickItem }: NotificationPanelProps) {
  const panelStyle = useMemo(
    () => ({
      position: "fixed" as const,
      top: topPx,
      right: 0,
      height: `calc(100vh - ${topPx}px)`,
      width: "20vw",
      zIndex: 1060,
    }),
    [topPx],
  );

  return (
    <div
      style={panelStyle}
      className={[
        "bg-white text-black shadow-xl border-l border-slate-200",
        "flex flex-col",
        "transition-transform duration-200 ease-out",
        open ? "translate-x-0" : "translate-x-full pointer-events-none",
      ].join(" ")}
      aria-hidden={!open}>
      <div className="flex items-center justify-between gap-3 border-b border-slate-200 px-4 py-3">
        <div className="text-base font-semibold">Notifications</div>
        <button type="button" className="rounded-md px-2 py-1 text-slate-600 hover:bg-slate-100" aria-label="Close" onClick={onClose}>
          ×
        </button>
      </div>

      <div className="flex-1 overflow-y-auto px-4 py-3">
        <div className="flex flex-col gap-2">
          {groups.map((g) => (
            <NotificationGroupView
              key={g.type}
              group={g}
              expanded={expandedGroups.has(g.type)}
              onToggle={() => onToggleGroup(g.type)}
              onClickItem={onClickItem}
            />
          ))}
        </div>
      </div>
    </div>
  );
}
