// Wiring tests for SupplySection — the component dialog's Supply page.
//
// Purely presentational: the sets and the requirements come in as props and the edited requirements go out through onChange, so there is no network here.
// What is pinned is the Body-only rule, that a set cannot be required twice, and that the quantity says what it is measured against.

import { useState } from "react";

import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";

import SupplySection from "../sections/SupplySection";
import type { SupplySetView } from "../../api/supplySetApi";
import type { UnitComponentSupplyRequirement } from "@/features/unitdesign/types/UnitComponent";

const HUMAN_FOOD: SupplySetView = { id: 7, code: "human-food", displayName: "Human Food", supplyType: "Food", assets: [] };
const MOUNT_FODDER: SupplySetView = { id: 8, code: "mount-fodder", displayName: "Mount Fodder", supplyType: "Fuel", assets: [] };
const HUMAN_MEDICAL: SupplySetView = { id: 9, code: "human-medical", displayName: "Human Medical Supplies", supplyType: "Repair", assets: [] };

const ALL_SETS = [HUMAN_FOOD, MOUNT_FODDER, HUMAN_MEDICAL];

function renderSection(
  requirements: UnitComponentSupplyRequirement[] = [],
  componentType: "Body" | "Weapon" = "Body",
  sets: SupplySetView[] = ALL_SETS,
) {
  const onChange = vi.fn();

  render(<SupplySection componentType={componentType} requirements={requirements} onChange={onChange} availableSets={sets} />);

  return { onChange, user: userEvent.setup() };
}

/**
 * Renders the section the way the dialog does — holding the requirements, so an edit lands back on the control the next keystroke types into.
 */
function renderStatefulSection(initial: UnitComponentSupplyRequirement[]) {
  const onChange = vi.fn();

  function Host() {
    const [requirements, setRequirements] = useState(initial);

    return (
      <SupplySection
        componentType="Body"
        requirements={requirements}
        onChange={(next) => {
          onChange(next);
          setRequirements(next);
        }}
        availableSets={ALL_SETS}
      />
    );
  }

  render(<Host />);

  return { onChange, user: userEvent.setup() };
}

/** A requirement for one unit of the named set per troop. */
function requirement(supplySetCode: string, quantityRaw = 1000): UnitComponentSupplyRequirement {
  return { supplySetCode, quantity: quantityRaw };
}

describe("the Body-only rule", () => {
  it("renders only for a Body component, because only a Body may carry a requirement", () => {
    renderSection([], "Weapon");

    expect(screen.queryByLabelText("Add a supply set")).not.toBeInTheDocument();
    expect(screen.getByText(/only a Body/i)).toBeInTheDocument();
  });
});

describe("the set picker", () => {
  it("offers each supply set once, dropping those already required", () => {
    renderSection([requirement("human-food")]);

    const options = screen.getAllByRole("option").map((option) => option.textContent);

    expect(options).not.toContain(HUMAN_FOOD.displayName);
    expect(options).toContain(MOUNT_FODDER.displayName);
    expect(options).toContain(HUMAN_MEDICAL.displayName);
  });

  it("adds the chosen set at one unit per basis, which is the commonest requirement", async () => {
    const { onChange, user } = renderSection();

    await user.selectOptions(screen.getByLabelText("Add a supply set"), MOUNT_FODDER.code);

    expect(onChange).toHaveBeenCalledWith([{ supplySetCode: MOUNT_FODDER.code, quantity: 1000 }]);
  });

  it("removes a requirement the component no longer carries", async () => {
    const { onChange, user } = renderSection([requirement("human-food"), requirement("mount-fodder")]);

    await user.click(screen.getByRole("button", { name: `Remove ${HUMAN_FOOD.displayName}` }));

    expect(onChange).toHaveBeenCalledWith([{ supplySetCode: MOUNT_FODDER.code, quantity: 1000 }]);
  });
});

describe("the quantity", () => {
  it.each([
    [HUMAN_FOOD, "per troop"],
    [MOUNT_FODDER, "per movement point"],
    [HUMAN_MEDICAL, "per damage point"],
  ])("labels the quantity by the selected set type: $1", (set, basis) => {
    renderSection([requirement(set.code)]);

    expect(screen.getByText(basis)).toBeInTheDocument();
  });

  it("sends the quantity scaled by 1000 from the logical value entered", async () => {
    const { onChange, user } = renderStatefulSection([requirement("human-food")]);

    const quantityField = screen.getByLabelText(`Quantity of ${HUMAN_FOOD.displayName}`);
    await user.clear(quantityField);
    await user.type(quantityField, "3");

    expect(onChange).toHaveBeenLastCalledWith([{ supplySetCode: HUMAN_FOOD.code, quantity: 3000 }]);
  });
});
