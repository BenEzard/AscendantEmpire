import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { useState } from "react";
import ColorPicker, { type ColorPickerProps } from "../colorpicker/ColorPicker";
import { TailwindColors } from "../colorpicker/tailwindColors";
import type { ColorPalette } from "../colorpicker/colorTypes";

const palettes: ColorPalette[] = [
  {
    title: "Primaries",
    colors: [
      { value: "#ff0000", description: "red" },
      { value: "#00ff00", description: "green" },
      { value: "#0000ff", description: "blue" },
      { value: "#ffffff", description: "white" },
    ],
  },
];

// Controlled wrapper so onChange round-trips back through value, as a real caller would wire it.
function Wrapper(props: Partial<ColorPickerProps>) {
  const [value, setValue] = useState<string | null>(props.value ?? null);
  return (
    <ColorPicker
      palettes={palettes}
      columns={2}
      {...props}
      value={value}
      onChange={(next, color) => {
        setValue(next);
        props.onChange?.(next, color);
      }}
    />
  );
}

/** Opens the popup by clicking the trigger square. */
async function openPicker(user: ReturnType<typeof userEvent.setup>) {
  await user.click(screen.getByRole("button", { name: "Select colour" }));
  return screen.getByRole("dialog", { name: "Colour picker" });
}

// ─── Opening and closing ──────────────────────────────────────────────────────

describe("opening and closing", () => {
  it("shows no popup until the trigger square is clicked", () => {
    render(<Wrapper />);
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
  });

  it("marks the trigger with a question mark while nothing is selected", () => {
    render(<Wrapper value={null} />);
    expect(screen.getByRole("button", { name: "Select colour" })).toHaveTextContent("?");
  });

  it("shows the colour rather than a question mark once something is selected", () => {
    render(<Wrapper value="#ff0000" />);
    expect(screen.getByRole("button", { name: "Select colour" })).not.toHaveTextContent("?");
  });

  it("opens the popup when the trigger square is clicked", async () => {
    const user = userEvent.setup();
    render(<Wrapper />);
    expect(await openPicker(user)).toBeInTheDocument();
  });

  it("closes when a pointer press lands outside the popup", async () => {
    const user = userEvent.setup();
    render(
      <div>
        <Wrapper />
        <button type="button">outside</button>
      </div>,
    );
    await openPicker(user);

    await user.click(screen.getByRole("button", { name: "outside" }));
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
  });

  it("closes on Escape", async () => {
    const user = userEvent.setup();
    render(<Wrapper />);
    await openPicker(user);

    await user.keyboard("{Escape}");
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
  });

  it("closes after a colour is picked", async () => {
    const user = userEvent.setup();
    render(<Wrapper />);
    await openPicker(user);

    await user.click(screen.getByRole("button", { name: "green" }));
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
  });
});

// ─── Palettes and columns ─────────────────────────────────────────────────────

describe("palettes", () => {
  it("renders each palette as a group named by its title", async () => {
    const user = userEvent.setup();
    render(<Wrapper />);
    await openPicker(user);

    expect(screen.getByRole("group", { name: "Primaries" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "red" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "white" })).toBeInTheDocument();
  });

  it("does not draw the palette title as visible text", async () => {
    const user = userEvent.setup();
    render(<Wrapper />);
    const popup = await openPicker(user);

    expect(popup).not.toHaveTextContent("Primaries");
  });

  it("lays every palette out on the requested column count", async () => {
    const user = userEvent.setup();
    const twoPalettes: ColorPalette[] = [palettes[0], { title: "Greys", colors: [{ value: "#888888", description: "grey" }] }];
    render(<Wrapper palettes={twoPalettes} columns={3} />);
    await openPicker(user);

    const grids = document.querySelectorAll<HTMLElement>("div.grid");
    expect(grids).toHaveLength(2);
    for (const grid of grids) {
      expect(grid.style.gridTemplateColumns).toBe("repeat(3, 18px)");
    }
  });

  it("defaults the column count to the number of Tailwind shades", async () => {
    const user = userEvent.setup();
    render(<Wrapper columns={undefined} />);
    await openPicker(user);

    const grid = document.querySelector<HTMLElement>("div.grid");
    expect(grid?.style.gridTemplateColumns).toBe(`repeat(${TailwindColors.ShadeCount}, 18px)`);
  });

  it("reports the picked colour and its palette entry", async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();
    render(<Wrapper onChange={onChange} />);
    await openPicker(user);

    await user.click(screen.getByRole("button", { name: "blue" }));
    expect(onChange).toHaveBeenCalledWith("#0000ff", { value: "#0000ff", description: "blue" });
  });
});

