import { useEffect, useState, type DragEvent, type KeyboardEvent, type ReactNode } from "react";
import { Button, Modal } from "@/components/ui";
import AssetIconImage from "@/components/assets/AssetIconImage";
import CapacityBar from "@/components/ui/CapacityBar";
import type { Color } from "@/components/ui/colorpicker/colorTypes";
import Tooltip from "@/components/ui/Tooltip";
import ForceHealthBar from "@/features/map/components/ForceHealthBar";
import { formatMovementPoints } from "../util/formatMovementPoints";
import {
  buildPlan,
  canPickUp,
  createCargoState,
  hasChanges,
  moveCargo,
  receivableAt,
  reduceCargo,
  spendsTurn,
  usedCapacity,
  type AssetStack,
  type CargoForceState,
  type CargoLocation,
  type CargoState,
  type TakeResourcesForce,
  type TakeResourcesPlan,
  type TileAssetStack,
} from "./takeResourcesCargo";

const ASSET_ICON_SIZE = 32;
// Every tile, the empty drop slot included, takes the same box whether it holds a label or an input.
const ASSET_TILE_SIZE_CLASS = "h-16 w-16 shrink-0";
const FORCE_IMAGE_SIZE = 48;
const CARGO_FILL_COLOR: Color = { value: "#0284c7", description: "Carried" };

export interface TakeResourcesDialogProps {
  show: boolean;

  /** Every Asset the tile offers, with the player's own figure for each. */
  tileAssets: readonly TileAssetStack[];

  /** The selected Forces on the tile. */
  forces: readonly TakeResourcesForce[];

  onClose: () => void;

  onConfirm: (plan: TakeResourcesPlan) => void;
}

interface DragSource {
  from: CargoLocation;
  assetId: number;
}

/**
 * Moves Assets between the tile and the selected Forces, and between the Forces themselves.
 *
 * Nothing costs anything until Confirm: the plan says which Forces finish holding more than they started with, and those spend their turn.
 */
export default function TakeResourcesDialog({ show, tileAssets, forces, onClose, onConfirm }: TakeResourcesDialogProps) {
  const [cargo, setCargo] = useState<CargoState>(() => createCargoState(tileAssets, forces));
  const [dragSource, setDragSource] = useState<DragSource | null>(null);

  useEffect(() => {
    if (show) {
      setCargo(createCargoState(tileAssets, forces));
      setDragSource(null);
    }
  }, [show, tileAssets, forces]);

  const startDrag = (event: DragEvent, from: CargoLocation, assetId: number) => {
    // Firefox starts no drag without data, though the source itself is held in state.
    event.dataTransfer?.setData("text/plain", String(assetId));

    if (event.dataTransfer != null) {
      event.dataTransfer.effectAllowed = "move";
    }

    setDragSource({ from, assetId });
  };

  const canDropAt = (to: CargoLocation) => dragSource != null && receivableAt(cargo, forces, to, dragSource.assetId) > 0;

  const allowDropAt = (event: DragEvent, to: CargoLocation) => {
    if (canDropAt(to)) {
      event.preventDefault();
    }
  };

  const dropAt = (event: DragEvent, to: CargoLocation) => {
    event.preventDefault();

    if (dragSource != null) {
      setCargo((current) => moveCargo(current, forces, dragSource.from, to, dragSource.assetId));
    }

    setDragSource(null);
  };

  const isChanged = hasChanges(cargo, forces);

  return (
    <Modal
      open={show}
      title="Take / Distribute / Drop Resources"
      onClose={onClose}
      size="xl"
      panelClassName="thick-border-modal"
      footer={
        <div className="flex w-full gap-2">
          <Button
            variant="outline-secondary"
            className="mr-auto"
            onClick={() => setCargo(createCargoState(tileAssets, forces))}
            disabled={!isChanged}>
            Reset
          </Button>
          <Button variant="outline-secondary" onClick={onClose}>
            Cancel
          </Button>
          <Button variant="primary" onClick={() => onConfirm(buildPlan(cargo, forces))} disabled={!isChanged}>
            Confirm
          </Button>
        </div>
      }>
      <p className="mb-2 text-sm text-slate-600">
        Drag resources between sections to redistribute. Adjust quantities by editing the number. Forces taking resources will use their remaining
        movement points.
      </p>

      <div className="flex h-[26rem] rounded-md border border-slate-200">
        <AvailablePanel
          available={cargo.available}
          isDropActive={dragSource?.from.kind === "force"}
          onDragStart={(event, assetId) => startDrag(event, { kind: "available" }, assetId)}
          onDragEnd={() => setDragSource(null)}
          onDragOver={(event) => allowDropAt(event, { kind: "available" })}
          onDrop={(event) => dropAt(event, { kind: "available" })}
        />

        <div className="flex min-w-0 flex-1 gap-2 overflow-x-auto p-2" aria-label="Selected Forces">
          {forces.map((force) => {
            const location: CargoLocation = { kind: "force", forceId: force.forceId };
            const current = cargo.forces.find((candidate) => candidate.forceId === force.forceId) ?? { forceId: force.forceId, carrying: [] };

            return (
              <ForcePanel
                key={force.forceId}
                force={force}
                current={current}
                onDragStart={(event, assetId) => startDrag(event, location, assetId)}
                onDragEnd={() => setDragSource(null)}
                onDragOver={(event) => allowDropAt(event, location)}
                onDrop={(event) => dropAt(event, location)}
                onReduce={(assetId, quantity) => setCargo((state) => reduceCargo(state, force.forceId, assetId, quantity))}
              />
            );
          })}
        </div>
      </div>
    </Modal>
  );
}

