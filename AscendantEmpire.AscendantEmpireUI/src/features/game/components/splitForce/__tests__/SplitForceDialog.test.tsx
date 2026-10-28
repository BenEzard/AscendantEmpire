import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";

import SplitForceDialog from "../SplitForceDialog";
import type { SplitForceOptions, SplitForceResult } from "@/features/game/api/splitForceApi";

// The API is mocked rather than the hooks, so what is pinned is the request the dialog actually sends — the group sizes
// especially, since those are the whole parameterisation of a split and the server refuses anything that is not a legal one.
const { getSplitForceOptions, splitForce } = vi.hoisted(() => ({
  getSplitForceOptions: vi.fn(),
  splitForce: vi.fn(),
}));

vi.mock("@/features/game/api/splitForceApi", () => ({
  getSplitForceOptions,
  splitForce,
}));

/** The design doc's Settler: 200 strong, quantum 25, so eight atoms with no remainder. */
function options(overrides: Partial<SplitForceOptions> = {}): SplitForceOptions {
  return {
    unitInstanceId: 55,
    displayName: "Settler",
    leadershipModel: "Unled",
    healthyCount: 200,
    injuredCount: 0,
    totalStrength: 200,
    baseSize: 25,
    ...overrides,
  };
}

function result(overrides: Partial<SplitForceResult> = {}): SplitForceResult {
  return {
    xCoord: 4,
    yCoord: 1,
    groups: [
      { unitInstanceId: 55, size: 25, healthyCount: 25, injuredCount: 0, isOriginalForce: true },
      { unitInstanceId: 56, size: 175, healthyCount: 175, injuredCount: 0, isOriginalForce: false },
    ],
    ...overrides,
  };
}

function renderDialog(props: Partial<React.ComponentProps<typeof SplitForceDialog>> = {}) {
  // Retries off: a test that asserts on a refusal would otherwise wait for react-query to try again first.
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } });

  const onClose = vi.fn();
  const onSplit = vi.fn();

  render(
    <QueryClientProvider client={queryClient}>
      <SplitForceDialog show gameId={1} playerId={2} forceId={55} onClose={onClose} onSplit={onSplit} {...props} />
    </QueryClientProvider>,
  );

  return { onClose, onSplit };
}

/** Every cuttable gap in the dialog, left to right — one between each neighbouring pair of atoms. */
function cutButtons() {
  return screen.getAllByRole("button", { name: /^Cut before soldier group/ });
}

/** Every quantum on show, left to right. Each carries the unit's name as its tooltip, which is what identifies one. */
function atomIcons() {
  return screen.getAllByTitle("Settler");
}

/** The dialog has its options and has laid the force out. The first group's box is the one thing every loaded state shows. */
function waitForLayout() {
  return screen.findByRole("group", { name: /^Group 1 / });
}

/**
 * The strength of each box, left to right — the division as the dialog currently holds it.
 *
 * Read from the boxes' own labels, since that is where the dialog states a group's strength now that nothing prints the
 * composition as a line of text.
 */
function groupStrengths() {
  return screen
    .getAllByRole("group")
    .map((box) => Number((box.getAttribute("aria-label") ?? "").match(/— ([\d,]+) soldiers/)?.[1]?.replace(/,/g, "")));
}

