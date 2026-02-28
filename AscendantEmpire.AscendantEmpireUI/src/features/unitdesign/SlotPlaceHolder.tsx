import type { UnitComponentType } from "./types/UnitComponentType";
import type { ReactNode } from "react";

export interface SlotPlaceHolderProps {
  x: number;
  y: number;
  type: UnitComponentType;
  text: string;
  iconUrl?: string | null;
  icon?: ReactNode;
  name?: string | null;
  componentId: number;
  onClick?: (args: { componentId: number; type: UnitComponentType }) => void;
}

const typeTintClass: Partial<Record<UnitComponentType, string>> = {
  weapon: "ring-2 ring-red-400/50 shadow-[0_0_0_4px_rgba(248,113,113,0.15)]",
  armour: "ring-2 ring-green-400/50 shadow-[0_0_0_4px_rgba(248,113,113,0.15)]",
  engine: "ring-2 ring-yellow-400/50 shadow-[0_0_0_4px_rgba(248,113,113,0.15)]",
};

export default function SlotPlaceHolder({ x, y, type, text, iconUrl, icon, name, componentId, onClick }: SlotPlaceHolderProps) {
  const isSelected = (name?.trim()?.length ?? 0) > 0;
  return (
    <button
      type="button"
      className={
        "absolute rounded border border-white/20 bg-white/10 p-2 text-lg font-semibold text-white " +
        "hover:bg-white/15 focus:outline-none focus-visible:ring-2 focus-visible:ring-white/30 " +
        (isSelected ? " flex h-20 w-36 flex-col items-center justify-between " : " ") +
        (typeTintClass[type] ?? "")
      }
      style={{
        left: x,
        top: y,
        transform: "translate(-50%, -50%)",
      }}
      onClick={() => onClick?.({ componentId, type })}>
      {isSelected ? (
        <>
          <div className="flex w-full flex-1 items-center justify-center">
            {icon ? (
              icon
            ) : iconUrl ? (
              <img src={iconUrl} alt={name ?? ""} className="max-h-10 w-auto select-none object-contain" draggable={false} />
            ) : null}
          </div>
          <div className="w-full text-center text-xs font-semibold leading-tight text-white/90">{name}</div>
        </>
      ) : (
        <div className="text-center">{text}</div>
      )}
    </button>
  );
}