function formatQuantity(stack: TileAssetStack): string {
  return stack.isEstimate ? `~${stack.quantity}` : String(stack.quantity);
}

interface DropHandlers {
  onDragStart: (event: DragEvent, assetId: number) => void;
  onDragEnd: () => void;
  onDragOver: (event: DragEvent) => void;
  onDrop: (event: DragEvent) => void;
}

interface AvailablePanelProps extends DropHandlers {
  available: readonly TileAssetStack[];
  isDropActive: boolean;
}

function AvailablePanel({ available, isDropActive, onDragStart, onDragEnd, onDragOver, onDrop }: AvailablePanelProps) {
  return (
    <div
      role="group"
      aria-label="Available"
      className="flex min-h-0 w-1/5 shrink-0 flex-col border-r border-slate-200 p-2"
      onDragOver={onDragOver}
      onDrop={onDrop}>
      <div className="mb-2 text-center text-sm font-medium">Available</div>

      <div className="flex min-h-0 flex-1 flex-wrap content-start justify-center gap-1 overflow-y-auto">
        {available.length === 0 ? (
          <p className="text-center text-xs text-slate-400">Nothing left here.</p>
        ) : (
          available.map((stack) => (
            <AssetTile key={stack.assetId} stack={stack} onDragStart={(event) => onDragStart(event, stack.assetId)} onDragEnd={onDragEnd}>
              <span className="text-xs tabular-nums text-slate-600">{formatQuantity(stack)}</span>
            </AssetTile>
          ))
        )}
      </div>

      <div
        role="group"
        aria-label="Drop"
        data-testid="drop-zone"
        className={[
          "mt-2 rounded-md border-2 border-dashed py-3 text-center text-sm",
          isDropActive ? "border-sky-600 bg-sky-50 text-sky-700" : "border-slate-300 text-slate-500",
        ].join(" ")}>
        Drop
      </div>
    </div>
  );
}

interface AssetTileProps {
  stack: AssetStack;
  onDragStart: (event: DragEvent) => void;
  onDragEnd: () => void;
  children: ReactNode;
}

function AssetTile({ stack, onDragStart, onDragEnd, children }: AssetTileProps) {
  return (
    <div className={`${ASSET_TILE_SIZE_CLASS} flex flex-col items-center justify-between rounded-md border border-slate-300 bg-white p-1`}>
      <Tooltip content={stack.displayName}>
        <div draggable aria-label={stack.displayName} className="cursor-grab" onDragStart={onDragStart} onDragEnd={onDragEnd}>
          <AssetIconImage iconPath={stack.iconPath} size={ASSET_ICON_SIZE} />
        </div>
      </Tooltip>
      {children}
    </div>
  );
}

interface ForcePanelProps extends DropHandlers {
  force: TakeResourcesForce;
  current: CargoForceState;
  onReduce: (assetId: number, quantity: number) => void;
}

