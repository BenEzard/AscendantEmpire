// Wiring tests for SupplySetManagerDialog.
//
// The api module is mocked rather than the http layer, so what is pinned is the payload the dialog actually sends — the rates it scales and the members it
// resolves — and the rules it refuses a save on before anything leaves the browser.
//
// Members are chosen in two wells: the catalogue on the left, the set's members on the right. An Asset sits in exactly one of them, so a tile is addressed by
// name — bare on the left, name plus amount on the right, which is also how its accessible name reads.

import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";

import SupplySetManagerDialog from "../SupplySetManagerDialog";
import type { AssetOption } from "../../api/assetApi";
import type { SupplySetView } from "../../api/supplySetApi";

const getSupplySets = vi.hoisted(() => vi.fn());
const createSupplySet = vi.hoisted(() => vi.fn());
const updateSupplySet = vi.hoisted(() => vi.fn());
const getAssetOptions = vi.hoisted(() => vi.fn());

vi.mock("../../api/supplySetApi", () => ({
  getSupplySets,
  createSupplySet,
  updateSupplySet,
}));

vi.mock("../../api/assetApi", () => ({
  getAssetOptions,
}));

const BERRIES: AssetOption = { id: 1, code: "Foodstuffs.PlantFoods.Berries", displayName: "Berries", iconPath: "/assets/berries.png" };
const FODDER: AssetOption = { id: 2, code: "RawMaterials.RawMaterials.Fodder", displayName: "Fodder", iconPath: "/assets/fodder.png" };
// No icon of its own, so its tile falls back to the generic glyph rather than rendering a broken image.
const STEEL_PLATE: AssetOption = { id: 3, code: "ConstructionMaterials.Metal.SteelPlate", displayName: "Steel Plate", iconPath: "" };

/** A saved Food set holding Berries, as the seed leaves it. */
function humanFood(): SupplySetView {
  return {
    id: 7,
    code: "human-food",
    displayName: "Human Food",
    supplyType: "Food",
    assets: [{ assetId: 1, assetCode: BERRIES.code, assetDisplayName: "Berries", amountPerUnit: 1670, selectionWeight: 1 }],
  };
}

/** A saved Repair set, whose members are drawn from and so carry weights that matter. */
function humanMedical(): SupplySetView {
  return {
    id: 8,
    code: "human-medical",
    displayName: "Human Medical Supplies",
    supplyType: "Repair",
    assets: [{ assetId: 3, assetCode: STEEL_PLATE.code, assetDisplayName: "Steel Plate", amountPerUnit: 1000, selectionWeight: 3 }],
  };
}

async function renderDialog(sets: SupplySetView[]) {
  getSupplySets.mockResolvedValue(sets);
  getAssetOptions.mockResolvedValue([BERRIES, FODDER, STEEL_PLATE]);
  createSupplySet.mockResolvedValue(sets[0] ?? humanFood());
  updateSupplySet.mockResolvedValue(sets[0] ?? humanFood());

  render(<SupplySetManagerDialog open onCancel={vi.fn()} onSaveSuccess={vi.fn()} />);

  // The form is seeded once the catalogue lands.
  await waitFor(() => expect(screen.getByLabelText("Supply Set")).toBeInTheDocument());

  return userEvent.setup();
}

const saveButton = () => screen.getByRole("button", { name: "Save" });
const setPicker = () => screen.getByLabelText("Supply Set") as HTMLSelectElement;

/** Starts authoring an unsaved set. The picker lists saved sets only, so this is the one route to a blank form. */
const startNewSet = (user: ReturnType<typeof userEvent.setup>) => user.click(screen.getByRole("button", { name: "New" }));

/** A catalogue tile, named by the Asset alone. */
const catalogueTile = (displayName: string) => screen.getByRole("button", { name: displayName });

/** A member tile, named by the Asset and the amount it carries, the way PillKeyValueEditor names its pills. */
const memberTile = (displayName: string, amount: number) => screen.getByRole("button", { name: `${displayName} ${amount}` });

const catalogueWell = () => screen.getByRole("group", { name: "Asset catalogue" });
const memberWell = () => screen.getByRole("group", { name: "Assets in this set" });

/** jsdom implements no DataTransfer, so a drag carries a stand-in for the one the browser would supply. */
function dragStandIn(assetCode: string) {
  return { setData: vi.fn(), getData: () => assetCode, effectAllowed: "", dropEffect: "" };
}

