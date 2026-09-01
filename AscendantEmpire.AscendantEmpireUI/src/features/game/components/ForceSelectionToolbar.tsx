import { useEffect, useRef } from "react";
import Tooltip from "@/components/ui/Tooltip";
import ObservedForce from "@/features/map/components/ObservedForce";
import { TILE_SIZE_PX } from "@/features/map/components/TileGrid";
import type { ObservedForceTile } from "@/features/map/components/TileTypes";

export interface ForceSelectionToolbarTile {
  x: number;
  y: number;
  /**
   * The forces to offer. Only ever the player's own — the caller filters by ownership before building this, so
   * an enemy force sharing the tile is drawn on the map but never selectable here.
   */
  forces: ObservedForceTile[];
}

export interface ForceSelectionToolbarProps {
  visible: boolean;
  tile: ForceSelectionToolbarTile | null;
  selectedForceId?: number;
  unitImageSrcByUnitTemplateId?: ReadonlyMap<number, string>;

  /**
   * Template names, for the hover tooltip. The template's name ("Settler"), not a name for the individual unit —
   * `UnitInstance.Name` exists for that but nothing sets one yet.
   */
  unitNameByUnitTemplateId?: ReadonlyMap<number, string>;

  /**
   * Adds a checkbox above each force, turning the bar into a multi-select.
   *
   * The tick is a separate channel from the selection: checking a force must not change which force is selected.
   * Nothing sets this yet — it is what split and join (TM-4) will drive.
   */
  selectionRequired?: boolean;
  checkedForceIds?: ReadonlySet<number>;

  onSelect: (force: ObservedForceTile) => void;
  onCheckedChange?: (forceId: number, checked: boolean) => void;
}

/** The accessible name for a force's button. */
function buildForceLabel(force: ObservedForceTile): string {
  return `Force #${force.id} — ${force.healthyTroops} healthy, ${force.injuredTroops} injured`;
}

/**
 * The bar of the player's forces on the selected tile, docked under the context action toolbar. Clicking a force
 * selects it; that is the whole selection mechanism, replacing the modal this used to be.
 */
export default function ForceSelectionToolbar({
  visible,
  tile,
  selectedForceId,
  unitImageSrcByUnitTemplateId,
  unitNameByUnitTemplateId,
  selectionRequired = false,
  checkedForceIds,
  onSelect,
  onCheckedChange,
}: ForceSelectionToolbarProps) {
  const selectedEntryRef = useRef<HTMLDivElement | null>(null);

  // The bar scrolls horizontally, and the selection is usually set by clicking the tile rather than the bar, so the
  // highlighted entry can easily start out of view. "nearest" scrolls only when it actually is, and never moves the
  // page. Optional-called because jsdom doesn't implement scrollIntoView.
  useEffect(() => {
    selectedEntryRef.current?.scrollIntoView?.({ block: "nearest", inline: "nearest" });
  }, [selectedForceId]);

  if (!visible || tile == null || tile.forces.length === 0) return null;

  // Forces that can still act come first, so a scrolling bar shows the useful ones without the player hunting.
  // The id tiebreak is what makes the order stable: tile.forces arrives in whatever order the query returned it,
  // and without a second key the buttons would shuffle under the cursor on every refetch. Ordering does shift as
  // forces spend their movement during a turn — that is the point of the first key.
  const orderedForces = [...tile.forces].sort(
    (a, b) => Number(b.hasMovementRemaining) - Number(a.hasMovementRemaining) || a.id - b.id,
  );

  return (
    <div className="force-selection-toolbar" aria-label="Force selection toolbar">
      {orderedForces.map((force) => {
        const isSelected = selectedForceId === force.id;

        return (
          <div key={force.id} className="force-selection-toolbar__force" ref={isSelected ? selectedEntryRef : undefined}>
            {/* Deliberately a sibling of the button, not a child: an input inside a button is invalid markup, and
                clicking it would also fire onSelect — exactly wrong when the tick means "include in the split". */}
            {selectionRequired ? (
              <input
                type="checkbox"
                className="force-selection-toolbar__checkbox"
                checked={checkedForceIds?.has(force.id) ?? false}
                onChange={(event) => onCheckedChange?.(force.id, event.currentTarget.checked)}
                aria-label={`Include ${buildForceLabel(force)}`}
              />
            ) : null}

            {/* Tooltip renders its children bare when content is undefined, so a template whose name hasn't
                arrived (or resolved) simply gets no tooltip rather than an empty bubble. */}
            <Tooltip content={unitNameByUnitTemplateId?.get(force.unitTemplateId)}>
              <button
                type="button"
                className="force-selection-toolbar__button"
                aria-pressed={isSelected}
                // ObservedForce is aria-hidden and the troop count alone says nothing, so without this the button
                // has no accessible name at all.
                aria-label={buildForceLabel(force)}
                onClick={() => onSelect(force)}>
                {/* ObservedForce is absolutely positioned within its parent, so this span has to be sized and
                    relative. Sized to a map tile, and from the same constant the grid uses: ObservedForce insets
                    and scales itself from whatever box it is given, so making that box a tile is what makes the
                    unit render here at exactly the size it does on the map — and keeps it that way if the tile
                    size ever changes. The spent modifier fades it — and only it, so the count stays legible. */}
                <span
                  style={{ width: TILE_SIZE_PX, height: TILE_SIZE_PX }}
                  className={[
                    "force-selection-toolbar__marker",
                    force.hasMovementRemaining ? "" : "force-selection-toolbar__marker--spent",
                  ]
                    .filter(Boolean)
                    .join(" ")}>
                  {/* isSelected is deliberately not passed. It draws the map's yellow corner brackets and glow,
                      which earn their place picking a unit out of a crowded grid but are noise on a tile sitting
                      in a row of tiles — the button's own border and tint already say which entry is selected. */}
                  <ObservedForce
                    force={force}
                    unitInstanceCount={1}
                    unitImageSrcByUnitTemplateId={unitImageSrcByUnitTemplateId}
                  />
                </span>

                <span className="force-selection-toolbar__troops">{force.healthyTroops + force.injuredTroops}</span>
              </button>
            </Tooltip>
          </div>
        );
      })}
    </div>
  );
}
