/**
 * A single selectable colour within a palette.
 */
export interface Color {
  /** Any valid HTML/CSS colour. Palette colours supplied by TailwindColors are sRGB hex. */
  value: string;

  /** Human readable description, shown on hover (e.g. "red-500"). */
  description: string;
}

/**
 * A titled block of colours. The picker lays every palette out on the same column count, so
 * palettes stack vertically as titled sections rather than each choosing its own width.
 */
export interface ColorPalette {
  /** Heading rendered above the palette's swatches. */
  title: string;

  /** The colours in display order; the picker wraps them at the configured column count. */
  colors: Color[];
}

/**
 * A colour that has already been claimed elsewhere and therefore cannot be picked again.
 */
export interface UsedColor {
  /** The claimed colour. Compared against palette colours case-insensitively. */
  value: string;

  /** What already uses it, surfaced as "Already used by `{usedBy}`" on hover. */
  usedBy: string;
}