// ─── Selection ────────────────────────────────────────────────────────────────

describe("selection", () => {
  it("rings the selected swatch", async () => {
    const user = userEvent.setup();
    render(<Wrapper value="#ff0000" />);
    await openPicker(user);

    expect(screen.getByRole("button", { name: "red" })).toHaveAttribute("aria-pressed", "true");
    expect(screen.getByRole("button", { name: "green" })).toHaveAttribute("aria-pressed", "false");
  });

  it("matches the selection by parsed colour rather than by string", async () => {
    const user = userEvent.setup();
    render(<Wrapper value="rgb(255, 0, 0)" />);
    await openPicker(user);

    expect(screen.getByRole("button", { name: "red" })).toHaveAttribute("aria-pressed", "true");
  });
});

// ─── Used colours ─────────────────────────────────────────────────────────────

describe("used colours", () => {
  it("blocks selection of a used colour", async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();
    render(<Wrapper usedColors={[{ value: "#00ff00", usedBy: "Rome" }]} onChange={onChange} />);
    await openPicker(user);

    const green = screen.getByRole("button", { name: "green" });
    expect(green).toBeDisabled();

    await user.click(green);
    expect(onChange).not.toHaveBeenCalled();
    // The popup stays open, since the click was not a selection.
    expect(screen.getByRole("dialog")).toBeInTheDocument();
  });

  it("strikes a used swatch through with a diagonal", async () => {
    const user = userEvent.setup();
    render(<Wrapper usedColors={[{ value: "#00ff00", usedBy: "Rome" }]} />);
    await openPicker(user);

    const line = screen.getByRole("button", { name: "green" }).querySelector("line");
    expect(line).not.toBeNull();
    // Top-right to bottom-left, in the swatch's 10x10 view box.
    expect(line).toHaveAttribute("x1", "10");
    expect(line).toHaveAttribute("y1", "0");
    expect(line).toHaveAttribute("x2", "0");
    expect(line).toHaveAttribute("y2", "10");
  });

  it("names the owner on hover of a used swatch", async () => {
    const user = userEvent.setup();
    render(<Wrapper usedColors={[{ value: "#00ff00", usedBy: "Rome" }]} />);
    await openPicker(user);

    await user.hover(screen.getByRole("button", { name: "green" }));
    expect(await screen.findByRole("tooltip")).toHaveTextContent("Already used by `Rome`");
  });

  it("keeps the selected colour usable even when it is also in the used list", async () => {
    const user = userEvent.setup();
    render(<Wrapper value="#ff0000" usedColors={[{ value: "#ff0000", usedBy: "Rome" }]} />);
    await openPicker(user);

    const red = screen.getByRole("button", { name: "red" });
    expect(red).toBeEnabled();
    expect(red).toHaveAttribute("aria-pressed", "true");
    expect(red.querySelector("line")).toBeNull();
  });
});

// ─── Custom colour ────────────────────────────────────────────────────────────

