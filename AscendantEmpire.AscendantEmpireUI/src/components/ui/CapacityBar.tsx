import { layOutCapacityBar, type CapacityBarSegment } from "./capacityBarLayout";
import type { Color } from "./colorpicker/colorTypes";
import Tooltip from "./Tooltip";

export type { CapacityBarSegment };

/** Height is a visual weight tuned once, so it carries a default; width almost never is. */
const DEFAULT_PX_HEIGHT = 10;

export interface CapacityBarProps {
  segments: readonly CapacityBarSegment[];

  /** Used for any segment that names no colour of its own. */
  defaultFillColor: Color;

  /** The bar's height in px. Default: 10 */
  pxHeight?: number;

  /** The bar's width in px. Omitted: the bar fills its container. */
  pxWidth?: number;

  /** Omitted: the segments are the whole. Supplied: they are shares of it and the shortfall is the remainder. */
  totalUnits?: number;

  remainderColor?: Color;

  remainderTooltip?: string;

  className?: string;
}

/**
 * A horizontal bar divided into proportional, individually-tooltipped parts.
 *
 * What a quantity is made of, rather than how far along it is — a force's cargo against its carrying
 * capacity, a structure's build time split into its stages. `ProgressBar` answers the other question and
 * overlays a percentage; this one draws no figures at all, because the segments' tooltips are where the
 * caller puts them. A caller whose figures matter to keyboard and touch users should render them in text
 * beside the bar, the way the research table does beneath its own.
 *
 * Width fills the container unless `pxWidth` says otherwise: inside a dialog the bar wants to span the
 * content area, and a hard-coded width is wrong as soon as that area changes.
 */
export default function CapacityBar({
  segments,
  defaultFillColor,
  pxHeight = DEFAULT_PX_HEIGHT,
  pxWidth,
  totalUnits,
  remainderColor,
  remainderTooltip,
  className,
}: CapacityBarProps) {
  const bands = layOutCapacityBar({ segments, defaultFillColor, totalUnits, remainderColor, remainderTooltip });

  // docs/WCAG.md treats a hover-only tooltip as a defect wherever it carries the only label, and Tooltip is
  // hover-only with a pointer-events-none bubble. This is the text route to the same breakdown.
  const accessibleName = bands
    .map((band) => band.tooltip)
    .filter((tooltip): tooltip is string => tooltip !== undefined)
    .join(", ");

  // A bar whose bands are all unlabelled carries no information, so it is decorative. The alternative is
  // role="img" with an empty name, which assistive technology announces as an image nobody named.
  const isDecorative = accessibleName.length === 0;

  // One track per band rather than a flex row, for the reason ResearchProgressBar sets out: Tooltip wraps its
  // child in an inline-flex span, and a span sized inside a flex row leaves the fill to inline layout, which
  // baseline-aligns it against the line-height inherited from wherever the bar sits. A grid track sizes
  // whatever item lands in it.
  const gridTemplateColumns = bands.map((band) => `${band.widthPercent}fr`).join(" ");

  return (
    <div
      role={isDecorative ? undefined : "img"}
      aria-label={isDecorative ? undefined : accessibleName}
      aria-hidden={isDecorative || undefined}
      className={["grid overflow-hidden rounded", className].filter(Boolean).join(" ")}
      style={{ gridTemplateColumns, width: pxWidth ?? "100%", height: pxHeight }}>
      {bands.map((band, index) => (
        // h-full on the Tooltip as well as the fill: the Tooltip's span is the grid item, and it has to fill
        // the row before the fill inside it has a height to fill in turn.
        <Tooltip key={index} content={band.tooltip} className="h-full">
          <div className="h-full w-full" style={{ backgroundColor: band.color.value }} data-testid="capacity-band" />
        </Tooltip>
      ))}
    </div>
  );
}
