import type { CSSProperties } from "react";

export interface AssetIndicatorProps {
  active: boolean;
  activeColor: string;
  inactiveColor: string;
  label: string;
  shape?: "circle" | "triangle-up" | "triangle-down";
}

export default function AssetIndicator({ active, activeColor, inactiveColor, label, shape = "circle" }: AssetIndicatorProps) {
  const style = {
    backgroundColor: active ? activeColor : inactiveColor,
  } satisfies CSSProperties;

  const className = ["asset-indicator", shape !== "circle" ? `asset-indicator--${shape}` : ""].filter(Boolean).join(" ");

  return <span className={className} aria-label={label} title={label} style={style} />;
}
