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

  /**
   * Every force currently selected. A set rather than a single id because the bar is a multi-select: several
   * entries can be pressed at once, and an order taken from the action toolbar applies to all of them.
   */
  selectedForceIds?: ReadonlySet<number>;
  unitImageSrcByUnitTemplateId?: ReadonlyMap<number, string>;

  /**
   * Template names, for the hover tooltip. The template's name ("Settler"), not a name for the individual unit —
   * `UnitInstance.Name` exists for that but nothing sets one yet.
   */
  unitNameByUnitTemplateId?: ReadonlyMap<number, string>;

  /**
   * Reports a click on a force, and whether it was modified.
   *
   * `append` is true for a ctrl (or ⌘) click. What that *means* is the caller's decision — the bar reports the
   * gesture, not the intent — but it is what turns the click into "add this force to the selection, or take it
   * out again" rather than "select only this force".
   */
  onSelect: (force: ObservedForceTile, append: boolean) => void;
}

/**
 * Formats a movement-point value for display, trimming the trailing zero off whole numbers.
 *
 * Costs are authored to one decimal place (desert's 1.1), so "1.5" and "2" both need to read naturally.
 */
function formatMovementPoints(points: number): string {
  return Number.isInteger(points) ? String(points) : points.toFixed(1);
}

/**
 * Describes a force's remaining movement as a fraction of a full turn, or null when we aren't told.
 *
 * Null for forces the player doesn't own — the API masks the numbers rather than reveal how far an enemy could still
 * travel — and the caller renders nothing at all in that case rather than inventing a figure.
 */
function describeMovement(force: ObservedForceTile): string | null {
  if (force.campaignMovementRemaining == null || force.campaignMovementPoints == null) return null;

  return `${formatMovementPoints(force.campaignMovementRemaining)} / ${formatMovementPoints(force.campaignMovementPoints)}`;
}

/** The accessible name for a force's button. */
function buildForceLabel(force: ObservedForceTile): string {
  const movement = describeMovement(force);
  const movementSuffix = movement == null ? "" : `, ${movement} movement points`;

  return `Force #${force.id} — ${force.healthyTroops} healthy, ${force.injuredTroops} injured${movementSuffix}`;
}

/**
 * The bar of the player's forces on the selected tile, docked under the context action toolbar. Clicking a force
 * selects it and ctrl+clicking adds or removes one; that is the whole selection mechanism, replacing the modal
 * this used to be.
 */
export default function ForceSelectionToolbar({
  visible,
  tile,
  selectedForceIds,
  unitImageSrcByUnitTemplateId,
  unitNameByUnitTemplateId,
  onSelect,
}: ForceSelectionToolbarProps) {
  const selectedEntryRef = useRef<HTMLDivElement | null>(null);

  // The bar scrolls horizontally, and the selection is usually set by clicking the tile rather than the bar, so the
  // highlighted entry can easily start out of view. "nearest" scrolls only when it actually is, and never moves the
  // page. Optional-called because jsdom doesn't implement scrollIntoView.
  //
  // The ref is on the first selected entry in render order, not on every one — the scroll matters when the
  // selection was set by clicking the *tile*, where there is exactly one. A player ctrl+clicking is already
  // looking at the bar. The key is joined so the effect re-runs when the selection changes rather than on every
  // render, which a raw Set identity would cause.
  const selectionKey = selectedForceIds == null ? "" : [...selectedForceIds].sort((a, b) => a - b).join(",");

  useEffect(() => {
    selectedEntryRef.current?.scrollIntoView?.({ block: "nearest", inline: "nearest" });
  }, [selectionKey]);

  if (!visible || tile == null || tile.forces.length === 0) return null;

  // Forces that can still act come first, so a scrolling bar shows the useful ones without the player hunting.
  // The id tiebreak is what makes the order stable: tile.forces arrives in whatever order the query returned it,
  // and without a second key the buttons would shuffle under the cursor on every refetch. Ordering does shift as
  // forces spend their movement during a turn — that is the point of the first key.
  const orderedForces = [...tile.forces].sort(
    (a, b) => Number(b.hasCampaignMovementRemaining) - Number(a.hasCampaignMovementRemaining) || a.id - b.id,
  );

  // Tracked while mapping so the scroll ref lands on the first selected entry without a second pass over the list.
  let hasMarkedSelectedEntry = false;

  return (
    <div className="force-selection-toolbar" aria-label="Force selection toolbar">
      {orderedForces.map((force) => {
        const isSelected = selectedForceIds?.has(force.id) ?? false;
        const isScrollTarget = isSelected && !hasMarkedSelectedEntry;

        if (isScrollTarget) {
          hasMarkedSelectedEntry = true;
        }

        return (
          <div
            key={force.id}
            className="force-selection-toolbar__force"
            ref={isScrollTarget ? selectedEntryRef : undefined}>
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
                // metaKey as well as ctrlKey: on macOS ctrl+click is the system context-menu gesture, so ⌘ is what
                // a Mac player will reach for. Both are read here rather than in the hook so the DOM event stays
                // inside the presenter.
                onClick={(event) => onSelect(force, event.ctrlKey || event.metaKey)}>
                {/* ObservedForce is absolutely positioned within its parent, so this span has to be sized and
                    relative. Sized to a map tile, and from the same constant the grid uses: ObservedForce insets
                    and scales itself from whatever box it is given, so making that box a tile is what makes the
                    unit render here at exactly the size it does on the map — and keeps it that way if the tile
                    size ever changes. The spent modifier fades it — and only it, so the count stays legible. */}
                <span
                  style={{ width: TILE_SIZE_PX, height: TILE_SIZE_PX }}
                  className={[
                    "force-selection-toolbar__marker",
                    force.hasCampaignMovementRemaining ? "" : "force-selection-toolbar__marker--spent",
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

                {/* Omitted entirely when masked, rather than shown as a dash or a zero: for a force we don't own, how
                    far it can still go is something we were deliberately not told. Already in the button's accessible
                    name above, so this span is decorative. */}
                {describeMovement(force) != null && (
                  <span className="force-selection-toolbar__movement" aria-hidden="true">
                    {describeMovement(force)} MP
                  </span>
                )}
              </button>
            </Tooltip>
          </div>
        );
      })}
    </div>
  );
}
