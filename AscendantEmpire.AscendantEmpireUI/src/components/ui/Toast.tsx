import { useEffect, useLayoutEffect, useMemo, useRef, useState, type ReactNode } from "react";

export type ToastVariant = "success" | "danger" | "warning" | "info";

export interface ToastProps {
  open: boolean;
  message: ReactNode;
  variant?: ToastVariant;
  onClose: () => void;

  /**
   * Auto close after this many ms.
   * Set to 0 (or a negative number) to disable auto-hide completely.
   */
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
  const messageRef = useRef<HTMLDivElement | null>(null);

  // These thresholds are conservative defaults; tweak to taste.
  const MAX_LINES_FOR_AUTOHIDE = 3;
  const MAX_CHARS_FOR_AUTOHIDE = 140;

  const [disableAutoHideBecauseLong, setDisableAutoHideBecauseLong] = useState(false);

  // Measure the rendered content when it opens or when message changes.
  useLayoutEffect(() => {
    if (!open) {
      setDisableAutoHideBecauseLong(false);
      return;
    }

    const el = messageRef.current;
    if (!el) return;

    // Measure after layout is stable.
    const raf = window.requestAnimationFrame(() => {
      const text = (el.innerText ?? "").trim();
      const charCount = text.length;

      const style = window.getComputedStyle(el);
      const lineHeightPx = parseFloat(style.lineHeight);
      const lineHeight = Number.isFinite(lineHeightPx) && lineHeightPx > 0 ? lineHeightPx : 16;

      const height = el.getBoundingClientRect().height;
      const lineCount = Math.max(1, Math.round(height / lineHeight));

      const isLong = lineCount > MAX_LINES_FOR_AUTOHIDE || charCount > MAX_CHARS_FOR_AUTOHIDE;
      setDisableAutoHideBecauseLong(isLong);
    });

    return () => window.cancelAnimationFrame(raf);
  }, [open, message]);

  const shouldAutoHide = useMemo(() => {
    if (!open) return false;
    if (autoHideMs <= 0) return false; // explicit disable
    if (disableAutoHideBecauseLong) return false; // smart disable
    return true;
  }, [open, autoHideMs, disableAutoHideBecauseLong]);

  useEffect(() => {
    if (!shouldAutoHide) return;
    const t = window.setTimeout(() => onClose(), autoHideMs);
    return () => window.clearTimeout(t);
  }, [shouldAutoHide, onClose, autoHideMs]);

  if (!open) return null;

  return (
    <div
      className={["min-w-[240px] max-w-[420px] rounded-md border px-3 py-2 text-sm shadow-lg", variantClass(variant)].join(" ")}
      role="status"
      aria-live="polite">
      <div className="flex items-start justify-between gap-3">
        <div className="flex-1" ref={messageRef}>
          {message}
        </div>

        <button type="button" className="-mr-1 rounded px-1 text-slate-600 hover:bg-black/5" aria-label="Close" onClick={onClose}>
          ×
        </button>
      </div>
    </div>
  );
}
