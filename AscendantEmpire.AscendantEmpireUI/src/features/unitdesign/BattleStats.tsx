import type { ReactNode } from "react";
import type { DamageType } from "./types/DamageType";
import type { IconType } from "react-icons";
import type { DamageProfile, DomainType } from "./types/UnitComponent";
import Tooltip from "@/components/ui/Tooltip";
import { formatToOneDecimal } from "@/util/number";

import { ALL_DAMAGE_TYPES, rawToScaled } from "./types/UnitComponent";
import { DAMAGE_TYPE_BADGES } from "./types/damageTypeBadges";
import { PiSword } from "react-icons/pi";
import { BsShield } from "react-icons/bs";

type IconBadgeMeta = { title: string; Icon: IconType };

const ROW_BADGES = {
  attack: { title: "Attack", Icon: PiSword },
  resistance: { title: "Resistance", Icon: BsShield },
} satisfies Record<"attack" | "resistance", IconBadgeMeta>;

/**
 * The battle data shown for a component. Weapons supply `attack` (plus range/domain modifiers);
 * armour supplies `resistance`. Whichever fields are present get a row.
 */
export interface BattleStatsData {
  attack?: DamageProfile;
  resistance?: DamageProfile;
  minimumRange?: number;
  maximumRange?: number;
  domainModifiers?: Partial<Record<DomainType, number>>;
}

function round1(n: number) {
  return Math.round(n * 10) / 10;
}

// Damage profiles are stored as raw Scale-1,000 fixed-point; convert to friendly decimals for display.
function DamageCell({ current, existing }: { current: number; existing?: number }) {
  const currentScaled = rawToScaled(current);
  if (existing == null) return <>{formatToOneDecimal(currentScaled)}</>;

  const currentRounded = round1(currentScaled);
  const existingRounded = round1(rawToScaled(existing));
  const deltaRounded = round1(currentRounded - existingRounded);

  if (!Number.isFinite(deltaRounded)) return <>{formatToOneDecimal(deltaRounded)}</>;

  const className = deltaRounded === 0 ? "text-white" : deltaRounded < 0 ? "text-red-400" : "text-green-400";
  return <span className={className}>{formatToOneDecimal(Math.abs(deltaRounded))}</span>;
}

function DamageRow({
  label,
  values,
  existingValues,
}: {
  label: ReactNode;
  values: DamageProfile;
  existingValues?: DamageProfile;
}) {
  return (
    <div className="flex items-center justify-between gap-3 text-xs">
      <div className="text-white/80">{label}</div>
      <div className="flex items-center gap-2 tabular-nums text-white">
        {ALL_DAMAGE_TYPES.map((dt) => (
          <div key={dt} className="min-w-12 text-center">
            <DamageCell current={values[dt]} existing={existingValues?.[dt]} />
          </div>
        ))}
      </div>
    </div>
  );
}

export function BattleStats({ stats, existingStats, className }: { stats: BattleStatsData; existingStats?: BattleStatsData; className?: string }) {
  const iconBadge = (title: string, icon: ReactNode) => (
    <Tooltip content={title}>
      <span className="inline-flex w-full justify-center">
        <span className="inline-flex items-center justify-center rounded bg-black p-1 text-white">{icon}</span>
      </span>
    </Tooltip>
  );

  const damageTypeBadge = (dt: DamageType) => {
    const meta = DAMAGE_TYPE_BADGES[dt];
    if (!meta) return dt;

    const Icon = meta.Icon;
    return iconBadge(meta.title, <Icon className="h-4 w-4" aria-label={meta.title} />);
  };

  // Domain modifiers are rendered as a compact "Land ×1, Air ×0.5" list. Multipliers are raw Scale-1,000.
  const domainModifierEntries = Object.entries(stats.domainModifiers ?? {}) as [DomainType, number][];

  return (
    <div className={className ?? "-mx-2 rounded-md border border-white/10 bg-sky-900/30 px-3 py-2"}>
      <div className="border-b border-white/10 pb-0.5 text-sm font-semibold text-white">Battle Stats</div>

      <div className="mt-2 space-y-2">
        <div className="flex items-center justify-end gap-2 text-xs text-white/70">
          {ALL_DAMAGE_TYPES.map((dt) => (
            <div key={dt} className="min-w-12 text-center">
              {damageTypeBadge(dt)}
            </div>
          ))}
        </div>

        {stats.attack && (
          <DamageRow
            label={iconBadge(ROW_BADGES.attack.title, <ROW_BADGES.attack.Icon className="h-4 w-4" aria-label={ROW_BADGES.attack.title} />)}
            values={stats.attack}
            existingValues={existingStats?.attack}
          />
        )}

        {stats.resistance && (
          <DamageRow
            label={iconBadge(ROW_BADGES.resistance.title, <ROW_BADGES.resistance.Icon className="h-4 w-4" aria-label={ROW_BADGES.resistance.title} />)}
            values={stats.resistance}
            existingValues={existingStats?.resistance}
          />
        )}

        {stats.maximumRange != null && (
          <div className="flex items-center justify-between gap-3 text-xs text-white/80">
            <div>Range</div>
            <div className="tabular-nums text-white">
              {/* Melee shows a single word; ranged shows the engageable band (min–max). */}
              {stats.maximumRange <= 1 ? "Melee" : `${stats.minimumRange ?? 0}–${stats.maximumRange}`}
            </div>
          </div>
        )}

        {domainModifierEntries.length > 0 && (
          <div className="flex items-center justify-between gap-3 text-xs text-white/80">
            <div>Targets</div>
            <div className="text-white">{domainModifierEntries.map(([d, m]) => `${d} ×${rawToScaled(m)}`).join(", ")}</div>
          </div>
        )}
      </div>
    </div>
  );
}
