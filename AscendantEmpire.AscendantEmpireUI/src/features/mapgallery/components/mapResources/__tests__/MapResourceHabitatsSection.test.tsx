// Tests for the Habitats page's water-ness filter: a water MapResourceType may only be authored on water Biomes and water
// Geographies, a land one only on land. The section is purely presentational — props in, onChange out — so no mocking is needed.
//
// The server enforces the same rule in PutMapResourceCatalogueHandler, since which Geographies are water is database state. These
// tests pin what the page *offers*; that one pins what the API accepts.

import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";

import MapResourceHabitatsSection from "../MapResourceHabitatsSection";
import type { Biome, BiomeType } from "@/types/map/Biome";
import type { Geography } from "@/types/map/Geography";
import type { MapResourceType } from "@/types/map/MapResource";
import { createMapResourceType } from "../../../tools/mapResourceDraft";

function biome(biomeType: BiomeType, name: string, isWater: boolean): Biome {
  return {
    biomeType,
    name,
    movementCost: 1,
    doesBlockSight: false,
    isWater,
    isTraversable: true,
    regenerationPercent: 0.1,
    isPaintableOnCampaignMap: true,
    mapEditorHtmlColor: "#90ee90",
    canBeElevated: true,
    baseForageValue: null,
    baseHuntValue: null,
    baseFishValue: null,
    baseFodderValue: null,
    baseBoneValue: null,
    baseWoodValue: null,
    baseStoneValue: null,
  };
}

function geography(campaignMapGeographyType: Geography["campaignMapGeographyType"], name: string, isWater: boolean, relativeElevation: number): Geography {
  return {
    campaignMapGeographyType,
    name,
    relativeElevation,
    movementCost: 1,
    isWater,
    isTraversable: true,
    mapEditorIcon: null,
    isPaintableOnCampaignMap: true,
  };
}

// Arid sorts first alphabetically and is land, which is what makes the new-row test below meaningful: an unfiltered "first unused
// Biome" would pick it even for a water resource.
const BIOMES: Biome[] = [biome("Ocean", "Ocean", true), biome("Coastal", "Coastal", true), biome("Arid", "Arid", false), biome("Forest", "Forest", false)];

const GEOGRAPHIES: Geography[] = [
  geography("DeepWater", "Deep Water", true, -2),
  geography("ShallowWater", "Shallow Water", true, -1),
  geography("Flat", "Flat", false, 0),
  geography("Hilly", "Hilly", false, 1),
];

function renderSection(isWaterResource: boolean, overrides: Partial<MapResourceType> = {}) {
  const type: MapResourceType = { ...createMapResourceType("Food", 1), isWaterResource, ...overrides };
  const onChange = vi.fn();

  render(<MapResourceHabitatsSection type={type} biomes={BIOMES} geographies={GEOGRAPHIES} onChange={onChange} />);

  return { onChange, user: userEvent.setup() };
}

/** A habitat row on the given Biome, so the selects have something to render. */
function habitatOn(biomeType: BiomeType): MapResourceType["habitats"][number] {
  return {
    biomeType,
    geographyType: null,
    initialQuantityMin: 1,
    initialQuantityMax: 2,
    maximumQuantity: 10,
    growthPercentage: 0.1,
    spreadLikelihood: null,
    spreadLimitMin: null,
    spreadLimitMax: null,
  };
}

const optionLabels = (ariaLabel: string) => Array.from(screen.getByRole("combobox", { name: ariaLabel }).querySelectorAll("option")).map((o) => o.textContent);

it("offers only water biomes and water geographies for a water resource", () => {
  renderSection(true, { habitats: [habitatOn("Ocean")] });

  expect(optionLabels("Biome")).toEqual(["Coastal", "Ocean"]);
  expect(optionLabels("Geography")).toEqual(["Any", "Deep Water", "Shallow Water"]);
});

it("offers only land biomes and land geographies for a land resource", () => {
  renderSection(false, { habitats: [habitatOn("Arid")] });

  expect(optionLabels("Biome")).toEqual(["Arid", "Forest"]);
  expect(optionLabels("Geography")).toEqual(["Any", "Flat", "Hilly"]);
});

it("still offers Any as a geography for a water resource", () => {
  // A habitat naming no Geography is legitimate for either kind: the tile's water-ness then comes from its Biome alone.
  renderSection(true, { habitats: [habitatOn("Ocean")] });

  expect(screen.getByRole("option", { name: "Any" })).toBeInTheDocument();
});

it("keeps a habitat's own biome and geography visible when the type's water-ness no longer matches", () => {
  // Flipping "Water Resource?" on an existing type leaves its habitats naming terrain of the old kind. The filter narrows the options a
  // row offers, never the value a row already holds, so without this the select renders blank while state keeps the land Biome - hiding
  // from the author the very row PutMapResourceCatalogueHandler is about to refuse.
  renderSection(true, { habitats: [{ ...habitatOn("Forest"), geographyType: "Hilly" }] });

  expect(screen.getByRole("combobox", { name: "Biome" })).toHaveValue("Forest");
  expect(screen.getByRole("combobox", { name: "Geography" })).toHaveValue("Hilly");
  expect(optionLabels("Biome")).toEqual(["Forest (not water)", "Coastal", "Ocean"]);
  expect(optionLabels("Geography")).toEqual(["Hilly (not water)", "Any", "Deep Water", "Shallow Water"]);
});

it("starts a new habitat row on a biome the filtered list offers", async () => {
  // The trap: the first unused Biome is picked from the whole catalogue unless the filter is applied first, so adding a habitat to
  // a water resource would start it on Arid — a value its own Biome select cannot show.
  const { onChange, user } = renderSection(true);

  await user.click(screen.getByRole("button", { name: /Habitat/ }));

  const [updated] = onChange.mock.calls.at(-1) as [MapResourceType];

  expect(["Ocean", "Coastal"]).toContain(updated.habitats[0].biomeType);
});
