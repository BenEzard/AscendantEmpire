// The in-memory model behind the Split Force dialog: how a Force divides into smaller Forces, and nothing else.
//
// Deliberately pure — no React, no API, no rendering. The dialog holds one composition in state and only posts
// anything when the player confirms, so every function here returns a new composition rather than mutating one.
//
// The rules come from docs/design/units/military-organisation.md, "Splitting and merging":
//   * An unled Force is sized in whole multiples of its organisation tag's base quantum.
//   * Every resulting group is at least one quantum, all but one are exact multiples of it, and one group
//     carries whatever is left over (a lone part-quantum group never exists).
//
// Those rules hold for *any* composition this model can produce, which is what lets the dialog offer cutting,
// rejoining and dragging without validating after each: a group is a non-empty set of atoms, so it is always at
// least one quantum; the leftover lives on a single atom, so exactly one group is ever ragged.

/**
 * One indivisible piece of the Force being split — the smallest thing a cut can sit beside or a drag can carry.
 *
 * For an unled Force this is the organisation tag's base quantum: a 200-strong settler party with a base size
 * of 25 has eight atoms. Casualties leave a ragged tail, which rides on a single atom rather than becoming an
 * atom of its own — see {@link buildSplitAtoms}.
 */
export interface SplitAtom {
  /** Position in the Force's atom row, 0-based and stable for the life of the dialog. */
  index: number;

  /** How many soldiers this atom stands for — the base quantum, plus the remainder on the one atom carrying it. */
  size: number;

  /** True for the single atom carrying the leftover that doesn't fill a whole quantum. At most one atom has this. */
  carriesRemainder: boolean;
}

/**
 * One of the Forces the split would produce, as the player has composed it so far.
 *
 * It holds atoms rather than a range, because a dragged quantum can join a group that doesn't adjoin it — after
 * dragging the last quantum onto the first group, the first group holds atoms 0 and 7 and nothing in between.
 */
export interface SplitGroupState {
  /** Stable for as long as the group exists, so a box the player is pointing at doesn't change under them. */
  id: string;

  /** The atoms this group holds, ascending. Never empty once a split has begun. */
  atomIndexes: readonly number[];
}

/**
 * The whole division as it currently stands: an ordered row of groups partitioning every atom.
 *
 * A flat row rather than a tree of cuts. A tree records *how* the player arrived at a division, which is only
 * meaningful while a group is a contiguous run — and dragging a quantum between groups breaks that. The row
 * records the division itself, which is all the request carries anyway.
 */
export interface SplitComposition {
  /** Left to right, as the dialog lays them out. */
  groups: readonly SplitGroupState[];

  /** Hands out the next group id. Carried in the composition so the functions here stay pure. */
  nextGroupNumber: number;
}

/** One of the Forces a confirmed split would produce, with its share of the original's casualties. */
export interface SplitGroup {
  /** The group this came from, so the renderer can tie a resolved group back to the box that shows it. */
  groupId: string;

  atomIndexes: readonly number[];

  /** Total soldiers in the group — healthy and injured together. */
  size: number;

  healthyCount: number;

  injuredCount: number;
}

/**
 * The smallest number of pieces a split can produce.
 *
 * Mirrors `UnledSplitRules.MINIMUM_DIVISIONS` on the server, which is what actually enforces it — both the
 * toolbar's availability rule and the split endpoint read that one constant. This copy exists so the dialog can
 * say "there is nothing to divide" without a round trip.
 */
export const MINIMUM_SPLIT_DIVISIONS = 2;

/**
 * Breaks a Force's current strength into the atoms a split can be made along.
 *
 * The remainder rules matter here. A 190-strong party with a base of 25 yields `floor(190 / 25) = 7` atoms,
 * not 8: the leftover 15 rides on the last atom (making it worth 40) rather than becoming a part-quantum atom
 * of its own. That is what caps the split at seven ways and stops a lone 15 ever existing.
 *
 * @param totalStrength The Force's current size — healthy and injured together, matching what the backend's
 *   split rule measures.
 * @param baseSize The organisation tag's base quantum. A non-positive value is a catalogue configuration
 *   error, and yields no atoms rather than a divide-by-zero.
 */
