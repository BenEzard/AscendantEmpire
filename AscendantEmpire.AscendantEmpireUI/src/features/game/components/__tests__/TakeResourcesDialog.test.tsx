import { fireEvent, render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";

import TakeResourcesDialog from "../TakeResourcesDialog";
import type { AssetStack, TakeResourcesForce, TileAssetStack } from "../takeResourcesCargo";

const BERRIES: AssetStack = { assetId: 1, displayName: "Berries", iconPath: "", quantity: 0 };
const FISH: AssetStack = { assetId: 2, displayName: "Fish", iconPath: "", quantity: 0 };

const TILE_ASSETS: TileAssetStack[] = [
  { ...BERRIES, quantity: 7000, isEstimate: true },
  { ...FISH, quantity: 40, isEstimate: false },
];

function force(overrides: Partial<TakeResourcesForce> = {}): TakeResourcesForce {
  return {
    forceId: 1,
    displayName: "Spearmen",
    imageSrc: undefined,
    healthyIndividuals: 40,
    injuredIndividuals: 0,
    initialStrength: 40,
    movementRemaining: 3,
    movementPoints: 3,
    turnRemaining: 1,
    capacity: 200,
    carrying: [],
    ...overrides,
  };
}

function renderDialog(forces: TakeResourcesForce[], onConfirm = vi.fn()) {
  render(<TakeResourcesDialog show tileAssets={TILE_ASSETS} forces={forces} onClose={vi.fn()} onConfirm={onConfirm} />);

  return onConfirm;
}

function drag(asset: HTMLElement, target: HTMLElement) {
  fireEvent.dragStart(asset);
  fireEvent.dragOver(target);
  fireEvent.drop(target);
  fireEvent.dragEnd(asset);
}

const availablePanel = () => screen.getByRole("group", { name: "Available" });
const forcePanel = (name: string) => screen.getByRole("group", { name });

describe("TakeResourcesDialog", () => {
  it("shows the tile's Assets with estimates marked", () => {
    renderDialog([force()]);

    expect(within(availablePanel()).getByText("~7000")).toBeInTheDocument();
    expect(within(availablePanel()).getByText("40")).toBeInTheDocument();
    expect(screen.getByText("Drop")).toBeInTheDocument();
  });

  it("draws each Force's health above it, split into healthy, injured and lost", () => {
    renderDialog([force({ healthyIndividuals: 20, injuredIndividuals: 10, initialStrength: 40 })]);

    const panel = forcePanel("Spearmen");

    expect(panel.querySelector(".observed-force__health-fill--healthy")).toHaveStyle({ width: "50%" });
    expect(panel.querySelector(".observed-force__health-fill--injured")).toHaveStyle({ width: "25%" });
    expect(panel.querySelector(".observed-force__health-fill--dead")).toHaveStyle({ width: "25%" });
  });

  it("reads out each Force's health and movement beside it", () => {
    renderDialog([force({ healthyIndividuals: 20, injuredIndividuals: 10, movementRemaining: 1.5, movementPoints: 3 })]);

    expect(within(forcePanel("Spearmen")).getByText("Troops: 20 / 10")).toBeInTheDocument();
    expect(within(forcePanel("Spearmen")).getByText("Movement: 1.5 / 3")).toBeInTheDocument();
  });

  it("loads a Force with as much as it can carry and lowers what is available", () => {
    renderDialog([force()]);

    drag(within(availablePanel()).getByLabelText("Berries"), forcePanel("Spearmen"));

    expect(screen.getByLabelText("Berries carried by Spearmen")).toHaveValue(200);
    expect(within(availablePanel()).getByText("~6800")).toBeInTheDocument();
    expect(within(forcePanel("Spearmen")).getByText("Uses its turn")).toBeInTheDocument();
  });

  it("returns cargo dragged onto Drop to the available Assets", () => {
    renderDialog([force({ carrying: [{ ...FISH, quantity: 10 }] })]);

    drag(within(forcePanel("Spearmen")).getByLabelText("Fish"), screen.getByTestId("drop-zone"));

    expect(within(availablePanel()).getByText("50")).toBeInTheDocument();
    expect(screen.queryByLabelText("Fish carried by Spearmen")).not.toBeInTheDocument();
  });

  it("treats a drop anywhere on the Available panel as a Drop", () => {
    renderDialog([force({ carrying: [{ ...FISH, quantity: 10 }] })]);

    drag(within(forcePanel("Spearmen")).getByLabelText("Fish"), availablePanel());

    expect(within(availablePanel()).getByText("50")).toBeInTheDocument();
  });

  it("moves cargo between Forces", () => {
    renderDialog([force({ carrying: [{ ...FISH, quantity: 10 }] }), force({ forceId: 2, displayName: "Bowmen" })]);

    drag(within(forcePanel("Spearmen")).getByLabelText("Fish"), forcePanel("Bowmen"));

    expect(screen.getByLabelText("Fish carried by Bowmen")).toHaveValue(10);
    expect(screen.queryByLabelText("Fish carried by Spearmen")).not.toBeInTheDocument();
  });

  it("puts back the difference when a carried quantity is lowered", async () => {
    renderDialog([force()]);
    drag(within(availablePanel()).getByLabelText("Berries"), forcePanel("Spearmen"));

    const field = screen.getByLabelText("Berries carried by Spearmen");
    await userEvent.clear(field);
    await userEvent.type(field, "150{Enter}");

    expect(field).toHaveValue(150);
    expect(within(availablePanel()).getByText("~6850")).toBeInTheDocument();
  });

  it("will not raise a carried quantity", async () => {
    renderDialog([force({ carrying: [{ ...FISH, quantity: 10 }] })]);

    const field = screen.getByLabelText("Fish carried by Spearmen");
    await userEvent.clear(field);
    await userEvent.type(field, "99{Enter}");

    expect(field).toHaveValue(10);
  });

  it("greys out a Force with no turn left and offers it no slot to load", () => {
    renderDialog([force({ turnRemaining: 0 })]);

    expect(forcePanel("Spearmen")).toHaveClass("grayscale");
    expect(within(forcePanel("Spearmen")).getByText("No turns remaining")).toBeInTheDocument();
    expect(screen.queryByTestId("cargo-placeholder")).not.toBeInTheDocument();

    drag(within(availablePanel()).getByLabelText("Berries"), forcePanel("Spearmen"));

    expect(screen.queryByLabelText("Berries carried by Spearmen")).not.toBeInTheDocument();
  });

  it("offers a slot to load only while the Force has room", () => {
    renderDialog([force({ capacity: 10, carrying: [{ ...FISH, quantity: 10 }] })]);

    expect(screen.queryByTestId("cargo-placeholder")).not.toBeInTheDocument();
  });

  it("puts everything back as it was when the dialog opened on Reset", async () => {
    renderDialog([force({ carrying: [{ ...FISH, quantity: 10 }] })]);
    const reset = screen.getByRole("button", { name: "Reset" });

    expect(reset).toBeDisabled();

    drag(within(availablePanel()).getByLabelText("Berries"), forcePanel("Spearmen"));
    await userEvent.click(reset);

    expect(screen.queryByLabelText("Berries carried by Spearmen")).not.toBeInTheDocument();
    expect(screen.getByLabelText("Fish carried by Spearmen")).toHaveValue(10);
    expect(within(availablePanel()).getByText("~7000")).toBeInTheDocument();
    expect(reset).toBeDisabled();
  });

  it("enables Confirm once something moves and hands back the plan", async () => {
    const onConfirm = renderDialog([force()]);
    const confirm = screen.getByRole("button", { name: "Confirm" });

    expect(confirm).toBeDisabled();

    drag(within(availablePanel()).getByLabelText("Fish"), forcePanel("Spearmen"));
    await userEvent.click(confirm);

    expect(onConfirm).toHaveBeenCalledWith({
      available: [{ ...BERRIES, quantity: 7000, isEstimate: true }],
      forces: [{ forceId: 1, carrying: [{ ...FISH, quantity: 40 }], spendsTurn: true }],
    });
  });
});
