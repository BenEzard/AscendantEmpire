import type { Color, ColorPalette } from "./colorTypes";

/**
 * The Tailwind CSS default colour palette, baked out as sRGB hex.
 *
 * Generated from the oklch values in the installed `tailwindcss/theme.css` (see the Tailwind colour docs
 * at https://tailwindcss.com/docs/colors). Hex is stored rather than `oklch(...)` so that picked values
 * round-trip through the colour helpers in `@/util/color` - which parse via the browser's `rgb()` output -
 * and persist cleanly as plain HTML colours.
 *
 * The hues run red through slate in documentation order; gray, zinc, neutral and stone are omitted as
 * near-duplicates of slate.
 */
export class TailwindColors {
  /** The shade steps Tailwind publishes for every hue, lightest first. */
  public static readonly Shades: readonly number[] = [50, 100, 200, 300, 400, 500, 600, 700, 800, 900, 950];

  /** Number of shades per hue - the natural column count for a Tailwind swatch grid. */
  public static readonly ShadeCount: number = 11;

  /** Every Tailwind colour, ordered hue by hue and, within a hue, lightest shade first. */
  public static readonly AllColors: readonly Color[] = [
    // red
    { value: "#fef2f2", description: "red-50" },
    { value: "#ffe2e2", description: "red-100" },
    { value: "#ffc9c9", description: "red-200" },
    { value: "#ffa2a2", description: "red-300" },
    { value: "#ff6467", description: "red-400" },
    { value: "#fb2c36", description: "red-500" },
    { value: "#e7000b", description: "red-600" },
    { value: "#c10007", description: "red-700" },
    { value: "#9f0712", description: "red-800" },
    { value: "#82181a", description: "red-900" },
    { value: "#460809", description: "red-950" },
    // orange
    { value: "#fff7ed", description: "orange-50" },
    { value: "#ffedd4", description: "orange-100" },
    { value: "#ffd6a7", description: "orange-200" },
    { value: "#ffb86a", description: "orange-300" },
    { value: "#ff8904", description: "orange-400" },
    { value: "#ff6900", description: "orange-500" },
    { value: "#f54900", description: "orange-600" },
    { value: "#ca3500", description: "orange-700" },
    { value: "#9f2d00", description: "orange-800" },
    { value: "#7e2a0c", description: "orange-900" },
    { value: "#441306", description: "orange-950" },
    // amber
    { value: "#fffbeb", description: "amber-50" },
    { value: "#fef3c6", description: "amber-100" },
    { value: "#fee685", description: "amber-200" },
    { value: "#ffd230", description: "amber-300" },
    { value: "#ffb900", description: "amber-400" },
    { value: "#fe9a00", description: "amber-500" },
    { value: "#e17100", description: "amber-600" },
    { value: "#bb4d00", description: "amber-700" },
    { value: "#973c00", description: "amber-800" },
    { value: "#7b3306", description: "amber-900" },
    { value: "#461901", description: "amber-950" },
    // yellow
    { value: "#fefce8", description: "yellow-50" },
    { value: "#fef9c2", description: "yellow-100" },
    { value: "#fff085", description: "yellow-200" },
    { value: "#ffdf20", description: "yellow-300" },
    { value: "#fdc700", description: "yellow-400" },
    { value: "#f0b100", description: "yellow-500" },
    { value: "#d08700", description: "yellow-600" },
    { value: "#a65f00", description: "yellow-700" },
    { value: "#894b00", description: "yellow-800" },
    { value: "#733e0a", description: "yellow-900" },
    { value: "#432004", description: "yellow-950" },
    // lime
    { value: "#f7fee7", description: "lime-50" },
    { value: "#ecfcca", description: "lime-100" },
    { value: "#d8f999", description: "lime-200" },
    { value: "#bbf451", description: "lime-300" },
    { value: "#9ae600", description: "lime-400" },
    { value: "#7ccf00", description: "lime-500" },
    { value: "#5ea500", description: "lime-600" },
    { value: "#497d00", description: "lime-700" },
    { value: "#3c6300", description: "lime-800" },
    { value: "#35530e", description: "lime-900" },
    { value: "#192e03", description: "lime-950" },
    // green
    { value: "#f0fdf4", description: "green-50" },
    { value: "#dcfce7", description: "green-100" },
    { value: "#b9f8cf", description: "green-200" },
    { value: "#7bf1a8", description: "green-300" },
    { value: "#05df72", description: "green-400" },
    { value: "#00c950", description: "green-500" },
    { value: "#00a63e", description: "green-600" },
    { value: "#008236", description: "green-700" },
    { value: "#016630", description: "green-800" },
    { value: "#0d542b", description: "green-900" },
    { value: "#032e15", description: "green-950" },
    // emerald
    { value: "#ecfdf5", description: "emerald-50" },
    { value: "#d0fae5", description: "emerald-100" },
    { value: "#a4f4cf", description: "emerald-200" },
    { value: "#5ee9b5", description: "emerald-300" },
    { value: "#00d492", description: "emerald-400" },
    { value: "#00bc7d", description: "emerald-500" },
    { value: "#009966", description: "emerald-600" },
    { value: "#007a55", description: "emerald-700" },
    { value: "#006045", description: "emerald-800" },
    { value: "#004f3b", description: "emerald-900" },
    { value: "#002c22", description: "emerald-950" },
    // teal
    { value: "#f0fdfa", description: "teal-50" },
    { value: "#cbfbf1", description: "teal-100" },
    { value: "#96f7e4", description: "teal-200" },
    { value: "#46ecd5", description: "teal-300" },
    { value: "#00d5be", description: "teal-400" },
    { value: "#00bba7", description: "teal-500" },
    { value: "#009689", description: "teal-600" },
    { value: "#00786f", description: "teal-700" },
    { value: "#005f5a", description: "teal-800" },
    { value: "#0b4f4a", description: "teal-900" },
    { value: "#022f2e", description: "teal-950" },
    // cyan
    { value: "#ecfeff", description: "cyan-50" },
    { value: "#cefafe", description: "cyan-100" },
    { value: "#a2f4fd", description: "cyan-200" },
    { value: "#53eafd", description: "cyan-300" },
    { value: "#00d3f2", description: "cyan-400" },
    { value: "#00b8db", description: "cyan-500" },
    { value: "#0092b8", description: "cyan-600" },
    { value: "#007595", description: "cyan-700" },
    { value: "#005f78", description: "cyan-800" },
    { value: "#104e64", description: "cyan-900" },
    { value: "#053345", description: "cyan-950" },
    // sky
    { value: "#f0f9ff", description: "sky-50" },
    { value: "#dff2fe", description: "sky-100" },
    { value: "#b8e6fe", description: "sky-200" },
    { value: "#74d4ff", description: "sky-300" },
    { value: "#00bcff", description: "sky-400" },
    { value: "#00a6f4", description: "sky-500" },
    { value: "#0084d1", description: "sky-600" },
    { value: "#0069a8", description: "sky-700" },
    { value: "#00598a", description: "sky-800" },
    { value: "#024a70", description: "sky-900" },
    { value: "#052f4a", description: "sky-950" },
    // blue
    { value: "#eff6ff", description: "blue-50" },
    { value: "#dbeafe", description: "blue-100" },
    { value: "#bedbff", description: "blue-200" },
    { value: "#8ec5ff", description: "blue-300" },
    { value: "#51a2ff", description: "blue-400" },
    { value: "#2b7fff", description: "blue-500" },
    { value: "#155dfc", description: "blue-600" },
    { value: "#1447e6", description: "blue-700" },
    { value: "#193cb8", description: "blue-800" },
    { value: "#1c398e", description: "blue-900" },
    { value: "#162456", description: "blue-950" },
    // indigo
    { value: "#eef2ff", description: "indigo-50" },
    { value: "#e0e7ff", description: "indigo-100" },
    { value: "#c6d2ff", description: "indigo-200" },
    { value: "#a3b3ff", description: "indigo-300" },
    { value: "#7c86ff", description: "indigo-400" },
    { value: "#615fff", description: "indigo-500" },
    { value: "#4f39f6", description: "indigo-600" },
    { value: "#432dd7", description: "indigo-700" },
    { value: "#372aac", description: "indigo-800" },
    { value: "#312c85", description: "indigo-900" },
    { value: "#1e1a4d", description: "indigo-950" },
    // violet
    { value: "#f5f3ff", description: "violet-50" },
    { value: "#ede9fe", description: "violet-100" },
    { value: "#ddd6ff", description: "violet-200" },
    { value: "#c4b4ff", description: "violet-300" },
    { value: "#a684ff", description: "violet-400" },
    { value: "#8e51ff", description: "violet-500" },
    { value: "#7f22fe", description: "violet-600" },
    { value: "#7008e7", description: "violet-700" },
    { value: "#5d0ec0", description: "violet-800" },
    { value: "#4d179a", description: "violet-900" },
    { value: "#2f0d68", description: "violet-950" },
    // purple
    { value: "#faf5ff", description: "purple-50" },
    { value: "#f3e8ff", description: "purple-100" },
    { value: "#e9d4ff", description: "purple-200" },
    { value: "#dab2ff", description: "purple-300" },
    { value: "#c27aff", description: "purple-400" },
    { value: "#ad46ff", description: "purple-500" },
    { value: "#9810fa", description: "purple-600" },
    { value: "#8200db", description: "purple-700" },
    { value: "#6e11b0", description: "purple-800" },
    { value: "#59168b", description: "purple-900" },
    { value: "#3c0366", description: "purple-950" },
    // fuchsia
    { value: "#fdf4ff", description: "fuchsia-50" },
    { value: "#fae8ff", description: "fuchsia-100" },
    { value: "#f6cfff", description: "fuchsia-200" },
    { value: "#f4a8ff", description: "fuchsia-300" },
    { value: "#ed6aff", description: "fuchsia-400" },
    { value: "#e12afb", description: "fuchsia-500" },
    { value: "#c800de", description: "fuchsia-600" },
    { value: "#a800b7", description: "fuchsia-700" },
    { value: "#8a0194", description: "fuchsia-800" },
    { value: "#721378", description: "fuchsia-900" },
    { value: "#4b004f", description: "fuchsia-950" },
    // pink
    { value: "#fdf2f8", description: "pink-50" },
    { value: "#fce7f3", description: "pink-100" },
    { value: "#fccee8", description: "pink-200" },
    { value: "#fda5d5", description: "pink-300" },
    { value: "#fb64b6", description: "pink-400" },
    { value: "#f6339a", description: "pink-500" },
    { value: "#e60076", description: "pink-600" },
    { value: "#c6005c", description: "pink-700" },
    { value: "#a3004c", description: "pink-800" },
    { value: "#861043", description: "pink-900" },
    { value: "#510424", description: "pink-950" },
    // rose
    { value: "#fff1f2", description: "rose-50" },
    { value: "#ffe4e6", description: "rose-100" },
    { value: "#ffccd3", description: "rose-200" },
    { value: "#ffa1ad", description: "rose-300" },
    { value: "#ff637e", description: "rose-400" },
    { value: "#ff2056", description: "rose-500" },
    { value: "#ec003f", description: "rose-600" },
    { value: "#c70036", description: "rose-700" },
    { value: "#a50036", description: "rose-800" },
    { value: "#8b0836", description: "rose-900" },
    { value: "#4d0218", description: "rose-950" },
    // slate
    { value: "#f8fafc", description: "slate-50" },
    { value: "#f1f5f9", description: "slate-100" },
    { value: "#e2e8f0", description: "slate-200" },
    { value: "#cad5e2", description: "slate-300" },
    { value: "#90a1b9", description: "slate-400" },
    { value: "#62748e", description: "slate-500" },
    { value: "#45556c", description: "slate-600" },
    { value: "#314158", description: "slate-700" },
    { value: "#1d293d", description: "slate-800" },
    { value: "#0f172b", description: "slate-900" },
    { value: "#020618", description: "slate-950" },
  ];

  /**
   * Builds the default single-palette view of the Tailwind colours.
   *
   * Rendered at the default `ShadeCount` columns this wraps into the familiar Tailwind grid, with one hue
   * per row. A fresh array is returned on each call so callers can safely append their own palettes to it.
   */
  public static defaultPalettes(): ColorPalette[] {
    return [{ title: "Tailwind", colors: [...TailwindColors.AllColors] }];
  }
}
