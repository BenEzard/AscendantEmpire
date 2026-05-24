import type { NotificationItem } from "../../types";
import NotificationItemShell from "./NotificationItemShell";

interface Props {
  item: NotificationItem;
  onClickItem: (item: NotificationItem) => void;
}

export default function CityUnderSiegeItem({ item, onClickItem }: Props) {
  return (
    <NotificationItemShell item={item} onClickItem={onClickItem}>
      <div className="flex items-center gap-2">
        <div className="w-7 h-7 flex items-center justify-center flex-none">
          {item.iconSrc && <img src={item.iconSrc} alt={item.iconAlt ?? ""} className="block w-7 h-7 object-contain" draggable={false} />}
        </div>
        <div className="min-w-0 flex-1">
          <div className="flex items-center gap-2">
            <span className="text-sm font-medium text-slate-900">{item.label}</span>
            <span className="text-xs font-semibold uppercase tracking-wide text-red-600">Siege</span>
          </div>
          {item.location && (
            <div className="text-xs text-slate-500">
              ({item.location.x}, {item.location.y})
            </div>
          )}
        </div>
      </div>
    </NotificationItemShell>
  );
}