function ForcePanel({ force, current, onDragStart, onDragEnd, onDragOver, onDrop, onReduce }: ForcePanelProps) {
  const used = usedCapacity(current.carrying);
  const free = Math.max(0, force.capacity - used);
  const hasNoTurn = force.turnRemaining <= 0;
  const willSpendTurn = spendsTurn(force, current);

  const segments = current.carrying.map((stack) => ({ unitWidth: stack.quantity, tooltip: `${stack.displayName}: ${stack.quantity}` }));

  return (
    <div
      role="group"
      aria-label={force.displayName}
      className={[
        "flex w-52 shrink-0 flex-col items-center overflow-y-auto rounded-md border-2 p-2",
        hasNoTurn ? "border-slate-300 opacity-60 grayscale" : "border-sky-700",
      ].join(" ")}
      onDragOver={onDragOver}
      onDrop={onDrop}>
      <div className="flex w-full items-center gap-2">
        <div className="flex shrink-0 flex-col items-center" style={{ width: FORCE_IMAGE_SIZE }}>
          <ForceHealthBar healthy={force.healthyIndividuals} injured={force.injuredIndividuals} initialStrength={force.initialStrength} />

          <Tooltip content={force.displayName}>
            {force.imageSrc != null ? (
              // Inline size and object-contain, since the preflight's `height: auto` lets each image's own proportions win over the attributes.
              <img
                src={force.imageSrc}
                alt={force.displayName}
                className="shrink-0 object-contain"
                style={{ width: FORCE_IMAGE_SIZE, height: FORCE_IMAGE_SIZE }}
                draggable={false}
              />
            ) : (
              <span className="flex items-center justify-center text-xs" style={{ width: FORCE_IMAGE_SIZE, height: FORCE_IMAGE_SIZE }}>
                {force.displayName}
              </span>
            )}
          </Tooltip>
        </div>

        <div className="min-w-0 text-xs leading-snug tabular-nums text-slate-700">
          <div>
            Troops: {force.healthyIndividuals} / {force.injuredIndividuals}
          </div>
          <div>
            Movement: {formatMovementPoints(force.movementRemaining)} / {formatMovementPoints(force.movementPoints)}
          </div>
        </div>
      </div>

      <div className="mt-2 w-full">
        <CapacityBar segments={segments} defaultFillColor={CARGO_FILL_COLOR} totalUnits={force.capacity} remainderTooltip={`${free} free`} />
        <div className="mt-0.5 text-center text-xs tabular-nums text-slate-600">
          {used} / {force.capacity}
        </div>
      </div>

      {willSpendTurn ? <div className="text-xs font-medium text-amber-700">Uses its turn</div> : null}

      {/* A fixed two-column grid, so the drop slot lines up under the tiles rather than centring on a row of its own. */}
      <div className="mt-2 grid grid-cols-[repeat(2,4rem)] justify-center gap-1">
        {current.carrying.map((stack) => (
          <AssetTile key={stack.assetId} stack={stack} onDragStart={(event) => onDragStart(event, stack.assetId)} onDragEnd={onDragEnd}>
            <CargoQuantityField stack={stack} forceName={force.displayName} onReduce={(quantity) => onReduce(stack.assetId, quantity)} />
          </AssetTile>
        ))}

        {hasNoTurn ? null : canPickUp(force) && free > 0 ? (
          <div
            aria-hidden
            data-testid="cargo-placeholder"
            className={`${ASSET_TILE_SIZE_CLASS} rounded-md border-2 border-dashed border-slate-300`}
          />
        ) : null}
      </div>

      {hasNoTurn ? (
        <p className="mt-2 text-center text-xs text-slate-500">No turns remaining</p>
      ) : !canPickUp(force) ? (
        <p className="mt-2 text-center text-xs text-slate-500">Needs a full turn to pick up</p>
      ) : null}
    </div>
  );
}

interface CargoQuantityFieldProps {
  stack: AssetStack;
  forceName: string;
  onReduce: (quantity: number) => void;
}

/** Commits on blur or Enter, since a down-only figure cannot be typed digit by digit. */
function CargoQuantityField({ stack, forceName, onReduce }: CargoQuantityFieldProps) {
  const [draft, setDraft] = useState(String(stack.quantity));

  useEffect(() => {
    setDraft(String(stack.quantity));
  }, [stack.quantity]);

  const commit = () => {
    const parsed = Number(draft);

    if (draft.trim() === "" || Number.isNaN(parsed) || parsed >= stack.quantity) {
      setDraft(String(stack.quantity));
      return;
    }

    onReduce(parsed);
  };

  const handleKeyDown = (event: KeyboardEvent<HTMLInputElement>) => {
    if (event.key === "Enter") {
      commit();
    } else if (event.key === "Escape") {
      setDraft(String(stack.quantity));
    }
  };

  return (
    <input
      type="number"
      min={0}
      max={stack.quantity}
      step={1}
      aria-label={`${stack.displayName} carried by ${forceName}`}
      className="w-full rounded border border-slate-300 px-1 text-center text-xs tabular-nums [appearance:textfield] [&::-webkit-inner-spin-button]:appearance-none [&::-webkit-outer-spin-button]:appearance-none"
      value={draft}
      onChange={(event) => setDraft(event.target.value)}
      onBlur={commit}
      onKeyDown={handleKeyDown}
    />
  );
}
