export function formatNumberWithCommas(value: number): string {
  if (!Number.isFinite(value)) return "0";
  const whole = Math.floor(value);
  return new Intl.NumberFormat("en-US").format(whole);
}
