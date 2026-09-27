import type { ReactNode } from "react";

// Served from public/ rather than bundled, because the resource catalogue seeds these same paths as each Resource's IconPath — see ResourceSeedData on the API
// side. This whole module goes away once the tickers take their icon path from GET /player/resources.
const boneIconUrl = "/resources/other/fishbone.svg";
const grassIconUrl = "/resources/other/high-grass.svg";

const iconUrlByLabel: Record<string, string> = {
  Bone: boneIconUrl,
  Fodder: grassIconUrl,
};

export function getResourceIconByLabel(label: string | undefined): ReactNode | undefined {
  if (!label) return undefined;
  const url = iconUrlByLabel[label];
  return url ? <img src={url} alt="" /> : undefined;
}
