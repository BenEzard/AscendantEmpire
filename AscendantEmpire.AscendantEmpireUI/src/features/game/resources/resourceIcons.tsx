import type { ReactNode } from "react";

import boneIconUrl from "@/assets/other/fishbone.svg";
import grassIconUrl from "@/assets/other/high-grass.svg";

const iconUrlByLabel: Record<string, string> = {
  Bone: boneIconUrl,
  Fodder: grassIconUrl,
};

export function getResourceIconByLabel(label: string | undefined): ReactNode | undefined {
  if (!label) return undefined;
  const url = iconUrlByLabel[label];
  return url ? <img src={url} alt="" /> : undefined;
}
