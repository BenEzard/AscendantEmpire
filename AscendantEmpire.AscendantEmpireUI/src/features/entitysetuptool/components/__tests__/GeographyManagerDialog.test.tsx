// Wiring tests for GeographyManagerDialog, over the terrain flags specifically.
//
// The API module is mocked rather than the mutation hook, so what is pinned is the payload the dialog actually PUTs. That is the
// point of these tests: the dialog loads the whole catalogue, holds a draft of it, and sends the draft straight back, so any field
// missing from the Geography type round-trips as undefined and silently blanks a flag the map author never touched.

import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";

import GeographyManagerDialog from "../GeographyManagerDialog";
import type { Geography } from "@/types/map/Geography";

const getCampaignMapGeographies = vi.hoisted(() => vi.fn());
const putCampaignMapGeographies = vi.hoisted(() => vi.fn());

vi.mock("@/api/geographiesApi", () => ({
  getCampaignMapGeographies,
  putCampaignMapGeographies,
}));

/** Deep Water as the seed has it: water, and traversable — the pair that is not the default for either flag. */
function deepWater(overrides: Partial<Geography> = {}): Geography {
  return {
    campaignMapGeographyType: "DeepWater",
    name: "Deep Water",
    relativeElevation: -2,
    movementCost: 1,
    isWater: true,
    isTraversable: true,
    mapEditorIcon: null,
    isPaintableOnCampaignMap: true,
    ...overrides,
  };
}

/** Mountainous as the seed has it: the one untraversable Geography. */
function mountainous(overrides: Partial<Geography> = {}): Geography {
  return {
    campaignMapGeographyType: "Mountainous",
    name: "Mountainous",
    relativeElevation: 2,
    movementCost: 999,
    isWater: false,
    isTraversable: false,
    mapEditorIcon: "Mountain",
    isPaintableOnCampaignMap: true,
    ...overrides,
  };
}

async function renderDialog(catalogue: Geography[]) {
  getCampaignMapGeographies.mockResolvedValue(catalogue);
  putCampaignMapGeographies.mockResolvedValue(undefined);

  // Retries off: a test asserting on a refusal would otherwise wait for react-query to try again first.
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } });

  render(
    <QueryClientProvider client={queryClient}>
      <GeographyManagerDialog open onCancel={vi.fn()} onSaveSuccess={vi.fn()} />
    </QueryClientProvider>,
  );

  // The draft is seeded once the catalogue query lands, and the dropdown is disabled until it does.
  await waitFor(() => expect(screen.getByRole("option", { name: catalogue[0].name })).toBeInTheDocument());

  return userEvent.setup();
}

// The fields have plain sibling <label>s with no htmlFor, so they are not reachable by label text. Name and Map Editor Icon are the
// only two text boxes on the page, in that order; the numeric fields are spinbuttons and the flags are checkboxes.
const nameInput = () => screen.getAllByRole("textbox")[0] as HTMLInputElement;
const saveButton = () => screen.getByRole("button", { name: "Save" });
const waterCheckbox = () => screen.getByLabelText("Is Water?");
const elevationInput = () => screen.getAllByRole("spinbutton")[0] as HTMLInputElement;
const traversableCheckbox = () => screen.getByLabelText("Is Traversable?");

/** The single Geography the dialog PUT, by its type. */
function savedGeography(type: Geography["campaignMapGeographyType"]): Geography {
  const [payload] = putCampaignMapGeographies.mock.calls.at(-1) as [Geography[]];
  const found = payload.find((g) => g.campaignMapGeographyType === type);

  if (!found) {
    throw new Error(`The payload carried no ${type} row.`);
  }

  return found;
}

beforeEach(() => {
  getCampaignMapGeographies.mockReset();
  putCampaignMapGeographies.mockReset();
});

it("keeps both terrain flags in the payload when saving", async () => {
  // Deep Water is water and traversable; Mountainous is neither. Editing only the name must leave all four values exactly as loaded.
  const user = await renderDialog([deepWater(), mountainous()]);

  await user.clear(nameInput());
  await user.type(nameInput(), "Abyssal Water");
  await user.click(saveButton());

  await waitFor(() => expect(putCampaignMapGeographies).toHaveBeenCalledTimes(1));

  expect(savedGeography("DeepWater")).toMatchObject({ name: "Abyssal Water", isWater: true, isTraversable: true });
  expect(savedGeography("Mountainous")).toMatchObject({ isWater: false, isTraversable: false });
});

it("sends the flipped flag when a terrain checkbox is toggled", async () => {
  const user = await renderDialog([deepWater()]);

  await user.click(waterCheckbox());
  await user.click(traversableCheckbox());
  await user.click(saveButton());

  await waitFor(() => expect(putCampaignMapGeographies).toHaveBeenCalledTimes(1));

  expect(savedGeography("DeepWater")).toMatchObject({ isWater: false, isTraversable: false });
});

it("shows each terrain flag's current value, so an author can see what they are changing", async () => {
  await renderDialog([mountainous()]);

  expect(waterCheckbox()).not.toBeChecked();
  expect(traversableCheckbox()).not.toBeChecked();
});

it("locks Is Water while the geography sits above sea level, the pair the catalogue rule refuses", async () => {
  // CampaignMapGeographyValidator refuses a water Geography above sea level, and BiomeManagerDialog locks its own water/elevation pair the
  // same way. Mountainous is at elevation 2, so ticking Is Water here could only earn a 400 the dialog surfaces a single line of.
  await renderDialog([mountainous()]);

  expect(waterCheckbox()).toBeDisabled();
  expect(elevationInput()).not.toHaveAttribute("max");
});

it("caps the elevation of a water geography at sea level, leaving -2 authorable", async () => {
  // The other direction of the same rule: Deep Water belongs at -2, so the field is capped rather than disabled.
  await renderDialog([deepWater()]);

  expect(waterCheckbox()).toBeEnabled();
  expect(elevationInput()).toHaveAttribute("max", "0");
});
