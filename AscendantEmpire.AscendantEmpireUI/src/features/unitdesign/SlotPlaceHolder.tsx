import type { ComponentType } from "./types/UnitComponent";
import type { ReactNode } from "react";

export interface SlotPlaceHolderProps {
  x: number;
  y: number;
  /** Index of this slot in the base body's `slots` array; -1 for the central base slot itself. */
  slotIndex: number;
  /** Representative component type for tinting (the base slot uses "Body"). */
  componentType?: ComponentType;
  text: string;
  iconUrl?: string | null;
  icon?: ReactNode;
  name?: string | null;
  /** Whether the slot responds to clicks (false in read-only view mode). */
  interactive?: boolean;
  onClick?: (args: { slotIndex: number }) => void;
}

// Tint keyed by canonical ComponentType so weapons/armour/engines read distinctly on the canvas.
const typeTintClass: Partial<Record<ComponentType, string>> = {
  Weapon: "ring-2 ring-red-400/50 shadow-[0_0_0_4px_rgba(248,113,113,0.15)]",
  Armour: "ring-2 ring-green-400/50 shadow-[0_0_0_4px_rgba(74,222,128,0.15)]",
  Engine: "ring-2 ring-yellow-400/50 shadow-[0_0_0_4px_rgba(250,204,21,0.15)]",
};

export default function SlotPlaceHolder({
  x,
  y,
  componentType,
  text,
  iconUrl,
  icon,
  name,
  slotIndex,
  interactive = true,
  onClick,
}: SlotPlaceHolderProps) {
  const isSelected = (name?.trim()?.length ?? 0) > 0;
  const isBase = componentType === "Body";

  return (
    <button
      type="button"
      disabled={!interactive}
      className={
        "absolute rounded border border-white/20 bg-white/10 p-2 text-lg font-semibold text-white " +
        "hover:bg-white/15 focus:outline-none focus-visible:ring-2 focus-visible:ring-white/30 " +
        (interactive ? "" : "cursor-default ") +
        (isSelected ? " flex h-20 w-36 flex-col items-center justify-between " : " ") +
        (typeTintClass[componentType ?? "Extension"] ?? "")
      }
      style={{
        left: x,
        top: y,
        transform: "translate(-50%, -50%)",
      }}
      onClick={() => onClick?.({ slotIndex })}>
      {isSelected ? (
        isBase ? (
          <div className="flex h-full w-full items-center justify-center text-center text-xs font-semibold leading-tight text-white/90">{name}</div>
        ) : (
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
        )
      ) : (
        <div className="text-center">{text}</div>
      )}
    </button>
  );
}
