import { Fragment, useState } from "react";
import { FaPlus, FaScissors, FaUsers } from "react-icons/fa6";
import type { SplitAtom, SplitGroup } from "./splitForceModel";

export interface SplitForceGroupRowProps {
  /** The Forces the split would produce, in row order, already carrying their share of the casualties. */
  groups: readonly SplitGroup[];

  atoms: readonly SplitAtom[];

  /** What the Force is, for each quantum's tooltip — "Settler". */
  unitName: string;

  /** The Force's map image, shown as the atom icon. Falls back to a generic figure when the image isn't loaded. */
  unitImageSrc?: string;

  /** Cut the identified group immediately before the given (absolute) atom index. */
  onCut: (groupId: string, atomIndex: number) => void;

  /** Rejoin two neighbouring groups into one. */
  onMerge: (groupId: string, otherGroupId: string) => void;

  /** Move one quantum out of whichever group holds it and into the identified group. */
  onMoveAtom: (atomIndex: number, targetGroupId: string) => void;
}

/**
 * The division as it currently stands: one bordered box per Force the split would produce, laid out in a row.
 *
 * Three gestures compose a division, and each is aimed at the thing it changes rather than at a control standing
 * in for it. Hovering the gap *inside* a box draws the line a cut would fall on; hovering the gap *between* two
 * boxes offers the plus that rejoins them; and a quantum can be dragged from any box into any other, which is the
 * only way to rebalance groups without taking the Force apart and recutting it.
 *
 * Cutting and rejoining are buttons, so every division remains reachable by keyboard — dragging is a shortcut for
 * what recutting can already do, never the only route to a composition.
 */
export default function SplitForceGroupRow({ groups, atoms, unitName, unitImageSrc, onCut, onMerge, onMoveAtom }: SplitForceGroupRowProps) {
  // The quantum currently in the player's hand, and the box it is hovering over. Both are presentation only: the
  // move itself is one call on drop, so an abandoned drag leaves the composition exactly as it was.
  const [draggedAtomIndex, setDraggedAtomIndex] = useState<number | null>(null);
  const [dragOverGroupId, setDragOverGroupId] = useState<string | null>(null);

  const draggedFromGroupId = draggedAtomIndex == null ? null : (groups.find((group) => group.atomIndexes.includes(draggedAtomIndex))?.groupId ?? null);

  const endDrag = () => {
    setDraggedAtomIndex(null);
    setDragOverGroupId(null);
  };

  return (
    // Wraps rather than scrolls: a force cut many ways reads better as stacked rows than as a wide filmstrip.
    <div className="flex flex-wrap items-stretch">
      {groups.map((group, groupPosition) => {
        const previousGroup = groups[groupPosition - 1];

        // A box the dragged quantum could actually land in. Its own box is not one, and neither is any box while
        // nothing is being dragged.
        const canAcceptDrop = draggedAtomIndex != null && draggedFromGroupId !== group.groupId;

        return (
          <Fragment key={group.groupId}>
            {previousGroup != null ? (
              // Only the plus merges. A button the height of the box would rejoin two groups from a click anywhere
              // beside them, which is a lot of dialog to misfire on.
              <div className="flex w-4 items-center justify-center">
                <button
                  type="button"
                  className="flex h-5 w-5 cursor-pointer items-center justify-center rounded-sm text-slate-400 transition-colors hover:bg-emerald-50 hover:text-emerald-600 focus:outline-none focus-visible:text-emerald-600"
                  aria-label={`Rejoin group ${groupPosition} and group ${groupPosition + 1}`}
                  title="Rejoin these two groups"
                  onClick={() => onMerge(previousGroup.groupId, group.groupId)}>
                  <FaPlus aria-hidden="true" className="text-xs" />
                </button>
              </div>
            ) : null}

            <SplitForceGroupBox
              group={group}
              label={describeGroup(groupPosition, group)}
              atoms={atoms}
              unitName={unitName}
              unitImageSrc={unitImageSrc}
              canAcceptDrop={canAcceptDrop}
              isDragTarget={canAcceptDrop && dragOverGroupId === group.groupId}
              onCut={onCut}
              onAtomDragStart={setDraggedAtomIndex}
              onAtomDragEnd={endDrag}
              onDragEnter={() => setDragOverGroupId(group.groupId)}
              onDrop={() => {
                if (draggedAtomIndex != null) {
                  onMoveAtom(draggedAtomIndex, group.groupId);
                }

                endDrag();
              }}
            />
          </Fragment>
        );
      })}
    </div>
  );
}

/**
 * What a screen reader hears for one box.
 *
 * It carries the casualty split, because the box shows that only as a bar — and a bar announces nothing.
 */
function describeGroup(groupPosition: number, group: SplitGroup): string {
  const casualties = group.injuredCount > 0 ? `, ${group.healthyCount.toLocaleString()} healthy and ${group.injuredCount.toLocaleString()} injured` : "";

  return `Group ${groupPosition + 1} — ${group.size.toLocaleString()} soldiers${casualties}`;
}

interface SplitForceGroupBoxProps {
  group: SplitGroup;

  /** Names the box for a screen reader, and for a drag that has to say which box it landed in. */
  label: string;

