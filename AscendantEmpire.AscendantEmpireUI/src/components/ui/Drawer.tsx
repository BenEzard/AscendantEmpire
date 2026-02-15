import { useEffect, type ReactNode } from "react";
import { createPortal } from "react-dom";

export interface DrawerProps {
  open: boolean;
  title?: string;
  onClose: () => void;
  children: ReactNode;
  side?: "left" | "right";
  widthClassName?: string;
}

export default function Drawer({ open, title, onClose, children, side = "left", widthClassName = "w-[340px]" }: DrawerProps) {
  useEffect(() => {
    if (!open) return;

    const onKeyDown = (e: KeyboardEvent) => {
      if (e.key === "Escape") onClose();
    };

    document.addEventListener("keydown", onKeyDown);
    return () => document.removeEventListener("keydown", onKeyDown);
  }, [open, onClose]);

  if (!open) return null;

  const sideClass = side === "left" ? "left-0" : "right-0";

  return createPortal(
    <div className="fixed inset-0 z-[1050]">
      <div className="absolute inset-0 bg-black/40" onMouseDown={onClose} />

      <div
        className={["absolute top-0 bottom-0", sideClass, widthClassName, "bg-white text-black shadow-xl", "flex flex-col"].join(" ")}
        onMouseDown={(e) => e.stopPropagation()}>
        <div className="flex items-center justify-between gap-3 border-b border-slate-200 px-4 py-3">
          <div className="text-base font-semibold">{title}</div>
          <button type="button" className="rounded-md px-2 py-1 text-slate-600 hover:bg-slate-100" aria-label="Close" onClick={onClose}>
            ×
          </button>
        </div>

        <div className="flex-1 overflow-auto px-4 py-3">{children}</div>
      </div>
    </div>,
    document.body,
  );
}
