import type { CSSProperties } from "react";

export interface ResourceIndicatorProps {
  active: boolean;
  activeColor: string;
  inactiveColor: string;
  label: string;
}

export default function ResourceIndicator({ active, activeColor, inactiveColor, label }: ResourceIndicatorProps) {
  const style = {
    backgroundColor: active ? activeColor : inactiveColor,
  } satisfies CSSProperties;

  return <span className="resource-indicator" aria-label={label} title={label} style={style} />;
}
