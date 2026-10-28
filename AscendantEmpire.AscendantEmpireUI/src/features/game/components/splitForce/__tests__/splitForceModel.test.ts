import {
  apportionByLargestRemainder,
  buildSplitAtoms,
  buildSplitGroups,
  createSplitComposition,
  cutGroup,
  hasAnyCut,
  mergeGroups,
  moveAtom,
  type SplitComposition,
} from "../splitForceModel";

/** The authored Settler tag's base quantum, used throughout so the numbers match the design doc's worked examples. */
const SETTLER_BASE_SIZE = 25;

/** The atoms a group holds, by group position — the shape most assertions here care about. */
function atomLayout(composition: SplitComposition): number[][] {
  return composition.groups.map((group) => [...group.atomIndexes]);
}

/** The sizes a confirmed split would produce, which is the only thing the dialog actually posts. */
function groupSizes(composition: SplitComposition, totalStrength: number): number[] {
  const atoms = buildSplitAtoms(totalStrength, SETTLER_BASE_SIZE);

  return buildSplitGroups(composition, atoms, 0).map((group) => group.size);
}

describe("buildSplitAtoms", () => {
  it("divides an exact multiple into equal atoms", () => {
    const atoms = buildSplitAtoms(200, SETTLER_BASE_SIZE);

    expect(atoms).toHaveLength(8);
    expect(atoms.map((atom) => atom.size)).toEqual([25, 25, 25, 25, 25, 25, 25, 25]);
    expect(atoms.some((atom) => atom.carriesRemainder)).toBe(false);
  });

  /**
   * The design's invariant: all but one group are exact multiples of the base and one carries the remainder, so
   * a bloodied 190-strong party is seven atoms with a fat one on the end — never eight with a stray 15.
   */
  it("rides the remainder on the last atom rather than making an atom of it", () => {
    const atoms = buildSplitAtoms(190, SETTLER_BASE_SIZE);

    expect(atoms).toHaveLength(7);
    expect(atoms.map((atom) => atom.size)).toEqual([25, 25, 25, 25, 25, 25, 40]);
    expect(atoms.filter((atom) => atom.carriesRemainder).map((atom) => atom.index)).toEqual([6]);
  });

  it("yields no atoms for a force smaller than one base group", () => {
    expect(buildSplitAtoms(20, SETTLER_BASE_SIZE)).toEqual([]);
  });

  it("yields no atoms when the organisation has no base size, rather than dividing by zero", () => {
    expect(buildSplitAtoms(200, 0)).toEqual([]);
  });
});

describe("createSplitComposition", () => {
  it("starts with the whole force in one group, which is not yet a split", () => {
    const composition = createSplitComposition(8);

    expect(atomLayout(composition)).toEqual([[0, 1, 2, 3, 4, 5, 6, 7]]);
    expect(hasAnyCut(composition)).toBe(false);
  });
});

describe("cutGroup", () => {
  /** The worked example from the request: 200 in eights of 25, cut into 25 + 75 + 100. */
  it("produces an uneven combination from two cuts", () => {
    let composition = createSplitComposition(8);

    composition = cutGroup(composition, composition.groups[0].id, 1); // 25 | 175
    composition = cutGroup(composition, composition.groups[1].id, 4); // 75 | 100

    expect(groupSizes(composition, 200)).toEqual([25, 75, 100]);
    expect(atomLayout(composition)).toEqual([[0], [1, 2, 3], [4, 5, 6, 7]]);
  });

  /** The new group goes where it falls, not on the end, so the row reads in the order the force is laid out. */
  it("inserts the half it carved off directly after the group it came from", () => {
    let composition = createSplitComposition(4);

    composition = cutGroup(composition, composition.groups[0].id, 2); // [0,1] | [2,3]
    composition = cutGroup(composition, composition.groups[0].id, 1); // [0] | [1] | [2,3]

    expect(atomLayout(composition)).toEqual([[0], [1], [2, 3]]);
  });

  it("refuses a cut at a group's first atom, which would leave an empty half", () => {
    const composition = createSplitComposition(4);

    expect(cutGroup(composition, composition.groups[0].id, 0)).toBe(composition);
  });

  it("refuses a cut at an atom the group does not hold", () => {
    let composition = createSplitComposition(4);
    composition = cutGroup(composition, composition.groups[0].id, 2);

    // Atom 3 belongs to the second group, so the first group cannot be cut at it.
    expect(cutGroup(composition, composition.groups[0].id, 3)).toBe(composition);
  });

  it("leaves the composition untouched when the group isn't in it", () => {
    const composition = createSplitComposition(4);

    expect(cutGroup(composition, "group-nowhere", 2)).toBe(composition);
  });

  /** Ids have to outlive a cut elsewhere, or the box the player is pointing at changes under them. */
  it("keeps the cut group's id and gives the new half an id of its own", () => {
    const composition = createSplitComposition(4);
    const originalId = composition.groups[0].id;

    const cut = cutGroup(composition, originalId, 2);

    expect(cut.groups[0].id).toBe(originalId);
    expect(cut.groups[1].id).not.toBe(originalId);
  });
});

