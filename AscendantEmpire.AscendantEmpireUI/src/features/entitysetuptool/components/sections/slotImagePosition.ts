// Conversions between a slot's stored UI position and pixel coordinates on the rendered component image.
//
// A slot's uiXPosition / uiYPosition are *normalized offsets from the image center*: (0, 0) is the
// centre of the image, -1 / +1 are the left/right (X) or top/bottom (Y) edges — i.e. the value is
// measured against the image's half-width / half-height. Values may fall outside -1..1 for slots that
// sit beyond the image bounds (e.g. a helmet marker floating above the head).
//
// Keeping these as small pure functions lets the drag-to-place editor (ImageSection) and any tests share
// exactly one definition of the coordinate system, and makes the mapping trivially unit-testable.

/** The minimal slice of a DOMRect we need — the rendered image's on-screen size. */
export interface ImageBox {
  width: number;
  height: number;
}

/** Decimal places we round normalized positions to. Matches the backend column precision (decimal(9,4)). */
const POSITION_DECIMALS = 4;

/** Rounds to POSITION_DECIMALS places, normalizing -0 to 0 so stored values stay clean. */
function roundPosition(value: number): number {
  const factor = 10 ** POSITION_DECIMALS;
  const rounded = Math.round(value * factor) / factor;
  // `Math.round(-0.0001 * factor) / factor` can yield -0; collapse it to 0.
  return rounded === 0 ? 0 : rounded;
}

/**
 * Pixel position (relative to the image's top-left) -> normalized offset from the image center.
 * A zero-sized box (image not laid out yet) maps everything to the center to avoid dividing by zero.
 */
export function toNormalized(pixelX: number, pixelY: number, box: ImageBox): { x: number; y: number } {
  const halfWidth = box.width / 2;
  const halfHeight = box.height / 2;

  const x = halfWidth === 0 ? 0 : (pixelX - halfWidth) / halfWidth;
  const y = halfHeight === 0 ? 0 : (pixelY - halfHeight) / halfHeight;

  return { x: roundPosition(x), y: roundPosition(y) };
}

/**
 * Normalized offset from the image center -> pixel position relative to the image's top-left.
 * The inverse of {@link toNormalized} (up to the rounding applied on the way in).
 */
export function toPixel(normalizedX: number, normalizedY: number, box: ImageBox): { x: number; y: number } {
  const halfWidth = box.width / 2;
  const halfHeight = box.height / 2;

  return {
    x: halfWidth + normalizedX * halfWidth,
    y: halfHeight + normalizedY * halfHeight,
  };
}
