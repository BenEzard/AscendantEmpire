// Tests for the tile-scoped MapResource placement dialog: a tile holds at most two MapResources (CampaignMapGalleryResource.MAX_SLOT), and the
// dialog edits both at once rather than making the designer pick one.
//
// The dialog is presentational — placements and the catalogue in, edits out — so nothing is mocked. What matters here is that Save reports only the
// sections that actually changed, since each edit it reports becomes its own PUT.

import { describe, expect, it, vi } from "vitest";
import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";

import MapResourcePlacementDialog from "../MapResourcePlacementDialog";
import type { GalleryMapResource } from "../../../api/galleryMapResourcesApi";
import type { MapResourceType } from "@/types/map/MapResource";
import { createHabitat, createMapResourceType } from "../../../tools/mapResourceDraft";

const FOREST = "Forest";
const FLAT = "Flat";

function placement(id: number, slot: number, mapResourceTypeId: number, initialQuantity: number | null = null): GalleryMapResource {
  return { id, xCoord: 12, yCoord: 34, slot, mapResourceTypeId, initialQuantity, distributionScenarioNumber: null };
}

/** A renewable type whose Forest habitat rolls 300–2000 and tops out at 5000. */
function berries(): MapResourceType {
  return {
    ...createMapResourceType("food", 10),
    id: 1,
    code: "BERRIES",
    displayName: "Berries",
    isRenewable: true,
    habitats: [{ ...createHabitat("renewable", FOREST), initialQuantityMin: 300, initialQuantityMax: 2000, maximumQuantity: 5000 }],
  };
}

/** A fixed type: a starting quantity, but no ceiling to grow to. */
function timber(): MapResourceType {
  return {
    ...createMapResourceType("materials", 20),
    id: 2,
    code: "TIMBER",
    displayName: "Timber",
    habitats: [{ ...createHabitat("fixed", FOREST), initialQuantityMin: 50, initialQuantityMax: 80 }],
  };
}

function coal(): MapResourceType {
  return {
    ...createMapResourceType("materials", 30),
    id: 3,
    code: "COAL",
    displayName: "Coal Seam",
    isStratified: true,
    habitats: [createHabitat("stratified", FOREST)],
  };
}

function renderDialog(placements: GalleryMapResource[], types: MapResourceType[], handlers: Partial<{ onSave: () => void; onDelete: () => void }> = {}) {
  const onSave = vi.fn();
  const onDelete = vi.fn();

  render(
    <MapResourcePlacementDialog
      tile={{ x: 12, y: 34 }}
      placements={placements}
      typesById={new Map(types.map((type) => [type.id!, type]))}
      biomeType={FOREST}
      geographyType={FLAT}
      busy={false}
      errors={new Map()}
      onSave={handlers.onSave ?? onSave}
      onDelete={handlers.onDelete ?? onDelete}
      onClose={vi.fn()}
    />,
  );

  return { onSave, onDelete };
}

describe("MapResourcePlacementDialog", () => {
  it("shows every MapResource on the tile, each with its own fields", () => {
    renderDialog([placement(10, 1, 1), placement(11, 2, 2)], [berries(), timber()]);

    expect(screen.getByText("Map Resources (Tile 12, 34)")).toBeInTheDocument();
    expect(screen.getByRole("region", { name: "Berries" })).toBeInTheDocument();
    expect(screen.getByRole("region", { name: "Timber" })).toBeInTheDocument();
    expect(screen.getByLabelText("Starting quantity for Berries")).toBeInTheDocument();
    expect(screen.getByLabelText("Starting quantity for Timber")).toBeInTheDocument();
  });

  it("writes the habitat's figures with thousands separators", () => {
    renderDialog([placement(10, 1, 1)], [berries()]);

    expect(screen.getByLabelText("Starting quantity for Berries")).toHaveAttribute("placeholder", "Roll 300–2,000");
    expect(screen.getByText("Grows to at most 5,000 here.")).toBeInTheDocument();
  });

  it("reports only the resource whose specifics changed, so an untouched one is not rewritten", async () => {
    const user = userEvent.setup();
    const { onSave } = renderDialog([placement(10, 1, 1), placement(11, 2, 2, 60)], [berries(), timber()]);

    await user.type(screen.getByLabelText("Starting quantity for Berries"), "900");
    await user.click(screen.getByRole("button", { name: "Save" }));

    expect(onSave).toHaveBeenCalledWith([{ placementId: 10, specifics: { initialQuantity: 900, distributionScenarioNumber: null } }]);
  });

  it("reports nothing when Save is pressed without an edit", async () => {
    const user = userEvent.setup();
    const { onSave } = renderDialog([placement(10, 1, 1, 400)], [berries()]);

    await user.click(screen.getByRole("button", { name: "Save" }));

    expect(onSave).toHaveBeenCalledWith([]);
  });

  it("deletes the resource whose own Delete was pressed, not the tile's first", async () => {
    const user = userEvent.setup();
    const { onDelete } = renderDialog([placement(10, 1, 1), placement(11, 2, 2)], [berries(), timber()]);

    await user.click(within(screen.getByRole("region", { name: "Timber" })).getByRole("button", { name: "Delete Timber" }));

    expect(onDelete).toHaveBeenCalledWith(11);
  });

  it("disables Save on a tile holding only stratified resources, which have nothing to set", () => {
    renderDialog([placement(10, 1, 3)], [coal()]);

    expect(screen.getByRole("button", { name: "Save" })).toBeDisabled();
  });

  it("leaves Save usable when a stratified resource shares the tile with a settable one", () => {
    renderDialog([placement(10, 1, 3), placement(11, 2, 1)], [coal(), berries()]);

    expect(screen.getByRole("button", { name: "Save" })).toBeEnabled();
  });
});
