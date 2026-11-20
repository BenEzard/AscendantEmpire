import type { Color } from "./colorpicker/colorTypes";

/** One part of a capacity bar, sized in the caller's own units. */
export interface CapacityBarSegment {
  /** Falls back to the bar's defaultFillColor. */
  fillColor?: Color;

  /** The segment's size in whatever units the caller counts — kilograms, turns, percent. */
  unitWidth: number;

  tooltip: string;
}

/** Everything the layout needs: a capacity bar's props less the ones that only affect its size on screen. */
export interface CapacityBarLayoutRequest {
  segments: readonly CapacityBarSegment[];

  defaultFillColor: Color;

  /** Omitted: the segments are the whole. Supplied: they are shares of it and the shortfall is the remainder. */
  totalUnits?: number;

  remainderColor?: Color;

  remainderTooltip?: string;
}

/** One drawable band of the bar, sized as a percentage of the track. */
export interface CapacityBarBand {
  widthPercent: number;
  color: Color;
  tooltip: string | undefined;
  isRemainder: boolean;
}

/** The grey ProgressBar already paints its remainder, used where a bar needs a remainder but names no colour. */
export const DEFAULT_REMAINDER_COLOR: Color = { value: "#b8c3ce", description: "Unused" };

/**
 * Turn a bar's segments into percentage bands.
 *
 * `unitWidth` is in the caller's own units, never a percentage, and `totalUnits` is what decides whether
 * there is any spare room to draw. Omit it and the segments are the whole: a build time of 80 turns
 * construction and 40 shipping fills the bar, with no remainder. Supply it and they are shares of it, with
 * `totalUnits - sum` drawn as a trailing remainder — a force carrying 40kg of a 100kg capacity. A
 * percentage bar that wants a visible gap therefore passes `totalUnits: 100` explicitly.
 *
 * Segments summing past `totalUnits` are renormalised onto the full track rather than overrunning it, so an
 * over-capacity bar reads as full and keeps the segments' proportions to each other. Nothing throws and no
 * segment is dropped for being inconvenient: a bar that silently loses a part is worse than one that reads
 * as full.
 */
export function layOutCapacityBar(request: CapacityBarLayoutRequest): CapacityBarBand[] {
  const { segments, defaultFillColor, totalUnits, remainderColor, remainderTooltip } = request;

  const drawable = segments.filter((segment) => toDrawableUnits(segment.unitWidth) > 0);
  const occupiedUnits = drawable.reduce((sum, segment) => sum + toDrawableUnits(segment.unitWidth), 0);

  const capacityUnits = totalUnits !== undefined && Number.isFinite(totalUnits) && totalUnits > 0 ? totalUnits : undefined;

  // Taking the larger of the two is what renormalises an overrun instead of letting a band exceed the track.
  const denominatorUnits = capacityUnits !== undefined ? Math.max(capacityUnits, occupiedUnits) : occupiedUnits;

  if (denominatorUnits <= 0) {
    return [];
  }

  const bands: CapacityBarBand[] = drawable.map((segment) => ({
    widthPercent: (toDrawableUnits(segment.unitWidth) / denominatorUnits) * 100,
    color: segment.fillColor ?? defaultFillColor,
    tooltip: segment.tooltip,
    isRemainder: false,
  }));

  if (capacityUnits !== undefined && occupiedUnits < capacityUnits) {
    bands.push({
      widthPercent: ((capacityUnits - occupiedUnits) / denominatorUnits) * 100,
      color: remainderColor ?? DEFAULT_REMAINDER_COLOR,
      tooltip: remainderTooltip,
      isRemainder: true,
    });
  }

  return bands;
}

/** Anything that is not a positive, finite size occupies nothing, so it neither draws nor skews the shares. */
function toDrawableUnits(unitWidth: number): number {
  return Number.isFinite(unitWidth) && unitWidth > 0 ? unitWidth : 0;
}
