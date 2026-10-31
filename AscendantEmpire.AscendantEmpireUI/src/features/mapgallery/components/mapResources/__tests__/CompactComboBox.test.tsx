// CompactComboBox — the type-to-filter picker used for the Scenarios page's Resource column.
//
// It edits a value rather than running a search, so the invariants worth pinning are about the value: the chosen option is always on display,
// the option's own value (not a string scraped off the DOM) is what comes back, and closing without picking changes nothing.

import { fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import type { AssetSummary, MapResourceType } from "@/types/map/MapResource";
import type { Biome } from "@/types/map/Biome";
import { createMapResourceType } from "../../../tools/mapResourceDraft";
import { CompactComboBox } from "../mapResourceFields";
import MapResourceScenariosSection from "../MapResourceScenariosSection";

const OPTIONS = [
  { value: 1, label: "Oak (Wood)" },
  { value: 2, label: "Cedar (Wood)" },
  { value: 3, label: "Copper Ore (Metal)" },
];

function renderComboBox(overrides: { value?: number; onChange?: (value: number) => void } = {}) {
  const onChange = overrides.onChange ?? vi.fn();
  render(<CompactComboBox ariaLabel="Resource" value={overrides.value ?? 1} options={OPTIONS} onChange={onChange} />);

  return { onChange, field: screen.getByLabelText("Resource") };
}

describe("CompactComboBox", () => {
  it("shows the chosen option's label while closed, so the cell reads as its value", () => {
    const { field } = renderComboBox({ value: 3 });

    expect(field).toHaveValue("Copper Ore (Metal)");
  });

  it("narrows the list to the options matching what is typed, ignoring case", () => {
    const { field } = renderComboBox();
    fireEvent.focus(field);

    fireEvent.change(field, { target: { value: "ced" } });

    expect(screen.getByRole("option", { name: "Cedar (Wood)" })).toBeInTheDocument();
    expect(screen.queryByRole("option", { name: "Oak (Wood)" })).not.toBeInTheDocument();
  });

  // The whole option is handed back, so the caller never parses a string. A DOM <select> round trip is what let Number() produce NaN here before.
  it("hands back the option's own value as a number rather than a string", () => {
    const { onChange, field } = renderComboBox();
    fireEvent.focus(field);

    fireEvent.pointerDown(screen.getByRole("option", { name: "Copper Ore (Metal)" }));

    expect(onChange).toHaveBeenCalledWith(3);
  });

  it("picks the arrowed-to option on Enter", () => {
    const { onChange, field } = renderComboBox();
    fireEvent.focus(field);

    fireEvent.keyDown(field, { key: "ArrowDown" });
    fireEvent.keyDown(field, { key: "Enter" });

    expect(onChange).toHaveBeenCalledWith(2);
  });

  it("abandons the typing on Escape, putting the chosen label back and leaving the value alone", () => {
    const { onChange, field } = renderComboBox({ value: 1 });
    fireEvent.focus(field);
    fireEvent.change(field, { target: { value: "copper" } });

    fireEvent.keyDown(field, { key: "Escape" });

    expect(onChange).not.toHaveBeenCalled();
    expect(field).toHaveValue("Oak (Wood)");
  });

  it("says so when nothing matches, rather than showing an empty list", () => {
    const { field } = renderComboBox();
    fireEvent.focus(field);

    fireEvent.change(field, { target: { value: "granite" } });

    expect(screen.getByText("No matches.")).toBeInTheDocument();
    expect(screen.queryAllByRole("option")).toHaveLength(0);
  });

  // assetOptions keeps a deleted Asset as its own option for this reason; a value with no option at all still must not render an empty cell.
  it("falls back to the raw value when no option carries it", () => {
    render(<CompactComboBox ariaLabel="Resource" value={99} options={OPTIONS} onChange={vi.fn()} />);

    expect(screen.getByLabelText("Resource")).toHaveValue("99");
  });
});

describe("Scenarios page Resource column", () => {
  const ASSETS: AssetSummary[] = [
    { id: 1, code: "oak", displayName: "Oak", metaCategoryDisplayName: "Wood", isNaturallyOccurring: true },
    { id: 2, code: "cedar", displayName: "Cedar", metaCategoryDisplayName: "Wood", isNaturallyOccurring: true },
  ];

  const BIOMES = [{ biomeType: "Forest", name: "Forest", isPaintableOnCampaignMap: true } as Biome];

  it("puts the picked Resource's id on the scenario amount", () => {
    const onChange = vi.fn();
    const type: MapResourceType = {
      ...createMapResourceType("food", 0),
      isDistribution: true,
      distributionScenarios: [{ scenarioNumber: 1, displayName: null, amounts: [{ assetId: 1, amount: 5 }], biomeWeights: [] }],
    };
    render(<MapResourceScenariosSection type={type} assets={ASSETS} biomes={BIOMES} onChange={onChange} />);

    const field = screen.getByLabelText("Resource");
    fireEvent.focus(field);
    fireEvent.change(field, { target: { value: "cedar" } });
    fireEvent.pointerDown(screen.getByRole("option", { name: "Cedar (Wood)" }));

    expect(onChange.mock.lastCall?.[0].distributionScenarios[0].amounts[0].assetId).toBe(2);
  });
});
