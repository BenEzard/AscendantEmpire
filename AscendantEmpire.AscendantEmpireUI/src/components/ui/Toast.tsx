import { useEffect, type ReactNode } from "react";

export type ToastVariant = "success" | "danger" | "warning" | "info";

export interface ToastProps {
  open: boolean;
  message: ReactNode;
  variant?: ToastVariant;
  onClose: () => void;
  autoHideMs?: number;
}

function variantClass(variant: ToastVariant): string {
  switch (variant) {
    case "danger":
      return "border-[var(--bs-danger)]/30 bg-[color-mix(in_srgb,var(--bs-danger)_12%,white)] text-slate-900";
    case "warning":
      return "border-[var(--bs-warning)]/40 bg-[color-mix(in_srgb,var(--bs-warning)_18%,white)] text-slate-900";
    case "info":
      return "border-[var(--bs-info)]/30 bg-[color-mix(in_srgb,var(--bs-info)_12%,white)] text-slate-900";
    case "success":
    default:
      return "border-[var(--bs-success)]/30 bg-[color-mix(in_srgb,var(--bs-success)_12%,white)] text-slate-900";
  }
}

export function ToastViewport({ children }: { children: ReactNode }) {
  return <div className="fixed bottom-3 right-3 z-[1100] flex flex-col gap-2">{children}</div>;
}

export default function Toast({ open, message, variant = "success", onClose, autoHideMs = 3000 }: ToastProps) {
  useEffect(() => {
    if (!open) return;
    const t = window.setTimeout(() => onClose(), autoHideMs);
    return () => window.clearTimeout(t);
  }, [open, onClose, autoHideMs]);

  if (!open) return null;

  return (
    <div className={["min-w-[240px] max-w-[420px] rounded-md border px-3 py-2 text-sm shadow-lg", variantClass(variant)].join(" ")} role="status">
      <div className="flex items-start justify-between gap-3">
        <div className="flex-1">{message}</div>
        <button type="button" className="-mr-1 rounded px-1 text-slate-600 hover:bg-black/5" aria-label="Close" onClick={onClose}>
          ×
        </button>
      </div>
    </div>
  );
}
