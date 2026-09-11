import type { NotificationItem } from "../../types";
import NotificationItemShell from "./NotificationItemShell";

interface Props {
  item: NotificationItem;
  onClickItem: (item: NotificationItem) => void;
}

/** One intelligence report: the composed sentence, with the round it happened in beneath it. */
export default function SightingReportItem({ item, onClickItem }: Props) {
  return (
    <NotificationItemShell item={item} onClickItem={onClickItem}>
      <div className="flex items-start gap-2">
        <div className="w-7 h-7 flex items-center justify-center flex-none">
          {item.iconSrc && <img src={item.iconSrc} alt={item.iconAlt ?? ""} className="block w-7 h-7 object-contain" draggable={false} />}
        </div>
        <div className="min-w-0">
          <div className="text-sm text-slate-900">{item.label}</div>
          {item.detail && <div className="text-xs text-slate-500">{item.detail}</div>}
        </div>
      </div>
    </NotificationItemShell>
  );
}