  atoms: readonly SplitAtom[];
  unitName: string;
  unitImageSrc?: string;
  canAcceptDrop: boolean;
  isDragTarget: boolean;
  onCut: (groupId: string, atomIndex: number) => void;
  onAtomDragStart: (atomIndex: number) => void;
  onAtomDragEnd: () => void;
  onDragEnter: () => void;
  onDrop: () => void;
}

/** One Force the split would produce: its strength, and the quanta it is made of. */
function SplitForceGroupBox({
  group,
  label,
  atoms,
  unitName,
  unitImageSrc,
  canAcceptDrop,
  isDragTarget,
  onCut,
  onAtomDragStart,
  onAtomDragEnd,
  onDragEnter,
  onDrop,
}: SplitForceGroupBoxProps) {
  // Each atom's bar reads its *group's* health, because the wounded are apportioned across the Forces the split
  // produces rather than down to the quantum — an atom is a slice of a Force, not a Force.
  const healthyPercent = group.size > 0 ? (group.healthyCount / group.size) * 100 : 0;
  const healthTitle = `${group.healthyCount.toLocaleString()} healthy · ${group.injuredCount.toLocaleString()} injured`;

  // A group's last quantum cannot leave: that would empty the group, and rejoining two boxes is what does that.
  const canDragAtoms = group.atomIndexes.length > 1;

  const boxClasses = ["rounded-md border bg-white p-2 transition-colors"];

  if (isDragTarget) {
    boxClasses.push("border-sky-500 ring-2 ring-sky-200");
  } else if (canAcceptDrop) {
    boxClasses.push("border-dashed border-sky-400");
  } else {
    boxClasses.push("border-slate-300");
  }

  return (
    <div
      role="group"
      aria-label={label}
      className={boxClasses.join(" ")}
      // preventDefault is what marks the box as a place a drop may land; without it the browser refuses the drop.
      onDragOver={(event) => {
        if (canAcceptDrop) {
          event.preventDefault();
          onDragEnter();
        }
      }}
      onDrop={(event) => {
        event.preventDefault();
        onDrop();
      }}>
      {/* The strength alone. How it divides into healthy and injured is the bars' job, and the box's own label carries
          it for a screen reader, which is why nothing here says it in text. */}
      <div className="mb-1 text-center text-sm font-semibold text-slate-800">{group.size.toLocaleString()}</div>

      <div className="flex flex-wrap items-stretch">
        {group.atomIndexes.map((atomIndex, atomPosition) => {
          const atom = atoms[atomIndex];

          if (atom == null) {
            return null;
          }

          return (
            <Fragment key={atomIndex}>
              {atomPosition > 0 ? (
                // The gap between two icons *is* the control: hovering it draws the line the cut would fall on, so
                // the player aims at the division itself. It is a real button, which is what keeps the cut
                // reachable by keyboard and nameable to a screen reader.
                <button
                  type="button"
                  className="group/cut flex w-4 cursor-pointer flex-col items-center rounded-sm focus:outline-none"
                  aria-label={`Cut before soldier group ${atomIndex + 1}`}
                  title="Cut here"
                  onClick={() => onCut(group.groupId, atomIndex)}>
                  {/* The scissors caps the line rather than standing beside the icons, so the whole affordance — what
                      it does, and exactly where it would fall — appears as one mark. */}
                  <FaScissors
                    aria-hidden="true"
                    className="text-[10px] text-transparent transition-colors group-hover/cut:text-rose-500 group-focus-visible/cut:text-rose-500"
                  />
                  <span
                    aria-hidden="true"
                    className="w-0 flex-1 border-l-2 border-dashed border-transparent transition-colors group-hover/cut:border-rose-500 group-focus-visible/cut:border-rose-500"
                  />
                </button>
              ) : null}

              <div
                className={["flex w-11 flex-col items-center", canDragAtoms ? "cursor-grab active:cursor-grabbing" : ""].join(" ")}
                draggable={canDragAtoms}
                onDragStart={(event) => {
                  // Firefox starts no drag at all without payload, even where the handler never reads it back.
                  event.dataTransfer.setData("text/plain", String(atomIndex));
                  event.dataTransfer.effectAllowed = "move";
                  onAtomDragStart(atomIndex);
                }}
                onDragEnd={onAtomDragEnd}>
                {/* The unit's own name, and nothing else: the count sits under the icon and the health under that, so a
                    tooltip repeating either would only say twice what the box already shows. */}
                <div className="flex h-10 w-10 items-center justify-center overflow-hidden" title={unitName}>
                  {unitImageSrc ? (
                    <img className="h-full w-full object-contain" alt="" src={unitImageSrc} draggable={false} />
                  ) : (
                    <FaUsers className="text-slate-400" aria-hidden="true" />
                  )}
                </div>

                {/* The map's own health colours, so a bar here reads the same as the one over the Force on the tile. */}
                <div className="mt-1 flex h-1.5 w-9 overflow-hidden rounded-sm bg-slate-200" title={healthTitle} aria-hidden="true">
                  <div className="h-full bg-green-500" style={{ width: `${healthyPercent}%` }} />
                  <div className="h-full bg-yellow-400" style={{ width: `${100 - healthyPercent}%` }} />
                </div>

                {/* Every count reads the same, including the atom carrying the remainder: the figure itself says it is
                    larger, and its tooltip says why. */}
                <span className="mt-0.5 text-[10px] text-slate-500">{atom.size}</span>
              </div>
            </Fragment>
          );
        })}
      </div>
    </div>
  );
}