/** Drags a tile onto a well the way the browser would: start on the tile, then over and into the target. */
function drag(tile: HTMLElement, target: HTMLElement, assetCode: string) {
  const dataTransfer = dragStandIn(assetCode);

  fireEvent.dragStart(tile, { dataTransfer });
  fireEvent.dragOver(target, { dataTransfer });
  fireEvent.drop(target, { dataTransfer });
}

describe("the two wells", () => {
  it("leaves an Asset already in the set out of the catalogue well, so it cannot be added twice", async () => {
    await renderDialog([humanFood()]);

    expect(screen.queryByRole("button", { name: "Berries" })).not.toBeInTheDocument();
    expect(memberTile("Berries", 1.67)).toBeInTheDocument();
  });

  it("filters the catalogue well by the search text, matching name or code", async () => {
    const user = await renderDialog([humanFood()]);

    await user.type(screen.getByLabelText("Search Assets"), "fodd");

    expect(catalogueTile("Fodder")).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Steel Plate" })).not.toBeInTheDocument();
  });

  it("adds an Asset on double-click at an amount of 1, the keyboard-reachable route a drag cannot give", async () => {
    const user = await renderDialog([humanFood()]);

    await user.dblClick(catalogueTile("Fodder"));

    expect(memberTile("Fodder", 1)).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Fodder" })).not.toBeInTheDocument();
  });

  it("takes a member back out on double-click, returning it to the catalogue well", async () => {
    const user = await renderDialog([humanFood()]);

    await user.dblClick(memberTile("Berries", 1.67));

    expect(catalogueTile("Berries")).toBeInTheDocument();
  });

  it("adds an Asset dragged from the catalogue into the set", async () => {
    await renderDialog([humanFood()]);

    drag(catalogueTile("Fodder"), memberWell(), FODDER.code);

    expect(memberTile("Fodder", 1)).toBeInTheDocument();
  });

  it("removes a member dragged back onto the catalogue, the mirror of dragging one in", async () => {
    await renderDialog([humanFood()]);

    drag(memberTile("Berries", 1.67), catalogueWell(), BERRIES.code);

    expect(catalogueTile("Berries")).toBeInTheDocument();
  });

  it("edits the selected member's amount through the one Amount field, rather than a field per member", async () => {
    const user = await renderDialog([humanFood()]);

    await user.click(memberTile("Berries", 1.67));

    const amountField = screen.getByLabelText("Amount");
    expect(amountField).toHaveValue(1.67);

    await user.clear(amountField);
    await user.type(amountField, "2.5");

    expect(memberTile("Berries", 2.5)).toBeInTheDocument();
  });

  it("offers a Weight field only for a Repair set, since only a Repair draw reads one", async () => {
    const user = await renderDialog([humanFood(), humanMedical()]);

    expect(screen.queryByLabelText("Weight")).not.toBeInTheDocument();

    await user.selectOptions(setPicker(), "8");

    expect(screen.getByLabelText("Weight")).toBeInTheDocument();
  });
});

describe("picking which set to edit", () => {
  it("offers the saved sets and nothing else, so the dropdown never lists a set that does not exist", async () => {
    await renderDialog([humanFood(), humanMedical()]);

    // By role, which is what the dropdown actually offers: the placeholder an unsaved draft selects is hidden, and so is absent from here.
    expect(screen.getAllByRole("option").map((option) => option.textContent)).toEqual(["Human Food", "Human Medical Supplies"]);
  });

  it("clears the form on New, leaving the picker on no saved set", async () => {
    const user = await renderDialog([humanFood()]);

    await startNewSet(user);

    expect(setPicker()).toHaveValue("");
    expect(screen.getByLabelText("Display name")).toHaveValue("");
    expect(screen.queryByRole("button", { name: "Berries 1.67" })).not.toBeInTheDocument();
  });

  it("returns to a saved set after New, so starting one is not a dead end", async () => {
    const user = await renderDialog([humanFood()]);

    await startNewSet(user);
    await user.selectOptions(setPicker(), "7");

    expect(screen.getByLabelText("Display name")).toHaveValue("Human Food");
    expect(memberTile("Berries", 1.67)).toBeInTheDocument();
  });
});

