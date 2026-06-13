import { useCallback, useState, type ReactNode } from "react";
import { Toast, ToastViewport } from "@/components/ui";

export type ToastVariant = "success" | "danger" | "warning" | "info";

interface ToastState {
  show: boolean;
  message: string;
  variant: ToastVariant;
}

export interface UseToastResult {
  /** Show a toast. Defaults to the "success" variant. */
  openToast: (message: string, variant?: ToastVariant) => void;
  /** Hide the current toast. */
  closeToast: () => void;
  /**
   * The toast UI to drop into the component tree (once). `ToastViewport` portals to
   * the document, so its position in the tree doesn't matter.
   */
  toastElement: ReactNode;
}

/**
 * Generic toast plumbing shared across feature screens. Owns the toast state and the
 * `ToastViewport`/`Toast` markup so callers only deal with `openToast`/`closeToast`.
 */
export function useToast(autoHideMs = 3000): UseToastResult {
  const [toast, setToast] = useState<ToastState>({ show: false, message: "", variant: "success" });

  const openToast = useCallback((message: string, variant: ToastVariant = "success") => {
    setToast({ show: true, message, variant });
  }, []);

  const closeToast = useCallback(() => {
    setToast((t) => ({ ...t, show: false }));
  }, []);

  const toastElement = (
    <ToastViewport>
      <Toast open={toast.show} onClose={closeToast} variant={toast.variant} message={toast.message} autoHideMs={autoHideMs} />
    </ToastViewport>
  );

  return { openToast, closeToast, toastElement };
}
