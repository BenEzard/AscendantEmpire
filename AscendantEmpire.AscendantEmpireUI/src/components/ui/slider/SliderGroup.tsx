import type { ReactNode } from "react";

export interface SliderGroupProps {
  children: ReactNode;
}

export default function SliderGroup({ children }: SliderGroupProps) {
  return <div className="flex flex-col gap-1 rounded-lg border border-slate-200 px-3 py-2">{children}</div>;
}
