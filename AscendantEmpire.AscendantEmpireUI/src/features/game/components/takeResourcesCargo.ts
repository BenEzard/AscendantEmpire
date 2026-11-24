// The Take Resources dialog's working state. Moves are free inside the dialog; the turn cost comes from where the plan ends up, so a Force that finishes
// holding more of an Asset than it started with has picked up, and dragging something away and back again costs nothing.

/** A quantity of one Asset, in whole units. */
export interface AssetStack {
  assetId: number;
  displayName: string;
  iconPath: string;
  quantity: number;
}

/** An Asset on the tile. An estimate is the player's own fuzzed figure, never the true quantity. */
export interface TileAssetStack extends AssetStack {
  isEstimate: boolean;
}

/** One selected Force on the tile, as the dialog opens on it. */
export interface TakeResourcesForce {
  forceId: number;
  displayName: string;
  imageSrc: string | undefined;

  healthyIndividuals: number;
  injuredIndividuals: number;
  initialStrength: number;

  movementRemaining: number;
  movementPoints: number;

  /** The share of this round's campaign turn still unspent: 1 is a whole turn, 0 is none. */
  turnRemaining: number;

  /** How many units it can carry in all. Every Asset weighs one unit until Assets carry weights. */
  capacity: number;

  carrying: AssetStack[];
}

export interface CargoForceState {
  forceId: number;
  carrying: AssetStack[];
}

export interface CargoState {
  available: TileAssetStack[];
  forces: CargoForceState[];
}

export type CargoLocation = { kind: "available" } | { kind: "force"; forceId: number };

/** What the dialog hands back: where everything ends up, and which Forces that costs a turn. */
export interface TakeResourcesPlan {
  available: TileAssetStack[];
  forces: (CargoForceState & { spendsTurn: boolean })[];
}

export function createCargoState(tileAssets: readonly TileAssetStack[], forces: readonly TakeResourcesForce[]): CargoState {
  return {
    available: tileAssets.filter((stack) => stack.quantity > 0).map((stack) => ({ ...stack })),
    forces: forces.map((force) => ({ forceId: force.forceId, carrying: force.carrying.map((stack) => ({ ...stack })) })),
  };
}

export function usedCapacity(carrying: readonly AssetStack[]): number {
  return carrying.reduce((total, stack) => total + stack.quantity, 0);
}

/** Picking up needs the whole turn for now. A part turn will later let a Force pick up a share of its capacity. */
export function canPickUp(force: TakeResourcesForce): boolean {
  return force.turnRemaining >= 1;
}

function quantityOf(stacks: readonly AssetStack[], assetId: number): number {
  return stacks.find((stack) => stack.assetId === assetId)?.quantity ?? 0;
}

/** How much more of an Asset a Force could take on. Without its whole turn it can still take back what it started with, since that costs nothing. */
export function maxReceivable(force: TakeResourcesForce, current: CargoForceState, assetId: number): number {
  const free = Math.max(0, force.capacity - usedCapacity(current.carrying));

  if (canPickUp(force)) {
    return free;
  }

  const ownedBefore = quantityOf(force.carrying, assetId) - quantityOf(current.carrying, assetId);

  return Math.min(free, Math.max(0, ownedBefore));
}

export function spendsTurn(force: TakeResourcesForce, current: CargoForceState): boolean {
  return current.carrying.some((stack) => stack.quantity > quantityOf(force.carrying, stack.assetId));
}

function quantityAt(state: CargoState, location: CargoLocation, assetId: number): number {
  if (location.kind === "available") {
    return quantityOf(state.available, assetId);
  }

  return quantityOf(state.forces.find((force) => force.forceId === location.forceId)?.carrying ?? [], assetId);
}

function findStack(state: CargoState, location: CargoLocation, assetId: number): AssetStack | undefined {
  const stacks = location.kind === "available" ? state.available : state.forces.find((force) => force.forceId === location.forceId)?.carrying;

  return stacks?.find((stack) => stack.assetId === assetId);
}

