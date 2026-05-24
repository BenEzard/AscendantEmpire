import type { ReactNode } from "react";
import type { NotificationItem } from "../../types";

interface Props {
  item: NotificationItem;
  onClickItem: (item: NotificationItem) => void;
  children: ReactNode;
}

export default function NotificationItemShell({ item, onClickItem, children }: Props) {
  return (
    <button type="button" className="w-full rounded-md px-2 py-2 text-left hover:bg-slate-50" onClick={() => onClickItem(item)}>
      {children}
    </button>
  );
}
