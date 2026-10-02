import type { CSSProperties } from "react";

export interface AssetAmountLabelProps {
  value: string;
  vertical?: boolean;
  className?: string;
  style?: CSSProperties;
}

export default function AssetAmountLabel({ value, vertical = false, className = "", style }: AssetAmountLabelProps) {
  const nextClassName = ["asset-amount-label", vertical ? "asset-amount-label--vertical" : "", className].filter(Boolean).join(" ");

  return (
    <span className={nextClassName} style={style}>
      {value}
    </span>
  );
}
