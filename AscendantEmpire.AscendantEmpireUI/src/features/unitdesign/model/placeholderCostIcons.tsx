import type { ReactNode } from "react";

// ============================================================================================
// Icons for the placeholder training cost and upkeep labels (see PLACEHOLDER_COST_UPKEEP in
// placeholderCosts.ts). It stands in until components carry real asset costs, at which point
// each cost names an Asset and takes its icon from that Asset's IconPath.
//
// The two labels below do not currently occur in `placeholderCostFor` / `placeholderUpkeepFor`
// output, so both call sites render their two-letter fallback badge today.
//
// Served from public/ rather than bundled, because the asset catalogue seeds these same paths
// as each Asset's IconPath — see AssetSeedData on the API side. The folder itself keeps its
// `resources` name: those paths are already stored in the IconPath column.
// ============================================================================================

const boneIconUrl = "/resources/other/fishbone.svg";
const grassIconUrl = "/resources/other/high-grass.svg";

const iconUrlByLabel: Record<string, string> = {
  Bone: boneIconUrl,
  Fodder: grassIconUrl,
};

/** The image for a placeholder cost or upkeep label, or undefined when nothing is mapped to it. */
export function getPlaceholderCostIconByLabel(label: string | undefined): ReactNode | undefined {
  if (!label) {
    return undefined;
  }

  const url = iconUrlByLabel[label];
  return url ? <img src={url} alt="" /> : undefined;
}
