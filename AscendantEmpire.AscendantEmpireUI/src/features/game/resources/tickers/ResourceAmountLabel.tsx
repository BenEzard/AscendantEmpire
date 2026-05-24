import type { CSSProperties } from "react";

export interface ResourceAmountLabelProps {
  value: string;
  vertical?: boolean;
  className?: string;
  style?: CSSProperties;
}

export default function ResourceAmountLabel({ value, vertical = false, className = "", style }: ResourceAmountLabelProps) {
  const nextClassName = ["resource-amount-label", vertical ? "resource-amount-label--vertical" : "", className].filter(Boolean).join(" ");

  return (
    <span className={nextClassName} style={style}>
      {value}
    </span>
  );
}
