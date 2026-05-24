import { useMemo } from "react";
import type { ComponentType } from "react";
import type { NotificationGroup, NotificationGroupType, NotificationItem } from "../types";
import { FaChevronDown, FaChevronRight } from "react-icons/fa6";
import UnitNeedsOrdersItem from "./items/UnitNeedsOrdersItem";
import CityUnderSiegeItem from "./items/CityUnderSiegeItem";
import TradeOpportunityItem from "./items/TradeOpportunityItem";

interface NotificationItemProps {
  item: NotificationItem;
  onClickItem: (item: NotificationItem) => void;
}

const itemComponentMap: Record<NotificationGroupType, ComponentType<NotificationItemProps>> = {
  "unit-needs-orders": UnitNeedsOrdersItem,
  "city-under-siege": CityUnderSiegeItem,
  "trade-opportunity": TradeOpportunityItem,
};

export interface NotificationGroupProps {
  group: NotificationGroup;
  expanded: boolean;
  onToggle: () => void;
  onClickItem: (item: NotificationItem) => void;
}

export default function NotificationGroup({ group, expanded, onToggle, onClickItem }: NotificationGroupProps) {
  const count = group.items.length;

  const headerText = useMemo(() => `${group.label} (${count})`, [group.label, count]);

  const ItemComponent = itemComponentMap[group.type];

  return (
    <div className="border border-slate-200 rounded-md overflow-hidden bg-white">
      <button
        type="button"
        className="w-full flex items-center gap-3 px-3 py-2 bg-stone-50 hover:bg-stone-100 text-left"
        onClick={onToggle}
        aria-expanded={expanded}>
        <div className="w-6 h-6 flex items-center justify-center flex-none">
          {group.iconSrc && <img src={group.iconSrc} alt={group.iconAlt ?? ""} className="block w-6 h-6 object-contain" draggable={false} />}
        </div>
        <span className="flex-1 font-medium text-slate-800">{headerText}</span>
        <span className="text-slate-600 flex-none">{expanded ? <FaChevronDown /> : <FaChevronRight />}</span>
      </button>

      {expanded && (
        <div className="px-3 py-2">
          <div className="flex flex-col gap-1">
            {group.items.map((item) => (
              <ItemComponent key={item.id} item={item} onClickItem={onClickItem} />
            ))}
          </div>
        </div>
      )}
    </div>
  );
}
