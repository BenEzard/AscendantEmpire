import type { MouseEventHandler } from "react";

interface SliderButtonProps {
  value: number; // 0–100, displayed as label
  leftPx: number; // computed pixel offset from groove left edge
  onMouseDown: MouseEventHandler<HTMLDivElement>;
  locked?: boolean;
}

export default function SliderButton({ value, leftPx, onMouseDown, locked = false }: SliderButtonProps) {
  return (
    <div
      className={`absolute top-1/2 z-10 w-10 -translate-x-1/2 -translate-y-1/2 select-none rounded py-0.5 text-center text-xs font-semibold whitespace-nowrap text-white ${
        locked ? "cursor-not-allowed bg-slate-400" : "cursor-grab bg-slate-700 active:cursor-grabbing"
      }`}
      style={{ left: leftPx }}
      onMouseDown={locked ? undefined : onMouseDown}>
      {value}%
    </div>
  );
}
