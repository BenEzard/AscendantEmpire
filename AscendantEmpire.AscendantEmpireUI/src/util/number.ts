export function formatNumberWithCommas(value: number): string {
  if (!Number.isFinite(value)) return "0";
  const whole = Math.floor(value);
  return new Intl.NumberFormat("en-US").format(whole);
}

/**
 * Formats a number to a single decimal place (6.5, 1.0, 0.0).
 *
 * Shared by the unit designer's stat cards — Summary, Battle Stats, Training Cost and Upkeep — so every
 * figure in that panel is written the same way.
 *
 * Unlike the two helpers either side of it, a non-finite value is returned as it is ("NaN", "Infinity")
 * rather than flattened to "0": these are derived design stats, and a broken one should be visible rather
 * than quietly rendered as a plausible-looking zero.
 */
export function formatToOneDecimal(value: number): string {
  if (!Number.isFinite(value)) return String(value);
  return value.toFixed(1);
}

/**
 * A whole, compact amount for the cramped parts of the map UI: 334, then 1.2k from a thousand up.
 *
 * Rounded down rather than to nearest, so a tile never promises food or ore it does not have. Shared by everything the tile toolbar draws — sustenance,
 * fodder and MapResource estimates — and by the tile hover title, so one figure never reads differently from the same figure beside it.
 */
export function formatCompactAmount(value: number): string {
  const whole = Math.floor(value);

  if (whole < 1000) {
    return String(whole);
  }

  // One decimal place, floored for the same reason, and dropped when it is zero ("2k" rather than "2.0k").
  const thousands = Math.floor(whole / 100) / 10;

  return `${Number.isInteger(thousands) ? thousands.toFixed(0) : thousands.toFixed(1)}k`;
}

export function formatResourceAmount(amount: number): string {
  if (!Number.isFinite(amount)) return "0";

  const whole = Math.floor(amount);
  if (whole < 1000) return `${whole}`;

  if (whole < 1_000_000) {
    const thousands = Math.floor(whole / 100) / 10;
    const display = Number.isInteger(thousands) ? `${thousands.toFixed(0)}` : `${thousands.toFixed(1)}`;
    return `${display}K`;
  }

  const millions = Math.floor(whole / 100_000) / 10;
  const display = Number.isInteger(millions) ? `${millions.toFixed(0)}` : `${millions.toFixed(1)}`;
  return `${display}M`;
}
