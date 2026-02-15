import type { ButtonHTMLAttributes, ReactNode } from "react";

export type ButtonVariant = "primary" | "secondary" | "success" | "danger" | "light" | "outline-secondary" | "outline-primary";

export interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: ButtonVariant;
  children: ReactNode;
}

function variantClass(variant: ButtonVariant): string {
  switch (variant) {
    case "primary":
      return "bg-[var(--bs-primary)] text-white hover:opacity-90";
    case "secondary":
      return "bg-[var(--bs-secondary)] text-white hover:opacity-90";
    case "success":
      return "bg-[var(--bs-success)] text-white hover:opacity-90";
    case "danger":
      return "bg-[var(--bs-danger)] text-white hover:opacity-90";
    case "light":
      return "bg-white text-black hover:bg-slate-50";
    case "outline-primary":
      return "border border-[var(--bs-primary)] text-[var(--bs-primary)] hover:bg-[color-mix(in_srgb,var(--bs-primary)_10%,transparent)]";
    case "outline-secondary":
      return "border border-[var(--bs-secondary)] text-[var(--bs-secondary)] hover:bg-[color-mix(in_srgb,var(--bs-secondary)_10%,transparent)]";
    default:
      return "bg-[var(--bs-secondary)] text-white hover:opacity-90";
  }
}

export default function Button({ variant = "secondary", className = "", disabled, type, ...props }: ButtonProps) {
  const base =
    "inline-flex items-center justify-center gap-2 rounded-md px-3 py-2 text-sm font-medium select-none " +
    "focus:outline-none focus-visible:ring-2 focus-visible:ring-offset-2 focus-visible:ring-slate-400 " +
    "disabled:opacity-60 disabled:cursor-not-allowed";

  const cls = [base, variantClass(variant), className].filter(Boolean).join(" ");

  return <button type={type ?? "button"} disabled={disabled} className={cls} {...props} />;
}
