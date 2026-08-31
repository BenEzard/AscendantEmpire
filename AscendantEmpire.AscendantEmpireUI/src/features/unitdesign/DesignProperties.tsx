import type { DesignAggregate } from "./model/unitDesign";
import { BattleStats } from "./BattleStats";
import { UpkeepStats } from "./UpkeepStats";
import { TrainingCostStats } from "./TrainingCostStats";
import { rawToScaled } from "./types/UnitComponent";
import { resolveUnitMapImageSrc } from "@/features/map/util/unitDisplayImageResolver";
import Tooltip from "@/components/ui/Tooltip";
import { formatToOneDecimal } from "@/util/number";

/** Mirrors UnitTemplate.MAP_IMAGE_PATH_MAX_LENGTH on the server. */
const MAP_IMAGE_PATH_MAX_LENGTH = 500;

export interface DesignPropertiesProps {
  aggregate: DesignAggregate;
  /**
   * The template's map sprite path. Only meaningful alongside onMapImagePathChange — without a change
   * handler the Map Image card isn't rendered and this value is ignored.
   */
  mapImagePath?: string;
  /**
   * Supplied only by the developer host (the Entity Setup Tool). Its presence is what makes the
   * developer-only Map Image card appear; the in-game player build passes neither prop.
   */
  onMapImagePathChange?: (next: string) => void;
}

/**
 * A labelled Scale-1,000 summary row (values are raw and shown as decimals). An optional `tooltip` explains
 * where a derived figure comes from; rows that simply sum their components don't need one.
 *
 * Styled to match the panel's other cards: same text size, and `tabular-nums` (without a monospace face) so
 * the values still line up in their column.
 */
function SummaryRow({ label, raw, tooltip }: { label: string; raw: number; tooltip?: string }) {
  return (
    <div className="flex items-center justify-between gap-3 text-xs">
      <div className="text-white/80">{tooltip ? <Tooltip content={tooltip}>{label}</Tooltip> : label}</div>
      <div className="tabular-nums text-white">{formatToOneDecimal(rawToScaled(raw))}</div>
    </div>
  );
}

/**
 * Developer-only editor for the template's map sprite — the image this unit is drawn as on the campaign
 * map, as opposed to the full-size artwork the designer canvas shows.
 *
 * The thumbnail deliberately resolves the path through the campaign map's own resolver rather than dropping
 * it into a plain img src. Map sprites are bundled out of src/assets/units, not served from public/, so a
 * raw src would show nothing even for a perfectly valid path — and, worse, would happily display a
 * public/ designer image that the map itself could never resolve. Going through the real resolver makes
 * the thumbnail an honest answer to "will this render on the map?": if it shows here, it shows there.
 * That is also why nothing is said on success and only failures get text — the image is the message.
 */
function MapImageCard({ mapImagePath, onChange }: { mapImagePath: string; onChange: (next: string) => void }) {
  const trimmedPath = mapImagePath.trim();
  const resolvedSrc = trimmedPath === "" ? undefined : resolveUnitMapImageSrc(trimmedPath);

  return (
    <div className="-mx-2 space-y-1 rounded-md border border-white/10 bg-sky-900/30 px-3 py-2">
      <div className="border-b border-white/10 pb-1 text-sm font-semibold text-white">Map Image</div>

      <div className="mt-1 space-y-2">
        {/* The sprite sits to the right of the box, so a resolving path speaks for itself and needs no
            accompanying text — the thumbnail appearing beside what you typed is the confirmation. */}
        <div className="flex items-center gap-2">
          <input
            className="min-w-0 flex-1 rounded-md border border-white/20 bg-white/10 px-2 py-1 text-sm text-white placeholder-white/50 focus:outline-none focus-visible:ring-2 focus-visible:ring-white/30"
            value={mapImagePath}
            onChange={(event) => onChange(event.target.value)}
            placeholder="e.g. ancient-spearman_40px.png"
            maxLength={MAP_IMAGE_PATH_MAX_LENGTH}
            spellCheck={false}
          />
          {resolvedSrc && <img src={resolvedSrc} alt="Map sprite preview" className="h-10 w-10 shrink-0 object-contain" />}
        </div>

        {/* Errors only. An empty box isn't flagged — that's just the starting state of every new design,
            and the disabled Save already says it's required. A non-empty path that resolves to nothing is
            worth shouting about: the server only checks MapImagePath is non-empty, so a typo saves happily
            and leaves the unit sprite-less on the map with nothing else to warn you. */}
        {trimmedPath !== "" && !resolvedSrc && (
          <p className="text-xs text-amber-300">No sprite found in src/assets/units for this path — the unit will have no image on the map.</p>
        )}
      </div>
    </div>
  );
}

export default function DesignProperties({ aggregate, mapImagePath = "", onMapImagePathChange }: DesignPropertiesProps) {
  const { summary, battleStats, cost, upkeep } = aggregate;

  const showBattleStats =
    Object.values(battleStats.attack ?? {}).some((n) => Number.isFinite(n) && n > 0) ||
    Object.values(battleStats.resistance ?? {}).some((n) => Number.isFinite(n) && n > 0);

  return (
    <div className="flex h-full w-full flex-col border-l border-white/10 bg-sky-900/20" style={{ minHeight: 0 }}>
      <div className="border-b border-white/10 px-4 py-3 text-sm font-semibold text-white">Design Properties</div>

      <div className="flex-1 overflow-auto px-4 py-3 text-sm text-white" style={{ minHeight: 0 }}>
        <div className="space-y-3">
          {/* Developer-only: absent entirely in the player build (no change handler is passed). Sits at the
              top because it's an authoring field rather than a derived stat. */}
          {onMapImagePathChange && <MapImageCard mapImagePath={mapImagePath} onChange={onMapImagePathChange} />}

          {/* Real, canonical-backed summary stats. */}
          <div className="-mx-2 space-y-1 rounded-md border border-white/10 bg-sky-900/30 px-3 py-2">
            <div className="border-b border-white/10 pb-1 text-sm font-semibold text-white">Summary</div>
            <div className="mt-1 space-y-1">
              <SummaryRow label="Weight" raw={summary.weight} />
              {/* Battle movement is derived, not authored, so the tooltip carries the rule that produced the
                  number — otherwise a figure that drops as you fit components looks arbitrary. */}
              <SummaryRow
                label="Battle Movement"
                raw={summary.battleMovementPoints}
                tooltip="Tiles per battle turn: the body's Power less the weight of everything fitted to it."
              />
              <SummaryRow label="Hitpoints" raw={summary.hitpoints} />
              <SummaryRow label="Detection" raw={summary.detection} />
              <SummaryRow label="Visibility" raw={summary.visibility} />
            </div>
          </div>

          {/* PLACEHOLDER_COST_UPKEEP: cost/upkeep aren't modelled server-side yet — shown from stand-ins. */}
          <TrainingCostStats cost={cost} />

          <UpkeepStats upkeep={upkeep} />

          {showBattleStats && <BattleStats stats={battleStats} className="-mx-2 rounded-md border border-white/10 bg-sky-900/30 px-3 py-2" />}
        </div>
      </div>
    </div>
  );
}
