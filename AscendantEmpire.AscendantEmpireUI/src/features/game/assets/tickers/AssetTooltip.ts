import { formatNumberWithCommas } from "@/util/number";

export interface BuildAssetTooltipArgs {
  name: string;
  amount: number;
  tooltip?: string;
  isImported?: boolean;
  isExported?: boolean;
  hasStrategicReserve?: boolean;
  // Number of turns until this asset becomes obsolete; omit or 0 means not obsoleting
  obsoleteIn?: number;
  demandNotMet?: boolean;
}

function shouldAppendCustomTooltip(name: string, tooltip: string | undefined): boolean {
  if (!tooltip) return false;

  const normalizedTooltip = tooltip.trim();
  if (normalizedTooltip.length === 0) return false;
  if (normalizedTooltip === name) return false;
  if (normalizedTooltip.startsWith(`${name}:`)) return false;

  return true;
}

export function buildAssetTooltip({
  name,
  amount,
  tooltip,
  isImported,
  isExported,
  hasStrategicReserve,
  obsoleteIn,
  demandNotMet,
}: BuildAssetTooltipArgs): string {
  const states = [
    isImported ? "Imported" : null,
    isExported ? "Exported" : null,
    hasStrategicReserve ? "Reserved" : null,
    obsoleteIn != null && obsoleteIn > 0 ? `Obsolete In: ${obsoleteIn} rounds` : null,
    demandNotMet ? "Demand Not Met" : null,
  ].filter((value): value is string => value != null);

  const stateSuffix = states.length > 0 ? ` (${states.join(", ")})` : "";
  const customSuffix = shouldAppendCustomTooltip(name, tooltip) ? ` - ${tooltip?.trim()}` : "";

  return `${name}: ${formatNumberWithCommas(amount)}${stateSuffix}${customSuffix}`;
}
