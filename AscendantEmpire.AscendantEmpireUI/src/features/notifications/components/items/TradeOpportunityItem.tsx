import type { NotificationItem } from "../../types";
import NotificationItemShell from "./NotificationItemShell";

interface Props {
  item: NotificationItem;
  onClickItem: (item: NotificationItem) => void;
}

export default function TradeOpportunityItem({ item, onClickItem }: Props) {
  return (
    <NotificationItemShell item={item} onClickItem={onClickItem}>
      <div className="flex items-center gap-2">
        <div className="w-7 h-7 flex items-center justify-center flex-none">
          {item.iconSrc && <img src={item.iconSrc} alt={item.iconAlt ?? ""} className="block w-7 h-7 object-contain" draggable={false} />}
        </div>
        <div className="min-w-0 flex-1">
          <div className="text-sm text-slate-900">{item.label}</div>
          <div className="text-xs text-emerald-600 font-medium">Trade available</div>
        </div>
      </div>
    </NotificationItemShell>
  );
}
