import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { useState } from "react";
import DescriptiveSettingDropDown, { type SelectionOption } from "../DescriptiveSettingDropDown";

const options: SelectionOption[] = [
  { id: 1, label: "Alpha" },
  { id: 2, label: "Beta" },
  { id: 3, label: "Gamma" },
];

const noSelectionOption: SelectionOption = { id: 0, label: "None" };
const allSelectionOption: SelectionOption = { id: -1, label: "All" };

// Controlled wrapper so onChange round-trips back through value
function Wrapper(props: Partial<Parameters<typeof DescriptiveSettingDropDown>[0]>) {
  const [value, setValue] = useState<SelectionOption[]>(props.value ?? []);
  return (
    <DescriptiveSettingDropDown
      multiSelect={false}
      options={options}
      value={value}
      onChange={(v) => {
        setValue(v);
        props.onChange?.(v);
      }}
      {...props}
    />
  );
}

// ─── Display label ────────────────────────────────────────────────────────────

describe("display label", () => {
  it("shows noSelectionOption label when value is empty", () => {
    render(<Wrapper noSelectionOption={noSelectionOption} />);
    expect(screen.getByRole("button")).toHaveTextContent("None");
  });

  it("renders a non-breaking space when value is empty and noSelectionOption is absent", () => {
    render(<Wrapper />);
    expect(screen.getByRole("button").textContent).toBe("\u00A0");
  });

  it("shows the selected option's label for a single selection", () => {
    render(<Wrapper value={[options[0]]} />);
    expect(screen.getByRole("button")).toHaveTextContent("Alpha");
  });

  it("joins multiple labels with separator", () => {
    render(<Wrapper multiSelect value={[options[0], options[1]]} separator=" | " />);
    expect(screen.getByRole("button")).toHaveTextContent("Alpha | Beta");
  });

  it("uses finalSeparator before last item", () => {
    render(<Wrapper multiSelect value={[options[0], options[1], options[2]]} separator=", " finalSeparator=" and " />);
    expect(screen.getByRole("button")).toHaveTextContent("Alpha, Beta and Gamma");
  });

  it("falls back to separator when finalSeparator is omitted", () => {
    render(<Wrapper multiSelect value={[options[0], options[1]]} separator=", " />);
    expect(screen.getByRole("button")).toHaveTextContent("Alpha, Beta");
  });

  it("shows allSelectionOption label when all options are selected", () => {
    render(<Wrapper multiSelect value={[...options]} allSelectionOption={allSelectionOption} />);
    expect(screen.getByRole("button")).toHaveTextContent("All");
  });
});

// ─── Dropdown visibility ──────────────────────────────────────────────────────

describe("dropdown visibility", () => {
  it("is closed by default", () => {
    render(<Wrapper />);
    expect(screen.queryByRole("listbox")).not.toBeInTheDocument();
  });

  it("opens when the trigger is clicked", async () => {
    const user = userEvent.setup();
    render(<Wrapper />);
    await user.click(screen.getByRole("button"));
    expect(screen.getByRole("listbox")).toBeInTheDocument();
  });

  it("closes when the trigger is clicked again", async () => {
    const user = userEvent.setup();
    render(<Wrapper />);
    await user.click(screen.getByRole("button"));
    await user.click(screen.getByRole("button"));
    expect(screen.queryByRole("listbox")).not.toBeInTheDocument();
  });

  it("closes when clicking outside", async () => {
    const user = userEvent.setup();
    render(
      <div>
        <Wrapper />
        <button>Outside</button>
      </div>,
    );
    await user.click(document.querySelector('[aria-haspopup="listbox"]') as HTMLElement);
    expect(screen.getByRole("listbox")).toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: "Outside" }));
    expect(screen.queryByRole("listbox")).not.toBeInTheDocument();
  });
});

// ─── Keyboard: trigger ────────────────────────────────────────────────────────

describe("trigger keyboard interaction", () => {
  it("opens on Enter", async () => {
    const user = userEvent.setup();
    render(<Wrapper />);
    screen.getByRole("button").focus();
    await user.keyboard("{Enter}");
    expect(screen.getByRole("listbox")).toBeInTheDocument();
  });

  it("opens on Space", async () => {
    const user = userEvent.setup();
    render(<Wrapper />);
    screen.getByRole("button").focus();
    await user.keyboard(" ");
    expect(screen.getByRole("listbox")).toBeInTheDocument();
  });
});

// ─── Keyboard: dropdown ───────────────────────────────────────────────────────