describe("SplitForceDialog", () => {
  beforeEach(() => {
    getSplitForceOptions.mockReset();
    splitForce.mockReset();
    getSplitForceOptions.mockResolvedValue(options());
    splitForce.mockResolvedValue(result());
  });

  it("asks for nothing while closed", () => {
    renderDialog({ show: false, forceId: null });

    expect(getSplitForceOptions).not.toHaveBeenCalled();
  });

  /** The quantum and the strength both come from the server, so the title and the row are only right if the dialog reads them. */
  it("lays the force out in groups of its authored base quantum", async () => {
    renderDialog();

    await waitForLayout();

    expect(screen.getByText("Split Force: Settler (Unled) - 200")).toBeInTheDocument();

    // Eight atoms of 25, and a cuttable gap between every neighbouring pair.
    expect(atomIcons()).toHaveLength(8);
    expect(screen.getAllByText("25")).toHaveLength(8);
    expect(cutButtons()).toHaveLength(7);
  });

  /**
   * Confirming before any cut would post a single group, which is not a split at all — the server would refuse it, so the
   * dialog must not offer it.
   */
  it("will not split until a cut has been made", async () => {
    renderDialog();

    await waitForLayout();

    expect(screen.getByRole("button", { name: "Split" })).toBeDisabled();
  });

  /** The worked example from the design doc: one cut peels off a single quantum and leaves the rest whole. */
  it("posts the sizes the player composed", async () => {
    const user = userEvent.setup();
    const { onSplit } = renderDialog();

    await waitForLayout();

    // The first scissors sits before the second atom, so cutting there gives 25 | 175.
    await user.click(cutButtons()[0]);

    await user.click(screen.getByRole("button", { name: "Split into 2 forces" }));

    await waitFor(() => expect(splitForce).toHaveBeenCalledWith({ unitInstanceId: 55, groupSizes: [25, 175] }));
    await waitFor(() => expect(onSplit).toHaveBeenCalledOnce());
  });

  /**
   * The nesting is the point of the hierarchy: cutting inside a half divides that half alone, which is how any combination
   * of quanta is reachable.
   */
  it("divides a group further when a cut is made inside it", async () => {
    const user = userEvent.setup();
    renderDialog();

    await waitForLayout();

    // 25 | 175, then cut the 175 three atoms in: 25 | 75 | 100.
    await user.click(cutButtons()[0]);
    await user.click(cutButtons()[2]);

    await user.click(screen.getByRole("button", { name: "Split into 3 forces" }));

    await waitFor(() => expect(splitForce).toHaveBeenCalledWith({ unitInstanceId: 55, groupSizes: [25, 75, 100] }));
  });

  /** The plus between two boxes has to put them back together, or the player could not recover from a misplaced cut. */
  it("returns the force to one group when the two halves are rejoined", async () => {
    const user = userEvent.setup();
    renderDialog();

    await waitForLayout();

    await user.click(cutButtons()[0]);
    await user.click(screen.getByRole("button", { name: "Rejoin group 1 and group 2" }));

    expect(screen.getByRole("button", { name: "Split" })).toBeDisabled();
    expect(groupStrengths()).toEqual([200]);
  });

  /**
   * Dragging is the only way to rebalance groups without recutting the whole force, so what it has to pin is the composition
   * that comes out: a quantum leaves the group it was in and is counted in the one it was dropped on.
   */
  it("moves a quantum into the group it is dragged onto", async () => {
    const user = userEvent.setup();
    renderDialog();

    await waitForLayout();

    await user.click(cutButtons()[0]);

    expect(groupStrengths()).toEqual([25, 175]);

    // The last quantum of the 175, dragged onto the box holding the 25.
    const atomCells = atomIcons().map((icon) => icon.closest('[draggable="true"]'));
    const lastAtom = atomCells[atomCells.length - 1]!;
    const firstGroup = screen.getByRole("group", { name: /^Group 1 / });

    // jsdom implements no DataTransfer, so the drag carries a stand-in for the one the browser would supply.
    const dataTransfer = { setData: vi.fn(), effectAllowed: "" };

    fireEvent.dragStart(lastAtom, { dataTransfer });
    fireEvent.dragOver(firstGroup, { dataTransfer });
    fireEvent.drop(firstGroup, { dataTransfer });

    expect(groupStrengths()).toEqual([50, 150]);
  });

  /** A group of nothing is not a Force, so the one quantum holding a group together cannot be dragged out of it. */
  it("will not let a group's last quantum be dragged away", async () => {
    const user = userEvent.setup();
    renderDialog();

    await waitForLayout();

    await user.click(cutButtons()[0]);

    // The 25 is a single quantum, so its icon offers no drag at all.
    const firstGroup = screen.getByRole("group", { name: /^Group 1 / });

    expect(firstGroup.querySelector('[draggable="true"]')).toBeNull();
  });

  /**
   * A force under two quanta has nothing to divide. Saying so beats a dialog of one uncuttable atom, which would read as
   * though the player had done something wrong.
   */
  it("explains itself rather than offering a split the force is too small for", async () => {
    getSplitForceOptions.mockResolvedValue(options({ healthyCount: 25, injuredCount: 0, totalStrength: 25 }));

    renderDialog();

    expect(await screen.findByText(/there is nothing to\s+divide/)).toBeInTheDocument();
  });

  /** The remainder rides with a quantum rather than becoming a group of its own, which is what stops a lone 15 ever existing. */
  it("lays the remainder out as a fatter quantum, not a group of its own", async () => {
    getSplitForceOptions.mockResolvedValue(options({ healthyCount: 190, injuredCount: 0, totalStrength: 190 }));

    renderDialog();

    await waitForLayout();

    // 190 is seven atoms, not seven and a bit: the last is worth 40 and the leftover 15 rides with it.
    expect(atomIcons()).toHaveLength(7);
    expect(screen.getAllByText("25")).toHaveLength(6);
    expect(screen.getByText("40")).toBeInTheDocument();
  });

  /**
   * The bar under each atom is the only thing in the dialog that shows where the wounded ended up, and it has to follow the
   * division rather than the force that was divided — a group's bar is wrong the moment a cut moves casualties off it.
   */
  it("reads each atom's health from the group it belongs to", async () => {
    const user = userEvent.setup();

    getSplitForceOptions.mockResolvedValue(options({ healthyCount: 150, injuredCount: 50, totalStrength: 200 }));

    renderDialog();

    await waitForLayout();

    // Uncut, every atom belongs to the one 200-strong group.
    expect(screen.getAllByTitle("150 healthy · 50 injured")).toHaveLength(8);

    // Cutting 25 | 175 apportions the 50 wounded by size, largest remainder first: 6 to the quantum, 44 to the rest.
    await user.click(cutButtons()[0]);

    expect(screen.getAllByTitle("19 healthy · 6 injured")).toHaveLength(1);
    expect(screen.getAllByTitle("131 healthy · 44 injured")).toHaveLength(7);
  });

  /** The server's refusals are written for the player, so the dialog shows the wording it is given rather than its own. */
  it("shows the server's refusal as it stands", async () => {
    const user = userEvent.setup();
    splitForce.mockRejectedValue(new Error("Settler has already moved this turn. Splitting requires a full turn."));

    renderDialog();

    await waitForLayout();

    await user.click(cutButtons()[0]);
    await user.click(screen.getByRole("button", { name: "Split into 2 forces" }));

    expect(await screen.findByText(/already moved this turn/)).toBeInTheDocument();
  });
});
