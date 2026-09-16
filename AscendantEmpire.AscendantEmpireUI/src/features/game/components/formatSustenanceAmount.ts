/**
 * A sustenance amount, whole and compact so the tile bar stays tight: 334, then 1.2k from a thousand up.
 *
 * Rounded down rather than to nearest, so the bar never promises food the tile does not have.
 */
export function formatSustenanceAmount(value: number): string {
  const whole = Math.floor(value);

  if (whole < 1000) {
    return String(whole);
  }

  // One decimal place, floored for the same reason, and dropped when it is zero ("2k" rather than "2.0k").
  const thousands = Math.floor(whole / 100) / 10;

  return `${Number.isInteger(thousands) ? thousands.toFixed(0) : thousands.toFixed(1)}k`;
}
