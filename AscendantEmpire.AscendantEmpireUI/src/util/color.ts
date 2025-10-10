/**
 * Checks if a string is a valid CSS color.
 * Supports named colors, hex, rgb/rgba, hsl/hsla formats.
 */
export function isValidColor(value?: string | null): boolean {
  if (!value) return false;
  const s = new Option().style;
  s.color = "";
  s.color = value;
  return s.color !== "";
}

/**
 * Parses any valid CSS color into RGB(A) using the browser's parser.
 * Returns { r, g, b, a } or null if invalid.
 */
export function cssColorToRgba(value?: string | null): { r: number; g: number; b: number; a: number } | null {
  if (!isValidColor(value)) return null;
  const s = new Option().style;
  s.color = value!;
  // s.color will now be in "rgb(r, g, b)" or "rgba(r, g, b, a)" form
  const m = s.color.match(/^rgba?\((\d+),\s*(\d+),\s*(\d+)(?:,\s*([\d.]+))?\)$/i);
  if (!m) return null;
  const [, r, g, b, a] = m;
  return {
    r: Number(r),
    g: Number(g),
    b: Number(b),
    a: a === undefined ? 1 : Number(a),
  };
}

/**
 * WCAG relative luminance (sRGB).
 */
function relativeLuminance({ r, g, b }: { r: number; g: number; b: number }): number {
  const toLin = (c: number) => {
    const cs = c / 255;
    return cs <= 0.03928 ? cs / 12.92 : Math.pow((cs + 0.055) / 1.055, 2.4);
  };
  const R = toLin(r), G = toLin(g), B = toLin(b);
  return 0.2126 * R + 0.7152 * G + 0.0722 * B;
}

/**
 * Returns true if the color is "dark" (so white text is preferred).
 * Threshold picked so contrast with white >= ~4.5:1 in most cases.
 */
export function isColorDark(value?: string | null): boolean {
  const rgba = cssColorToRgba(value);
  if (!rgba) return false; // treat invalid as light
  // If fully transparent, treat as light.
  if (rgba.a === 0) return false;
  const L = relativeLuminance(rgba);
  return L < 0.5;
}

/**
 * Picks high-contrast text color ("#000" or "#fff") for a background.
 */
export function pickTextColorOnBg(
  background?: string | null,
  lightText: string = "#ffffff",
  darkText: string = "#000000"
): string {
  return isColorDark(background) ? lightText : darkText;
}
