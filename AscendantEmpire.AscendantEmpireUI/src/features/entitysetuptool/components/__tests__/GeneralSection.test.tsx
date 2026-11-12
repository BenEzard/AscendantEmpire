// Wiring tests for GeneralSection — the component dialog's General page.
//
// Purely presentational: the component comes in as a prop and edits go out through onChange, so there is no network here. What is pinned is the Component Type
// control, whose buttons carry only an icon and so would otherwise reach assistive tech unnamed.

import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";

import GeneralSection from "../sections/GeneralSection";
import { COMPONENT_TYPES, createEmptyComponent, type UnitComponent } from "@/features/unitdesign/types/UnitComponent";

function renderSection(component: UnitComponent = createEmptyComponent()) {
  const onChange = vi.fn();
  const onComponentTypeChange = vi.fn();

  render(<GeneralSection component={component} onChange={onChange} onComponentTypeChange={onComponentTypeChange} />);

  return { onChange, onComponentTypeChange, user: userEvent.setup() };
}

describe("the component type control", () => {
  it.each(COMPONENT_TYPES.map((type) => [type]))("names the %s button, which carries an icon and no text", (type) => {
    renderSection();

    expect(screen.getByRole("radio", { name: type })).toBeInTheDocument();
  });

  it("delegates the chosen type upward rather than patching the component itself, since the type swaps the extension", async () => {
    const { onChange, onComponentTypeChange, user } = renderSection();

    await user.click(screen.getByRole("radio", { name: "Weapon" }));

    expect(onComponentTypeChange).toHaveBeenCalledWith("Weapon");
    expect(onChange).not.toHaveBeenCalled();
  });
});
