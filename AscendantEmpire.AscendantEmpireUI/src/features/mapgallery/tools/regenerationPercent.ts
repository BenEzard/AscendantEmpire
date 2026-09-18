/**
 * Converts a biome's Regeneration Percent between how it is stored and how the Biome Manager shows it.
 *
 * The server stores the share of Base that regrows each Round as a fraction (0.25), to three decimal places. A designer thinks in percent, so the
 * editor shows and accepts 25 — which also keeps it from being mistaken for the old multiplier (1.25).
 */

/** The value to show in the editor: the stored fraction as a percent, to one decimal place (the finest step the server keeps), or "" when there is none. */
export function toRegenerationPercentInput(fraction: number | null | undefined): string | number {
  if (fraction == null) {
    return "";
  }

  // Rounded to tenths of a percent so float noise (0.07 × 100 = 7.000000000000001) never reaches the input.
  return Math.round(fraction * 1000) / 10;
}

/** The value to store from what the designer typed: the percent as a fraction, to three decimal places, or null when the input is empty. */
export function fromRegenerationPercentInput(raw: string): number | null {
  if (raw.trim() === "") {
    return null;
  }

  // Divided in thousandths rather than by 100 directly, for the same float-noise reason. The server validates the range (more than 0, at most 100%).
  return Math.round(Number(raw) * 10) / 1000;
}
