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