export function buildSplitAtoms(totalStrength: number, baseSize: number): SplitAtom[] {
  if (baseSize <= 0 || totalStrength < baseSize) {
    return [];
  }

  const atomCount = Math.floor(totalStrength / baseSize);
  const remainder = totalStrength - atomCount * baseSize;

  return Array.from({ length: atomCount }, (_unused, index) => {
    const isLastAtom = index === atomCount - 1;

    return {
      index,
      // The remainder starts on the last atom. Wherever that atom ends up — cut away or dragged across the row —
      // its group is the one carrying the ragged part, and the player chooses which group that is.
      size: isLastAtom ? baseSize + remainder : baseSize,
      carriesRemainder: isLastAtom && remainder > 0,
    };
  });
}

/** The undivided force: one group holding every atom, ready for its first cut. */
export function createSplitComposition(atomCount: number): SplitComposition {
  return {
    groups: [{ id: buildGroupId(1), atomIndexes: Array.from({ length: atomCount }, (_unused, index) => index) }],
    nextGroupNumber: 2,
  };
}

/**
 * Cuts one group in two, immediately before `cutAtomIndex`.
 *
 * The atoms positioned before the cut stay with the group; that atom and everything after it become a new group
 * sitting directly to its right, so the row still reads in the order the force is laid out.
 *
 * Returns the identical composition when the cut isn't legal — the group isn't there, it doesn't hold that atom,
 * or the atom is the group's first, where a cut would leave an empty half. Callers can fire optimistically and
 * compare references.
 */
export function cutGroup(composition: SplitComposition, groupId: string, cutAtomIndex: number): SplitComposition {
  const groupPosition = composition.groups.findIndex((group) => group.id === groupId);

  if (groupPosition < 0) {
    return composition;
  }

  const group = composition.groups[groupPosition];
  const atomPosition = group.atomIndexes.indexOf(cutAtomIndex);

  // Position 0 is the group's own first atom: cutting there carves off nothing. A negative means the atom
  // belongs to some other group, which is a cut in a box the player isn't pointing at.
  if (atomPosition <= 0) {
    return composition;
  }

  const keptAtoms = group.atomIndexes.slice(0, atomPosition);
  const carvedAtoms = group.atomIndexes.slice(atomPosition);

  return {
    groups: [
      ...composition.groups.slice(0, groupPosition),
      { ...group, atomIndexes: keptAtoms },
      { id: buildGroupId(composition.nextGroupNumber), atomIndexes: carvedAtoms },
      ...composition.groups.slice(groupPosition + 1),
    ],
    nextGroupNumber: composition.nextGroupNumber + 1,
  };
}

/**
 * Rejoins two groups into one, holding every atom both of them held.
 *
 * The survivor keeps the earlier group's id and its place in the row, so rejoining the pair either side of a gap
 * leaves the row reading as it did before the cut. Only the two groups named are touched: a merge is not an undo,
 * and nothing cut elsewhere comes back with it.
 *
 * Returns the identical composition when either group is missing, or when both names are the same group.
 */
export function mergeGroups(composition: SplitComposition, groupId: string, otherGroupId: string): SplitComposition {
  if (groupId === otherGroupId) {
    return composition;
  }

  const firstPosition = composition.groups.findIndex((group) => group.id === groupId);
  const secondPosition = composition.groups.findIndex((group) => group.id === otherGroupId);

  if (firstPosition < 0 || secondPosition < 0) {
    return composition;
  }

  const survivorPosition = Math.min(firstPosition, secondPosition);
  const absorbedPosition = Math.max(firstPosition, secondPosition);

  const survivor = composition.groups[survivorPosition];
  const absorbed = composition.groups[absorbedPosition];

  const mergedGroup: SplitGroupState = {
    ...survivor,
    atomIndexes: sortAtoms([...survivor.atomIndexes, ...absorbed.atomIndexes]),
  };

  return {
    ...composition,
    groups: composition.groups
      .map((group, position) => (position === survivorPosition ? mergedGroup : group))
      .filter((_unused, position) => position !== absorbedPosition),
  };
}

/**
 * Moves one atom out of the group holding it and into another group.
 *
 * This is the drag, and it is the only operation that can rebalance groups without taking the force apart:
 * 25 + 75 + 100 becomes 50 + 75 + 75 by moving a single quantum, which no sequence of cuts and rejoins reaches.
 *
 * Returns the identical composition when the target isn't there, when the atom is already in the target, or when
 * the atom is the last one its group holds — a group of nothing is not a Force, and emptying one is what
 * rejoining two groups is for.
 */
