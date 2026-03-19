import { formatNumberWithCommas } from "@/util/number";

export interface BuildResourceTooltipArgs {
  name: string;
  amount: number;
  tooltip?: string;
  isImported?: boolean;
  isExported?: boolean;
  hasStrategicStockpile?: boolean;
  soonToBeObsolete?: boolean;
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

export function buildResourceTooltip({
  name,
  amount,
  tooltip,
  isImported,
  isExported,
  hasStrategicStockpile,
  soonToBeObsolete,
  demandNotMet,
}: BuildResourceTooltipArgs): string {
  const states = [
    isImported ? "Imported" : null,
    isExported ? "Exported" : null,
    hasStrategicStockpile ? "Stockpiled" : null,
    soonToBeObsolete ? "Obsoleting" : null,
    demandNotMet ? "Demand Not Met" : null,
  ].filter((value): value is string => value != null);

  const stateSuffix = states.length > 0 ? ` (${states.join(", ")})` : "";
  const customSuffix = shouldAppendCustomTooltip(name, tooltip) ? ` - ${tooltip?.trim()}` : "";

  return `${name}: ${formatNumberWithCommas(amount)}${stateSuffix}${customSuffix}`;
}
