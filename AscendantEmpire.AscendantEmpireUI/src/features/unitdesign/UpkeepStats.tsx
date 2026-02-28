import type { ReactNode } from "react";
import type { IconType } from "react-icons";

import { FaFish } from "react-icons/fa";
import { GiHeavyBullets, GiJerrycan, GiSpanner } from "react-icons/gi";

import type { ComponentUpkeep, UpkeepCategory } from "./types/UnitComponent";
import { getResourceIconByLabel } from "@/features/game/resources/resourceIcons";

const CATEGORY_META: Record<UpkeepCategory, { title: string; Icon: IconType }> = {
  food: { title: "Food", Icon: FaFish },
  fuel: { title: "Fuel", Icon: GiJerrycan },
  repair: { title: "Repair", Icon: GiSpanner },
  ammunition: { title: "Ammunition", Icon: GiHeavyBullets },
};

const CATEGORY_ORDER: UpkeepCategory[] = ["food", "fuel", "repair", "ammunition"];

function iconBadge(title: string, icon: ReactNode) {
  return (
    <span title={title} className="inline-flex h-6 w-6 items-center justify-center rounded bg-black leading-none text-white">
      {icon}
    </span>
  );
}

function resourceIcon(label: string) {
  const icon = getResourceIconByLabel(label);
  const fallback = (
    <div className="flex h-full w-full items-center justify-center text-[10px] font-semibold text-white/80">{label.slice(0, 2).toUpperCase()}</div>
  );

  return (
    <div title={label} className="mx-auto h-6 w-6 [&>img]:h-full [&>img]:w-full [&>img]:object-contain">
      {icon ?? fallback}
    </div>
  );
}

export interface UpkeepStatsProps {
  upkeep?: ComponentUpkeep;
  existingUpkeep?: ComponentUpkeep;
  className?: string;
}

function fmt(n: number) {
  if (!Number.isFinite(n)) return String(n);
  return n.toFixed(1);
}

function round1(n: number) {
  return Math.round(n * 10) / 10;
}

function ResourceAmount({ current, existing, baselineProvided }: { current: number; existing?: number; baselineProvided: boolean }) {
  if (!baselineProvided) return <>{fmt(current)}</>;

  const currentRounded = round1(current);
  const existingRounded = round1(existing ?? 0);
  const deltaRounded = round1(currentRounded - existingRounded);

  if (!Number.isFinite(deltaRounded)) return <>{fmt(deltaRounded)}</>;

  const className = deltaRounded === 0 ? "text-white" : deltaRounded < 0 ? "text-red-400" : "text-green-400";
  return <span className={className}>{fmt(Math.abs(deltaRounded))}</span>;
}

export function UpkeepStats({ upkeep, existingUpkeep, className }: UpkeepStatsProps) {
  const baselineProvided = existingUpkeep != null;

  const hasAny =
    (upkeep != null &&
      Object.values(upkeep).some((resources) => resources != null && Object.values(resources).some((n) => Number.isFinite(n) && n > 0))) ||
    (existingUpkeep != null &&
      Object.values(existingUpkeep).some((resources) => resources != null && Object.values(resources).some((n) => Number.isFinite(n) && n > 0)));

  const categoryResources = (category: UpkeepCategory): Array<[string, number, number | undefined]> => {
    const current = upkeep?.[category] ?? {};
    const existing = existingUpkeep?.[category] ?? {};
    const labels = new Set<string>([...Object.keys(current), ...Object.keys(existing)]);

    return [...labels]
      .map((label) => {
        const c = current[label] ?? 0;
        const e = existing[label];
        return [label, Number.isFinite(c) ? c : 0, e] as [string, number, number | undefined];
      })
      .filter(([, c, e]) => (Number.isFinite(c) && c > 0) || (e != null && Number.isFinite(e) && e > 0));
  };

  return (
    <div className={className ?? "-mx-2 rounded-md border border-white/10 bg-sky-900/30 px-3 py-2"}>
      <div className="border-b border-white/10 pb-1 text-sm font-semibold text-white">Upkeep</div>

      {!hasAny ? (
        <div className="mt-2 text-xs text-white/70">None</div>
      ) : (
        <div className="mt-2 space-y-2">
          {CATEGORY_ORDER.map((category) => {
            const entries = categoryResources(category)
              .slice()
              .sort(([a], [b]) => a.localeCompare(b));
            if (entries.length === 0) return null;

            const meta = CATEGORY_META[category];
            const Icon = meta.Icon;

            const columns = entries.length;
            const gridStyle: React.CSSProperties = {
              gridTemplateColumns: `24px repeat(${columns}, 56px)`,
              gridTemplateRows: "repeat(2, 24px)",
            };

            return (
              <div key={category} className="overflow-x-auto">
                <div className="grid items-center gap-x-3 gap-y-0" style={gridStyle}>
                  <div className="row-span-2 flex h-12 items-center justify-start">
                    {iconBadge(meta.title, <Icon className="h-4 w-4" aria-label={meta.title} />)}
                  </div>

                  {entries.map(([label, currentAmount, existingAmount]) => {
                    const hasCurrent = Number.isFinite(currentAmount) && currentAmount > 0;
                    const hasExisting = existingAmount != null && Number.isFinite(existingAmount) && existingAmount > 0;

                    const isNew = hasCurrent && !hasExisting;
                    const isRemoved = hasExisting && !hasCurrent;

                    const borderClass = isNew ? "border-green-400" : isRemoved ? "border-red-400" : "border-white/10";

                    return (
                      <div
                        key={`${category}:${label}:resource`}
                        className={`row-span-2 flex h-12 w-full flex-col items-center justify-center rounded border ${borderClass} box-border`}>
                        <div className="flex h-6 items-center justify-center">{resourceIcon(label)}</div>
                        <div className="flex h-6 items-center justify-center text-center text-xs tabular-nums leading-none text-white">
                          <ResourceAmount current={currentAmount} existing={existingAmount} baselineProvided={baselineProvided} />
                        </div>
                      </div>
                    );
                  })}
                </div>
              </div>
            );
          })}
        </div>
      )}
    </div>
  );
}