describe("mergeGroups", () => {
  it("rejoins two groups into one holding both their atoms", () => {
    let composition = createSplitComposition(8);

    composition = cutGroup(composition, composition.groups[0].id, 1); // 25 | 175
    composition = mergeGroups(composition, composition.groups[0].id, composition.groups[1].id);

    expect(atomLayout(composition)).toEqual([[0, 1, 2, 3, 4, 5, 6, 7]]);
    expect(hasAnyCut(composition)).toBe(false);
  });

  /** Only the two groups named are rejoined — a third cut elsewhere in the row is not undone by a merge beside it. */
  it("leaves the groups it was not asked about alone", () => {
    let composition = createSplitComposition(8);

    composition = cutGroup(composition, composition.groups[0].id, 1); // [0] | [1..7]
    composition = cutGroup(composition, composition.groups[1].id, 4); // [0] | [1,2,3] | [4..7]
    composition = mergeGroups(composition, composition.groups[0].id, composition.groups[1].id);

    expect(atomLayout(composition)).toEqual([
      [0, 1, 2, 3],
      [4, 5, 6, 7],
    ]);
  });

  it("refuses to merge a group with itself", () => {
    const composition = createSplitComposition(4);

    expect(mergeGroups(composition, composition.groups[0].id, composition.groups[0].id)).toBe(composition);
  });
});

describe("moveAtom", () => {
  /**
   * The rebalance the drag exists for: 25 + 75 + 100 becomes 50 + 75 + 75 by moving one quantum, which no
   * sequence of cuts and rejoins can reach without taking the whole force apart first.
   */
  it("moves a quantum from one group to another, rebalancing both", () => {
    let composition = createSplitComposition(8);

    composition = cutGroup(composition, composition.groups[0].id, 1); // 25 | 175
    composition = cutGroup(composition, composition.groups[1].id, 4); // 25 | 75 | 100

    composition = moveAtom(composition, 7, composition.groups[0].id);

    expect(groupSizes(composition, 200)).toEqual([50, 75, 75]);
    expect(atomLayout(composition)).toEqual([
      [0, 7],
      [1, 2, 3],
      [4, 5, 6],
    ]);
  });

  /** A group of nothing is not a Force. Emptying one is what the plus between two boxes is for. */
  it("refuses to move a group's last atom, which would empty it", () => {
    let composition = createSplitComposition(8);
    composition = cutGroup(composition, composition.groups[0].id, 1); // [0] | [1..7]

    expect(moveAtom(composition, 0, composition.groups[1].id)).toBe(composition);
  });

  it("refuses to move an atom into the group that already holds it", () => {
    const composition = createSplitComposition(4);

    expect(moveAtom(composition, 2, composition.groups[0].id)).toBe(composition);
  });

  it("leaves the composition untouched when the target group isn't in it", () => {
    let composition = createSplitComposition(4);
    composition = cutGroup(composition, composition.groups[0].id, 2);

    expect(moveAtom(composition, 0, "group-nowhere")).toBe(composition);
  });

  /** The remainder atom is a quantum and a bit, so wherever it is dragged, that group is the ragged one. */
  it("carries the remainder with the atom holding it", () => {
    const atoms = buildSplitAtoms(190, SETTLER_BASE_SIZE);

    let composition = createSplitComposition(atoms.length);
    composition = cutGroup(composition, composition.groups[0].id, 3); // 75 | 115 (the fat atom is in the second)

    composition = moveAtom(composition, 6, composition.groups[0].id);

    expect(buildSplitGroups(composition, atoms, 0).map((group) => group.size)).toEqual([115, 75]);
  });
});

describe("buildSplitGroups", () => {
  /** 20 wounded over 25 + 75 + 100 is 2.5 / 7.5 / 10 — the halves have to land somewhere, and nowhere twice. */
  it("apportions the injured across the groups without changing the total", () => {
    const atoms = buildSplitAtoms(200, SETTLER_BASE_SIZE);

    let composition = createSplitComposition(atoms.length);
    composition = cutGroup(composition, composition.groups[0].id, 1);
    composition = cutGroup(composition, composition.groups[1].id, 4);

    const groups = buildSplitGroups(composition, atoms, 20);

    expect(groups.map((group) => group.injuredCount)).toEqual([3, 7, 10]);
    expect(groups.map((group) => group.healthyCount)).toEqual([22, 68, 90]);
    expect(groups.reduce((total, group) => total + group.injuredCount, 0)).toBe(20);
  });

  it("reports the whole force as one group before any cut is made", () => {
    const atoms = buildSplitAtoms(200, SETTLER_BASE_SIZE);

    const groups = buildSplitGroups(createSplitComposition(atoms.length), atoms, 40);

    expect(groups).toHaveLength(1);
    expect(groups[0].size).toBe(200);
    expect(groups[0].injuredCount).toBe(40);
  });
});

describe("apportionByLargestRemainder", () => {
  it("hands the rounding leftovers to the largest discarded fractions", () => {
    expect(apportionByLargestRemainder([1, 1, 1], 10)).toEqual([4, 3, 3]);
  });

  it("gives nothing away when there is nothing to apportion", () => {
    expect(apportionByLargestRemainder([25, 75], 0)).toEqual([0, 0]);
  });
});