describe("dropdown keyboard interaction", () => {
  it("closes on Escape", async () => {
    const user = userEvent.setup();
    render(<Wrapper />);
    await user.click(screen.getByRole("button"));
    expect(screen.getByRole("listbox")).toBeInTheDocument();
    await user.keyboard("{Escape}");
    expect(screen.queryByRole("listbox")).not.toBeInTheDocument();
  });

  it("navigates options with ArrowDown / ArrowUp", async () => {
    const user = userEvent.setup();
    render(<Wrapper />);
    await user.click(screen.getByRole("button"));
    const opts = screen.getAllByRole("option");
    // First option should be focused on open
    expect(document.activeElement).toBe(opts[0]);
    await user.keyboard("{ArrowDown}");
    expect(document.activeElement).toBe(opts[1]);
    await user.keyboard("{ArrowUp}");
    expect(document.activeElement).toBe(opts[0]);
  });

  it("selects an option with Enter", async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();
    render(<Wrapper onChange={onChange} />);
    await user.click(screen.getByRole("button"));
    await user.keyboard("{Enter}"); // first option focused
    expect(onChange).toHaveBeenCalledWith([options[0]]);
  });

  it("selects an option with Space", async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();
    render(<Wrapper onChange={onChange} />);
    await user.click(screen.getByRole("button"));
    await user.keyboard(" ");
    expect(onChange).toHaveBeenCalledWith([options[0]]);
  });
});

// ─── Single-select behaviour ──────────────────────────────────────────────────

describe("single-select", () => {
  it("selects an option and closes the dropdown", async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();
    render(<Wrapper onChange={onChange} />);
    await user.click(screen.getByRole("button"));
    await user.click(screen.getByRole("option", { name: "Beta" }));
    expect(onChange).toHaveBeenCalledWith([options[1]]);
    expect(screen.queryByRole("listbox")).not.toBeInTheDocument();
  });

  it("deselects the current option when noSelectionOption is provided", async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();
    render(<Wrapper value={[options[0]]} noSelectionOption={noSelectionOption} onChange={onChange} />);
    await user.click(screen.getByRole("button"));
    await user.click(screen.getByRole("option", { name: "Alpha" }));
    expect(onChange).toHaveBeenCalledWith([]);
  });

  it("keeps selection when re-clicking selected option without noSelectionOption", async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();
    render(<Wrapper value={[options[0]]} onChange={onChange} />);
    await user.click(screen.getByRole("button"));
    await user.click(screen.getByRole("option", { name: "Alpha" }));
    expect(onChange).toHaveBeenCalledWith([options[0]]);
  });

  it("marks the selected option as aria-selected", async () => {
    const user = userEvent.setup();
    render(<Wrapper value={[options[1]]} />);
    await user.click(screen.getByRole("button"));
    const betaOption = screen.getByRole("option", { name: "Beta" });
    expect(betaOption).toHaveAttribute("aria-selected", "true");
    expect(screen.getByRole("option", { name: "Alpha" })).toHaveAttribute("aria-selected", "false");
  });
});

// ─── Multi-select behaviour ───────────────────────────────────────────────────

describe("multi-select", () => {
  it("adds an option to the selection and keeps dropdown open", async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();
    render(<Wrapper multiSelect onChange={onChange} />);
    await user.click(screen.getByRole("button"));
    await user.click(screen.getByRole("option", { name: "Alpha" }));
    expect(onChange).toHaveBeenCalledWith([options[0]]);
    expect(screen.getByRole("listbox")).toBeInTheDocument();
  });

  it("removes an option when already selected", async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();
    render(<Wrapper multiSelect value={[options[0], options[1]]} onChange={onChange} />);
    await user.click(screen.getByRole("button"));
    await user.click(screen.getByRole("option", { name: "Alpha" }));
    expect(onChange).toHaveBeenCalledWith([options[1]]);
  });

  it("marks multiple options as aria-selected", async () => {
    const user = userEvent.setup();
    render(<Wrapper multiSelect value={[options[0], options[2]]} />);
    await user.click(screen.getByRole("button"));
    expect(screen.getByRole("option", { name: "Alpha" })).toHaveAttribute("aria-selected", "true");
    expect(screen.getByRole("option", { name: "Beta" })).toHaveAttribute("aria-selected", "false");
    expect(screen.getByRole("option", { name: "Gamma" })).toHaveAttribute("aria-selected", "true");
  });

  it("listbox has aria-multiselectable='true' in multi-select mode", async () => {
    const user = userEvent.setup();
    render(<Wrapper multiSelect />);
    await user.click(screen.getByRole("button"));
    expect(screen.getByRole("listbox")).toHaveAttribute("aria-multiselectable", "true");
  });
});

// ─── Accessibility attributes ─────────────────────────────────────────────────

describe("accessibility", () => {
  it("trigger has aria-haspopup='listbox'", () => {
    render(<Wrapper />);
    expect(screen.getByRole("button")).toHaveAttribute("aria-haspopup", "listbox");
  });

  it("trigger reflects aria-expanded state", async () => {
    const user = userEvent.setup();
    render(<Wrapper />);
    const trigger = screen.getByRole("button");
    expect(trigger).toHaveAttribute("aria-expanded", "false");
    await user.click(trigger);
    expect(trigger).toHaveAttribute("aria-expanded", "true");
  });
});