/** Adds to a matching stack, or appends a new one. Stacks that reach zero drop out of the list. */
function adjust<T extends AssetStack>(stacks: readonly T[], template: AssetStack, delta: number, create: (stack: AssetStack) => T): T[] {
  const exists = stacks.some((stack) => stack.assetId === template.assetId);
  const adjusted = exists
    ? stacks.map((stack) => (stack.assetId === template.assetId ? { ...stack, quantity: stack.quantity + delta } : stack))
    : [...stacks, create({ ...template, quantity: delta })];

  return adjusted.filter((stack) => stack.quantity > 0);
}

/** Drops the tile's estimate flag, which means nothing once the Asset is in cargo. */
function toCargoStack({ assetId, displayName, iconPath, quantity }: AssetStack): AssetStack {
  return { assetId, displayName, iconPath, quantity };
}

function adjustAt(state: CargoState, location: CargoLocation, template: AssetStack, delta: number): CargoState {
  if (location.kind === "available") {
    // An Asset the tile did not list, such as cargo put down, is a counted figure rather than an estimate.
    return { ...state, available: adjust(state.available, template, delta, (stack) => ({ ...stack, isEstimate: false })) };
  }

  return {
    ...state,
    forces: state.forces.map((force) =>
      force.forceId === location.forceId ? { ...force, carrying: adjust(force.carrying, template, delta, toCargoStack) } : force,
    ),
  };
}

/** The most the destination can accept of this Asset, or zero when it cannot accept any. */
export function receivableAt(state: CargoState, forces: readonly TakeResourcesForce[], to: CargoLocation, assetId: number): number {
  if (to.kind === "available") {
    return Number.POSITIVE_INFINITY;
  }

  const force = forces.find((candidate) => candidate.forceId === to.forceId);
  const current = state.forces.find((candidate) => candidate.forceId === to.forceId);

  if (force == null || current == null) {
    return 0;
  }

  return maxReceivable(force, current, assetId);
}

function sameLocation(left: CargoLocation, right: CargoLocation): boolean {
  if (left.kind === "available" || right.kind === "available") {
    return left.kind === right.kind;
  }

  return left.forceId === right.forceId;
}

/** Moves as much of an Asset as the destination can take. Returns the same state when nothing moves. */
export function moveCargo(state: CargoState, forces: readonly TakeResourcesForce[], from: CargoLocation, to: CargoLocation, assetId: number): CargoState {
  if (sameLocation(from, to)) {
    return state;
  }

  const stack = findStack(state, from, assetId);

  if (stack == null) {
    return state;
  }

  const quantity = Math.min(stack.quantity, receivableAt(state, forces, to, assetId));

  if (quantity <= 0) {
    return state;
  }

  return adjustAt(adjustAt(state, from, stack, -quantity), to, stack, quantity);
}

/** Lowers what a Force carries of an Asset, putting the difference back on the tile. A higher figure is ignored. */
export function reduceCargo(state: CargoState, forceId: number, assetId: number, quantity: number): CargoState {
  const location: CargoLocation = { kind: "force", forceId };
  const current = quantityAt(state, location, assetId);
  const target = Math.max(0, Math.floor(quantity));

  if (Number.isNaN(target) || target >= current) {
    return state;
  }

  const stack = findStack(state, location, assetId)!;

  return adjustAt(adjustAt(state, location, stack, target - current), { kind: "available" }, stack, current - target);
}

export function buildPlan(state: CargoState, forces: readonly TakeResourcesForce[]): TakeResourcesPlan {
  return {
    available: state.available,
    forces: state.forces.map((current) => {
      const force = forces.find((candidate) => candidate.forceId === current.forceId);

      return { ...current, spendsTurn: force != null && spendsTurn(force, current) };
    }),
  };
}

/** Whether any Force ends up carrying something other than what it started with. */
export function hasChanges(state: CargoState, forces: readonly TakeResourcesForce[]): boolean {
  return state.forces.some((current) => {
    const force = forces.find((candidate) => candidate.forceId === current.forceId);

    if (force == null) {
      return false;
    }

    const assetIds = new Set([...force.carrying, ...current.carrying].map((stack) => stack.assetId));

    return [...assetIds].some((assetId) => quantityOf(force.carrying, assetId) !== quantityOf(current.carrying, assetId));
  });
}
