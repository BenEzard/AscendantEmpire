import { formatResourceAmount } from "@/util/number";
import Tooltip from "./Tooltip";

export interface NumberValueFieldProps {
  actualNumber: number;
  tooltip: string;
  width?: number;
}

const DEFAULT_WIDTH = 56;

export default function NumberValueField({ actualNumber, tooltip, width = DEFAULT_WIDTH }: NumberValueFieldProps) {
  return (
    <Tooltip content={tooltip}>
      <span className="inline-flex items-center justify-center rounded border border-slate-300 py-0.5 text-sm tabular-nums" style={{ width }}>
        {formatResourceAmount(actualNumber)}
      </span>
    </Tooltip>
  );
}