describe("custom colour", () => {
  it("hides the custom field unless allowCustomColor is set", async () => {
    const user = userEvent.setup();
    render(<Wrapper />);
    await openPicker(user);

    expect(screen.queryByLabelText("Custom colour")).not.toBeInTheDocument();
  });

  it("takes over the selection when a valid colour is typed", async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();
    render(<Wrapper value="#ff0000" allowCustomColor onChange={onChange} />);
    await openPicker(user);

    await user.type(screen.getByLabelText("Custom colour"), "#123456");
    // No palette entry accompanies a hand-typed colour.
    expect(onChange).toHaveBeenLastCalledWith("#123456", undefined);
    // The previously selected palette swatch gives up its selection.
    expect(screen.getByRole("button", { name: "red" })).toHaveAttribute("aria-pressed", "false");
  });

  it("rejects a value that is not a valid HTML colour", async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();
    render(<Wrapper allowCustomColor onChange={onChange} />);
    await openPicker(user);

    await user.type(screen.getByLabelText("Custom colour"), "nonsense!");
    expect(onChange).not.toHaveBeenCalled();
    expect(screen.getByRole("dialog")).toHaveTextContent("Not a valid HTML colour.");
  });

  it("seeds the field when reopening on a colour no palette offers", async () => {
    const user = userEvent.setup();
    render(<Wrapper value="#123456" allowCustomColor />);
    await openPicker(user);

    expect(screen.getByLabelText("Custom colour")).toHaveValue("#123456");
  });

  it("leaves the field empty when reopening on a palette colour", async () => {
    const user = userEvent.setup();
    render(<Wrapper value="#ff0000" allowCustomColor />);
    await openPicker(user);

    expect(screen.getByLabelText("Custom colour")).toHaveValue("");
  });
});

// ─── Clearing ─────────────────────────────────────────────────────────────────

describe("clearing", () => {
  it("offers no clear control unless allowClear is set", async () => {
    const user = userEvent.setup();
    render(<Wrapper value="#ff0000" />);
    await openPicker(user);

    expect(screen.queryByRole("button", { name: "No colour" })).not.toBeInTheDocument();
  });

  it("clears the selection to null", async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();
    render(<Wrapper value="#ff0000" allowClear onChange={onChange} />);
    await openPicker(user);

    await user.click(screen.getByRole("button", { name: "No colour" }));
    expect(onChange).toHaveBeenCalledWith(null, undefined);
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
  });

  it("marks the clear control as the current choice when there is no colour", async () => {
    const user = userEvent.setup();
    render(<Wrapper value={null} allowClear />);
    await openPicker(user);

    expect(screen.getByRole("button", { name: "No colour" })).toHaveAttribute("aria-pressed", "true");
  });

  it("drops the custom text along with the selection", async () => {
    const user = userEvent.setup();
    render(<Wrapper value="#123456" allowClear allowCustomColor />);
    await openPicker(user);
    expect(screen.getByLabelText("Custom colour")).toHaveValue("#123456");

    await user.click(screen.getByRole("button", { name: "No colour" }));
    await openPicker(user);
    expect(screen.getByLabelText("Custom colour")).toHaveValue("");
  });
});

// ─── Tailwind defaults ────────────────────────────────────────────────────────

describe("TailwindColors", () => {
  it("publishes 11 shades per hue", () => {
    expect(TailwindColors.ShadeCount).toBe(11);
    expect(TailwindColors.Shades).toHaveLength(11);
  });

  it("covers the hues from red through slate", () => {
    const hues = [...new Set(TailwindColors.AllColors.map((c) => c.description.replace(/-\d+$/, "")))];
    expect(hues[0]).toBe("red");
    expect(hues[hues.length - 1]).toBe("slate");
    expect(hues).toHaveLength(18);
    expect(hues).not.toContain("stone");
  });

  it("holds every shade of every hue as hex", () => {
    expect(TailwindColors.AllColors).toHaveLength(18 * 11);
    for (const color of TailwindColors.AllColors) {
      expect(color.value).toMatch(/^#[0-9a-f]{6}$/);
    }
  });

  it("builds a single palette that a fresh call does not share", () => {
    const first = TailwindColors.defaultPalettes();
    const second = TailwindColors.defaultPalettes();

    expect(first).toHaveLength(1);
    expect(first[0].title).toBe("Tailwind");
    expect(first[0].colors).toHaveLength(18 * 11);
    expect(first).not.toBe(second);
    expect(first[0].colors).not.toBe(second[0].colors);
  });
});
