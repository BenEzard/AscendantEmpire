import { TailwindColors } from "./tailwindColors";
import type { Color, ColorPalette } from "./colorTypes";

/**
 * A subset of the Tailwind palette chosen so that no two colours can be mistaken for one another.
 *
 * Built for faction colours, where two players picking near-identical shades makes a map unreadable. The full 198-swatch
 * Tailwind grid is far too fine-grained for that: neighbouring hues at the same shade are barely distinguishable.
 *
 * The selection was measured rather than eyeballed, in OKLab - the space Tailwind 4 generates its ramps in. Two findings
 * shaped it:
 *
 * - A uniform two-shade grid tops out at about 20 usable swatches. Chroma collapses at both ends of every ramp, so the
 *   minimum distance *between hues* is worst exactly at the light and dark extremes (0.016 at shade 300, 0.038 at 700).
 *   Pruning hues does not rescue it.
 * - Letting each hue pick its own light step (300-500) and deep step (600-800), so neighbouring hues never share a
 *   lightness, reaches 24 swatches while keeping every pair well apart.
 *
 * The result is 24 colours whose closest pair is 0.122 apart in OKLab. For calibration, Tailwind's own orange-500 and
 * red-500 sit 0.101 apart - so the guarantee is "no two faction colours are closer than red is to orange". Six hues are
 * dropped outright as near-neighbours of ones that stayed: amber and green (yellow, lime, emerald), cyan (teal, sky),
 * violet and blue (purple, indigo, sky), and rose (red, pink).
 *
 * `distinctColors.test.ts` re-derives that separation from these values, so an edit that quietly ruins it fails there.
 *
 * Hexes are never written out here: each name is looked up in {@link TailwindColors.AllColors}, so a future regeneration
 * of the Tailwind values cannot leave a stale copy behind. A name that stops resolving is a build-time null, which the
 * same test catches.
 */
export class DistinctColors {
  /** Tailwind names of the light band, in display order. */
  private static readonly _LIGHT_NAMES: readonly string[] = [
    "red-300",
    "orange-400",
    "yellow-400",
    "lime-300",
    "emerald-500",
    "teal-300",
    "sky-500",
    "indigo-500",
    "purple-400",
    "fuchsia-300",
    "pink-400",
    "slate-400",
  ];

  /** Tailwind names of the deep band, in the same hue order as the light band. */
  private static readonly _DEEP_NAMES: readonly string[] = [
    "red-700",
    "orange-600",
    "yellow-800",
    "lime-700",
    "emerald-800",
    "teal-600",
    "sky-700",
    "indigo-800",
    "purple-700",
    "fuchsia-600",
    "pink-600",
    "slate-800",
  ];

  /** Every Tailwind colour indexed by its description, so a band is a lookup per name rather than a scan per name. */
  private static readonly _BY_DESCRIPTION: ReadonlyMap<string, Color> = new Map(
    TailwindColors.AllColors.map((color) => [color.description, color]),
  );

  /** Number of colours per band - the natural column count for laying the two bands out as an aligned grid. */
  public static readonly BandSize: number = DistinctColors._LIGHT_NAMES.length;

  /** The lighter of the two bands, one colour per hue. */
  public static readonly Light: readonly Color[] = DistinctColors.resolve(DistinctColors._LIGHT_NAMES);

  /** The deeper of the two bands, in the same hue order as {@link Light}. */
  public static readonly Deep: readonly Color[] = DistinctColors.resolve(DistinctColors._DEEP_NAMES);

  /** Both bands, light first. */
  public static readonly AllColors: readonly Color[] = [...DistinctColors.Light, ...DistinctColors.Deep];

  /**
   * The two bands as titled palettes, ready to hand to a colour picker.
   *
   * Returns a fresh array each call so a caller can append palettes of its own without mutating the shared value.
   */
  public static palettes(): ColorPalette[] {
    return [
      { title: "Bright", colors: [...DistinctColors.Light] },
      { title: "Deep", colors: [...DistinctColors.Deep] },
    ];
  }

  /**
   * Maps Tailwind names to their colours, keeping the given order.
   *
   * A name that does not resolve is dropped rather than throwing at module load, which would take the whole page down
   * over one renamed swatch. The band-size assertions in the tests are what turn that into a visible failure.
   */
  private static resolve(names: readonly string[]): Color[] {
    return names
      .map((name) => DistinctColors._BY_DESCRIPTION.get(name))
      .filter((color): color is Color => color !== undefined);
  }
}
