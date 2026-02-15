export interface SpinnerProps {
  size?: "sm" | "md";
  className?: string;
}

export default function Spinner({ size = "md", className = "" }: SpinnerProps) {
  const wh = size === "sm" ? "h-4 w-4" : "h-6 w-6";
  return <div className={["inline-block animate-spin rounded-full border-2 border-slate-300 border-t-slate-700", wh, className].join(" ")} />;
}