describe("the supply type control", () => {
  it("picks the type with mutually-exclusive icon buttons, the control the component dialog uses for Component Type", async () => {
    const user = await renderDialog([humanFood()]);

    expect(screen.getByRole("radio", { name: "Food" })).toBeChecked();

    // Switched on an empty set, where no confirmation stands in the way: what this pins is the control, not the guard below it.
    await startNewSet(user);
    await user.click(screen.getByRole("radio", { name: "Repair" }));

    expect(screen.getByRole("radio", { name: "Repair" })).toBeChecked();
    expect(screen.getByRole("radio", { name: "Food" })).not.toBeChecked();
  });

  it("asks before changing the type of a set that holds Assets, since a set satisfies one need and its members go with it", async () => {
    const user = await renderDialog([humanFood()]);

    await user.click(screen.getByRole("radio", { name: "Fuel" }));

    expect(screen.getByRole("button", { name: "Change and clear" })).toBeInTheDocument();
    expect(screen.getByRole("radio", { name: "Food" })).toBeChecked();
    expect(memberTile("Berries", 1.67)).toBeInTheDocument();
  });

  it("leaves the type and the members alone when the change is refused", async () => {
    const user = await renderDialog([humanFood()]);

    await user.click(screen.getByRole("radio", { name: "Fuel" }));
    await user.click(screen.getByRole("button", { name: "Cancel" }));

    expect(screen.getByRole("radio", { name: "Food" })).toBeChecked();
    expect(memberTile("Berries", 1.67)).toBeInTheDocument();
  });

  it("empties the set once the change is confirmed, so Food members cannot survive into a Fuel set", async () => {
    const user = await renderDialog([humanFood()]);

    await user.click(screen.getByRole("radio", { name: "Fuel" }));
    await user.click(screen.getByRole("button", { name: "Change and clear" }));

    expect(screen.getByRole("radio", { name: "Fuel" })).toBeChecked();
    expect(screen.queryByRole("button", { name: "Berries 1.67" })).not.toBeInTheDocument();
    expect(catalogueTile("Berries")).toBeInTheDocument();
  });

  it("changes the type without asking when the set holds nothing to lose", async () => {
    const user = await renderDialog([humanFood()]);

    await startNewSet(user);
    await user.click(screen.getByRole("radio", { name: "Fuel" }));

    expect(screen.getByRole("radio", { name: "Fuel" })).toBeChecked();
    expect(screen.queryByRole("button", { name: "Change and clear" })).not.toBeInTheDocument();
  });
});

describe("the save gate", () => {
  it("refuses to save a set with no members, matching the API rule", async () => {
    const user = await renderDialog([humanFood()]);

    await user.dblClick(memberTile("Berries", 1.67));
    await user.click(saveButton());

    expect(updateSupplySet).not.toHaveBeenCalled();
    expect(screen.getByText(/at least one Asset/i)).toBeInTheDocument();
  });

  it("sends amountPerUnit scaled by 1000 from the logical value entered", async () => {
    const user = await renderDialog([humanFood()]);

    await user.click(memberTile("Berries", 1.67));

    const amountField = screen.getByLabelText("Amount");
    await user.clear(amountField);
    await user.type(amountField, "2.5");

    await user.click(saveButton());

    await waitFor(() => expect(updateSupplySet).toHaveBeenCalledTimes(1));

    expect(updateSupplySet).toHaveBeenCalledWith(
      7,
      expect.objectContaining({
        code: "human-food",
        supplyType: "Food",
        assets: [expect.objectContaining({ assetCode: BERRIES.code, amountPerUnit: 2500 })],
      }),
    );
  });

  it("derives the code from the display name as a lowercase slug, the format every other setup dialog's CodeField produces", async () => {
    const user = await renderDialog([humanFood()]);

    await startNewSet(user);
    await user.type(screen.getByLabelText("Display name"), "Mount Fodder");

    expect(screen.getByPlaceholderText("supply set code, e.g. human-food")).toHaveValue("mount-fodder");
  });

  it("surfaces the server's reason for refusing a save, since the API names what is wrong", async () => {
    const user = await renderDialog([humanFood()]);

    // The http layer already joins the API's `errors` array into the rejected error's message (see ApiError), so that is what a caller has to surface.
    updateSupplySet.mockRejectedValue(new Error("Another supply set already uses code 'human-food'."));

    await user.click(saveButton());

    expect(await screen.findByText(/already uses code/i)).toBeInTheDocument();
  });

  it("creates a set the catalogue does not hold yet rather than updating one", async () => {
    const user = await renderDialog([humanFood()]);

    await startNewSet(user);
    await user.type(screen.getByLabelText("Display name"), "Mount Fodder");
    await user.click(screen.getByRole("radio", { name: "Fuel" }));
    await user.dblClick(catalogueTile("Fodder"));
    await user.click(saveButton());

    await waitFor(() => expect(createSupplySet).toHaveBeenCalledTimes(1));

    expect(createSupplySet).toHaveBeenCalledWith(
      expect.objectContaining({
        code: "mount-fodder",
        displayName: "Mount Fodder",
        supplyType: "Fuel",
        assets: [expect.objectContaining({ assetCode: FODDER.code })],
      }),
    );
  });
});
