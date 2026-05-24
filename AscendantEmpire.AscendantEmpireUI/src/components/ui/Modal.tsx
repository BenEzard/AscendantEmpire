import { useEffect, type ReactNode } from "react";
import { createPortal } from "react-dom";

export interface ModalProps {
  open: boolean;
  title?: ReactNode;
  onClose: () => void;
  children: ReactNode;
  footer?: ReactNode;
  size?: "md" | "lg" | "xl";
  panelClassName?: string;
}

function sizeClass(size: NonNullable<ModalProps["size"]>): string {
  switch (size) {
    case "xl":
      return "max-w-6xl";
    case "lg":
      return "max-w-4xl";
    case "md":
    default:
      return "max-w-2xl";
  }
}

export default function Modal({ open, title, onClose, children, footer, size = "md", panelClassName }: ModalProps) {
  useEffect(() => {
    if (!open) return;

    const onKeyDown = (e: KeyboardEvent) => {
      if (e.key === "Escape") onClose();
    };

    document.addEventListener("keydown", onKeyDown);
    return () => document.removeEventListener("keydown", onKeyDown);
  }, [open, onClose]);

  if (!open) return null;

  return createPortal(
    <div className="fixed inset-0 z-[1050] flex items-center justify-center p-4">
      <div className="absolute inset-0 bg-black/40" onMouseDown={onClose} />

      <div
        className={["relative w-full", sizeClass(size), "rounded-lg bg-white text-black shadow-xl", panelClassName ?? ""].filter(Boolean).join(" ")}
        onMouseDown={(e) => e.stopPropagation()}>
        <div className="flex items-center justify-between gap-3 border-b border-slate-200 px-4 py-3">
          <div className="text-base font-semibold">{title}</div>
          <button type="button" className="rounded-md px-2 py-1 text-slate-600 hover:bg-slate-100" aria-label="Close" onClick={onClose}>
            ×
          </button>
        </div>

        <div className="px-4 py-3">{children}</div>

        {footer != null && <div className="flex items-center justify-end gap-2 border-t border-slate-200 px-4 py-3">{footer}</div>}
      </div>
    </div>,
    document.body,
  );
}
