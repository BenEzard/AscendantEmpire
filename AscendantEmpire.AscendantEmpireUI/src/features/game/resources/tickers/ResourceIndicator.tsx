import type { CSSProperties } from "react";

export interface ResourceIndicatorProps {
  active: boolean;
  activeColor: string;
  inactiveColor: string;
  label: string;
  shape?: "circle" | "triangle-up" | "triangle-down";
}

export default function ResourceIndicator({ active, activeColor, inactiveColor, label, shape = "circle" }: ResourceIndicatorProps) {
  const style = {
    backgroundColor: active ? activeColor : inactiveColor,
  } satisfies CSSProperties;

  const className = ["resource-indicator", shape !== "circle" ? `resource-indicator--${shape}` : ""].filter(Boolean).join(" ");

  return <span className={className} aria-label={label} title={label} style={style} />;
}
