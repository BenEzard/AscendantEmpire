// Every dropdown in the MapResource Type Manager that picks an enum-backed value.
//
// The API serialises enums as their member *name* (AscendantEmpire.Api/Program.cs registers a JsonStringEnumConverter), so a Biome is "Arid"
// and never 4. These selects used to coerce the chosen name with Number(), which gave NaN: the row then held a value matching no <option>, the
// browser fell back to showing the first one, and the designer's choice silently never stuck. Because the Biome list is sorted by name, that
// first option was "Arid" — the same value a new row defaults to — which is what hid the bug.
//
// So each test here changes a select and asserts the *name* reaches the draft. A coercion creeping back in fails them with NaN or null.

import { fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import type { Biome } from "@/types/map/Biome";
import type { Geography } from "@/types/map/Geography";
import type { AssetSummary, MapResourceType } from "@/types/map/MapResource";
import { createHabitat, createMapResourceType } from "../../../tools/mapResourceDraft";
import MapResourceGeneralSection from "../MapResourceGeneralSection";
import MapResourceHabitatsSection from "../MapResourceHabitatsSection";
import MapResourceScenariosSection from "../MapResourceScenariosSection";

/**
 * Two paintable Biomes whose names sort Arid-then-Forest, so Arid is the first option exactly as it is in the real catalogue.
 *
 * Both are land, as are the Geographies below, because the types in this file are land types: the Habitats page narrows both lists
 * by water-ness, so a Biome with no isWater at all would be filtered out and leave the selects empty.
 */
const BIOMES = [
  { biomeType: "Arid", name: "Arid", isWater: false, isPaintableOnCampaignMap: true } as Biome,
  { biomeType: "Forest", name: "Forest", isWater: false, isPaintableOnCampaignMap: true } as Biome,
];

const GEOGRAPHIES = [
  { campaignMapGeographyType: "Flat", name: "Flat", relativeElevation: 0, isWater: false, isPaintableOnCampaignMap: true } as Geography,
  { campaignMapGeographyType: "Hilly", name: "Hilly", relativeElevation: 1, isWater: false, isPaintableOnCampaignMap: true } as Geography,
];

const ASSETS: AssetSummary[] = [{ id: 1, code: "wood", displayName: "Wood", metaCategoryDisplayName: "Material", isNaturallyOccurring: true }];

/** A renewable type sitting in one Arid habitat — the state a designer is in right after adding their first habitat row. */
function typeInArid(): MapResourceType {
  return { ...createMapResourceType("food", 0), habitats: [createHabitat("renewable", "Arid")] };
}

describe("Habitats page Biome and Geography selects", () => {
  it("puts the chosen Biome's name in the habitat rather than coercing it to NaN", () => {
    const onChange = vi.fn();
    render(<MapResourceHabitatsSection type={typeInArid()} biomes={BIOMES} geographies={GEOGRAPHIES} onChange={onChange} />);

    fireEvent.change(screen.getByLabelText("Biome"), { target: { value: "Forest" } });

    expect(onChange).toHaveBeenCalledTimes(1);
    expect(onChange.mock.calls[0][0].habitats[0].biomeType).toBe("Forest");
  });

  it("puts the chosen Geography's name in the habitat", () => {
    const onChange = vi.fn();
    render(<MapResourceHabitatsSection type={typeInArid()} biomes={BIOMES} geographies={GEOGRAPHIES} onChange={onChange} />);

    fireEvent.change(screen.getByLabelText("Geography"), { target: { value: "Hilly" } });

    expect(onChange.mock.calls[0][0].habitats[0].geographyType).toBe("Hilly");
  });

  // "Any" is the select's own stand-in for a null Geography, which a <select> value cannot carry, so it must not be mistaken for a member name.
  it("stores null for the Any Geography, which stands for every Geography in the Biome", () => {
    const onChange = vi.fn();
    const type = { ...typeInArid(), habitats: [{ ...createHabitat("renewable", "Arid"), geographyType: "Hilly" as const }] };
    render(<MapResourceHabitatsSection type={type} biomes={BIOMES} geographies={GEOGRAPHIES} onChange={onChange} />);

    fireEvent.change(screen.getByLabelText("Geography"), { target: { value: "any" } });

    expect(onChange.mock.calls[0][0].habitats[0].geographyType).toBeNull();
  });

  // The select is controlled off the row, so a value matching no option is exactly how the old bug showed itself on screen.
  it("shows the habitat's own Biome rather than falling back to the first option", () => {
    const type = { ...typeInArid(), habitats: [createHabitat("renewable", "Forest")] };
    render(<MapResourceHabitatsSection type={type} biomes={BIOMES} geographies={GEOGRAPHIES} onChange={vi.fn()} />);

    expect(screen.getByLabelText("Biome")).toHaveValue("Forest");
  });
});

describe("Scenarios page Biome weight select", () => {
  it("puts the chosen Biome's name in the weight", () => {
    const onChange = vi.fn();
    const type: MapResourceType = {
      ...createMapResourceType("food", 0),
      isDistribution: true,
      distributionScenarios: [{ scenarioNumber: 1, displayName: null, amounts: [], biomeWeights: [{ biomeType: "Arid", weight: 1 }] }],
    };
    render(<MapResourceScenariosSection type={type} assets={ASSETS} biomes={BIOMES} onChange={onChange} />);

    fireEvent.change(screen.getByLabelText("Biome"), { target: { value: "Forest" } });

    expect(onChange.mock.calls[0][0].distributionScenarios[0].biomeWeights[0].biomeType).toBe("Forest");
  });
});

describe("General page Extraction select", () => {
  // The options used to be the enum's numbers while the loaded value was its name, so a saved method displayed as Gather whatever it really was.
  it("shows the type's saved extraction method rather than the first option", () => {
    const type: MapResourceType = { ...createMapResourceType("food", 0), extractionMethod: "Mine" };
    render(<MapResourceGeneralSection type={type} categories={[{ code: "food", displayName: "Food" }]} onChange={vi.fn()} />);

    expect(screen.getByDisplayValue("Mine")).toBeInTheDocument();
  });

  it("puts the chosen method's name on the type rather than coercing it to NaN", () => {
    const onChange = vi.fn();
    const type: MapResourceType = { ...createMapResourceType("food", 0), extractionMethod: "Mine" };
    render(<MapResourceGeneralSection type={type} categories={[{ code: "food", displayName: "Food" }]} onChange={onChange} />);

    fireEvent.change(screen.getByDisplayValue("Mine"), { target: { value: "Capture" } });

    // The last call, not the first: an unsaved type's CodeField derives a code from the name as it mounts, so it has already fired one.
    expect(onChange.mock.lastCall?.[0].extractionMethod).toBe("Capture");
  });
});
