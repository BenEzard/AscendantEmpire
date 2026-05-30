import type { ReactNode } from "react";
import type { DamageType } from "./types/DamageType";
import type { IconType } from "react-icons";
import type { WeaponStats } from "./types/UnitComponent";
import Tooltip from "@/components/ui/Tooltip";

import { ALL_DAMAGE_TYPES } from "./types/UnitComponent";
import { GiFlatHammer, GiFlame, GiPoisonBottle, GiQuickSlash, GiSunSpear } from "react-icons/gi";
import { PiSword } from "react-icons/pi";
import { BsShield } from "react-icons/bs";

type IconBadgeMeta = { title: string; Icon: IconType };

const DAMAGE_BADGES: Partial<Record<DamageType, IconBadgeMeta>> = {
  slash: { title: "Slash", Icon: GiQuickSlash },
  pierce: { title: "Pierce", Icon: GiSunSpear },
  burn: { title: "Burn", Icon: GiFlame },
  crush: { title: "Crush", Icon: GiFlatHammer },
  gas: { title: "Poison", Icon: GiPoisonBottle },
};

const ROW_BADGES = {
  attack: { title: "Attack", Icon: PiSword },
  defense: { title: "Defence", Icon: BsShield },
} satisfies Record<"attack" | "defense", IconBadgeMeta>;

function fmt(n: number) {
  if (!Number.isFinite(n)) return String(n);
  return n.toFixed(1);
}

function round1(n: number) {
  return Math.round(n * 10) / 10;
}

function DamageCell({ current, existing }: { current: number; existing?: number }) {
  if (existing == null) return <>{fmt(current)}</>;

  const currentRounded = round1(current);
  const existingRounded = round1(existing);
  const deltaRounded = round1(currentRounded - existingRounded);

  if (!Number.isFinite(deltaRounded)) return <>{fmt(deltaRounded)}</>;

  const className = deltaRounded === 0 ? "text-white" : deltaRounded < 0 ? "text-red-400" : "text-green-400";
  return <span className={className}>{fmt(Math.abs(deltaRounded))}</span>;
}

function DamageRow({
  label,
  values,
  existingValues,
}: {
  label: ReactNode;
  values: Record<DamageType, number>;
  existingValues?: Record<DamageType, number>;
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

export function BattleStats({ stats, existingStats, className }: { stats: WeaponStats; existingStats?: WeaponStats; className?: string }) {
  const iconBadge = (title: string, icon: ReactNode) => (
    <Tooltip content={title}>
      <span className="inline-flex w-full justify-center">
        <span className="inline-flex items-center justify-center rounded bg-black p-1 text-white">{icon}</span>
      </span>
    </Tooltip>
  );

  const damageTypeBadge = (dt: DamageType) => {
    const meta = DAMAGE_BADGES[dt];
    if (!meta) return dt;

    const Icon = meta.Icon;
    return iconBadge(meta.title, <Icon className="h-4 w-4" aria-label={meta.title} />);
  };

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

        <DamageRow
          label={iconBadge(ROW_BADGES.attack.title, <ROW_BADGES.attack.Icon className="h-4 w-4" aria-label={ROW_BADGES.attack.title} />)}
          values={stats.attack}
          existingValues={existingStats?.attack}
        />
        <DamageRow
          label={iconBadge(ROW_BADGES.defense.title, <ROW_BADGES.defense.Icon className="h-4 w-4" aria-label={ROW_BADGES.defense.title} />)}
          values={stats.defense}
          existingValues={existingStats?.defense}
        />
      </div>
    </div>
  );
}
