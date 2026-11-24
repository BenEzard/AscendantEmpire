/**
 * Formats a movement-point value for display, trimming the trailing zero off whole numbers.
 *
 * Costs are authored to one decimal place (desert's 1.1), so "1.5" and "2" both need to read naturally.
 */
export function formatMovementPoints(points: number): string {
  return Number.isInteger(points) ? String(points) : points.toFixed(1);
}