export function moveAtom(composition: SplitComposition, atomIndex: number, targetGroupId: string): SplitComposition {
  const targetPosition = composition.groups.findIndex((group) => group.id === targetGroupId);
  const sourcePosition = composition.groups.findIndex((group) => group.atomIndexes.includes(atomIndex));

  if (targetPosition < 0 || sourcePosition < 0 || targetPosition === sourcePosition) {
    return composition;
  }

  if (composition.groups[sourcePosition].atomIndexes.length <= 1) {
    return composition;
  }

  return {
    ...composition,
    groups: composition.groups.map((group, position) => {
      if (position === sourcePosition) {
        return { ...group, atomIndexes: group.atomIndexes.filter((index) => index !== atomIndex) };
      }

      if (position === targetPosition) {
        return { ...group, atomIndexes: sortAtoms([...group.atomIndexes, atomIndex]) };
      }

      return group;
    }),
  };
}

/** Whether the force has been divided at all — a single group isn't a split, whatever has been dragged about. */
export function hasAnyCut(composition: SplitComposition): boolean {
  return composition.groups.length > 1;
}

/** How many soldiers a set of atoms holds. */
export function getAtomsSize(atomIndexes: readonly number[], atoms: readonly SplitAtom[]): number {
  return atomIndexes.reduce((size, atomIndex) => size + (atoms[atomIndex]?.size ?? 0), 0);
}

/**
 * Turns the composition into the Forces a confirmed split would produce, apportioning the original's injured
 * across them in proportion to size.
 *
 * Proportional is the neutral choice: nothing in the design says the wounded concentrate anywhere, and it needs
 * no extra decision from the player. Rounding is settled by largest remainder so the parts sum back to exactly
 * the original injured count — no soldier is invented or lost by the split.
 *
 * This is a *preview*: the server apportions the wounded itself, by the same algorithm, so what the player
 * confirms here is what they get. The request carries sizes only.
 */
export function buildSplitGroups(composition: SplitComposition, atoms: readonly SplitAtom[], totalInjuredCount: number): SplitGroup[] {
  const sizes = composition.groups.map((group) => getAtomsSize(group.atomIndexes, atoms));
  const injuredCounts = apportionByLargestRemainder(sizes, totalInjuredCount);

  return composition.groups.map((group, position) => ({
    groupId: group.id,
    atomIndexes: group.atomIndexes,
    size: sizes[position],
    healthyCount: sizes[position] - injuredCounts[position],
    injuredCount: injuredCounts[position],
  }));
}

/**
 * Divides `total` between the given weights so the parts are whole numbers that sum back to `total` exactly.
 *
 * Each part gets its exact share rounded down, and the units left over by that rounding go to the parts with
 * the largest discarded fractions — the standard largest-remainder apportionment. Ties fall to the earlier
 * part, so the result is deterministic and doesn't shuffle as the player cuts.
 */
export function apportionByLargestRemainder(weights: readonly number[], total: number): number[] {
  const weightSum = weights.reduce((runningTotal, weight) => runningTotal + weight, 0);

  if (weights.length === 0 || weightSum <= 0 || total <= 0) {
    return weights.map(() => 0);
  }

  const exactShares = weights.map((weight) => (total * weight) / weightSum);
  const parts = exactShares.map((share) => Math.floor(share));

  // What rounding down threw away, handed out one unit at a time to whoever lost the most to it.
  let unitsToDistribute = total - parts.reduce((runningTotal, part) => runningTotal + part, 0);

  const byDiscardedFraction = exactShares
    .map((share, index) => ({ index, fraction: share - Math.floor(share) }))
    .sort((left, right) => right.fraction - left.fraction || left.index - right.index);

  for (const candidate of byDiscardedFraction) {
    if (unitsToDistribute <= 0) {
      break;
    }

    parts[candidate.index]++;
    unitsToDistribute--;
  }

  return parts;
}

function buildGroupId(groupNumber: number): string {
  return `group-${groupNumber}`;
}

/** Atoms are held in row order within a group, so a dragged quantum doesn't land wherever the drop happened to put it. */
function sortAtoms(atomIndexes: readonly number[]): number[] {
  return [...atomIndexes].sort((left, right) => left - right);
}
