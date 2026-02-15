import type { ReactNode } from "react";

export type AlertVariant = "secondary" | "danger" | "warning" | "info" | "success";

export interface AlertProps {
  variant?: AlertVariant;
  children: ReactNode;
  className?: string;
}

function variantClass(variant: AlertVariant): string {
  switch (variant) {
    case "danger":
      return "border-[var(--bs-danger)]/30 bg-[color-mix(in_srgb,var(--bs-danger)_12%,white)] text-[var(--bs-danger)]";
    case "warning":
      return "border-[var(--bs-warning)]/40 bg-[color-mix(in_srgb,var(--bs-warning)_18%,white)] text-slate-800";
    case "info":
      return "border-[var(--bs-info)]/30 bg-[color-mix(in_srgb,var(--bs-info)_12%,white)] text-slate-800";
    case "success":
      return "border-[var(--bs-success)]/30 bg-[color-mix(in_srgb,var(--bs-success)_12%,white)] text-[var(--bs-success)]";
    case "secondary":
    default:
      return "border-slate-200 bg-slate-50 text-slate-700";
  }
}

export default function Alert({ variant = "secondary", children, className = "" }: AlertProps) {
  const cls = ["rounded-md border px-3 py-2 text-sm", variantClass(variant), className].filter(Boolean).join(" ");
  return <div className={cls}>{children}</div>;
}
