// Behaviour tests for ImagePicker: what opens and closes the grid, what a tile click raises, how the chosen tile
// is marked, and what a tile shows when its image will not load.
//
// Modelled on ColorPicker.test.tsx, which this component is shaped after.

import { fireEvent, render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { useState } from "react";
import ImagePicker, { type ImagePickerItem, type ImagePickerProps } from "../ImagePicker";

const items: ImagePickerItem[] = [
  { value: "/portraits/ruler_01.png", label: "Portrait 1" },
  { value: "/portraits/ruler_02.png", label: "Portrait 2" },
  { value: "/portraits/ruler_03.png", label: "Portrait 3" },
];

// Controlled wrapper so onChange round-trips back through value, as a real caller would wire it.
function Wrapper(props: Partial<ImagePickerProps>) {
  const [value, setValue] = useState<string | null>(props.value ?? null);

  return (
    <ImagePicker
      items={items}
      columns={2}
      title="Ruler portrait"
      {...props}
      value={value}
      onChange={(next) => {
        setValue(next);
        props.onChange?.(next);
      }}
    />
  );
}

const trigger = () => screen.getByRole("button", { name: "Ruler portrait" });
const tile = (label: string) => screen.getByRole("button", { name: label });

/** Opens the grid by clicking the trigger thumbnail. */
async function openPicker(user: ReturnType<typeof userEvent.setup>) {
  await user.click(trigger());

  return screen.getByRole("dialog", { name: "Ruler portrait" });
}

describe("opening and closing", () => {
  it("shows no grid until the trigger is clicked", () => {
    render(<Wrapper />);

    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
  });

  it("opens the grid on the trigger, with one tile per item", async () => {
    const user = userEvent.setup();
    render(<Wrapper />);

    await openPicker(user);

    expect(tile("Portrait 1")).toBeInTheDocument();
    expect(tile("Portrait 2")).toBeInTheDocument();
    expect(tile("Portrait 3")).toBeInTheDocument();
  });

  it("closes on Escape, so the grid never traps a keyboard user", async () => {
    const user = userEvent.setup();
    render(<Wrapper />);
    await openPicker(user);

    await user.keyboard("{Escape}");

    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
  });

  it("closes on a pointer press outside itself", async () => {
    const user = userEvent.setup();
    render(
      <>
        <Wrapper />
        <button type="button">Elsewhere</button>
      </>,
    );
    await openPicker(user);

    await user.click(screen.getByRole("button", { name: "Elsewhere" }));

    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
  });
});

describe("choosing", () => {
  it("raises the chosen item's value and closes", async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();
    render(<Wrapper onChange={onChange} />);
    await openPicker(user);

    await user.click(tile("Portrait 2"));

    expect(onChange).toHaveBeenCalledExactlyOnceWith("/portraits/ruler_02.png");
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
  });

  it("marks only the chosen tile as pressed, so the grid states what is selected", async () => {
    const user = userEvent.setup();
    render(<Wrapper value="/portraits/ruler_03.png" />);
    await openPicker(user);

    expect(tile("Portrait 3")).toHaveAttribute("aria-pressed", "true");
    expect(tile("Portrait 1")).toHaveAttribute("aria-pressed", "false");
  });

  it("shows the chosen image on the closed trigger", async () => {
    render(<Wrapper value="/portraits/ruler_02.png" />);

    expect(trigger().querySelector("img")).toHaveAttribute("src", "/portraits/ruler_02.png");
  });

  // An empty value is how a stored-but-unset path arrives, so it has to read as unchosen rather than as a
  // value to look up and fail to find.
  it.each([
    ["null", null],
    ["empty", ""],
  ])("shows no image on the trigger when the value is %s", async (_name, value) => {
    render(<Wrapper value={value} />);

    expect(trigger().querySelector("img")).toBeNull();
  });
});

// The popover measures itself before a single image has loaded, to decide whether to open below the trigger or
// flip above it. Were the tiles left to size themselves, that reading would be near-zero and the grid would
// overflow the viewport once the images arrived, so the sizing is what these pin.
describe("sizing that does not wait on the images", () => {
  it("gives every tile a definite height from its aspect ratio", async () => {
    const user = userEvent.setup();
    render(<Wrapper thumbnailWidth={120} aspectRatio={2} />);
    await openPicker(user);

    expect(tile("Portrait 1")).toHaveStyle({ width: "120px", aspectRatio: "2" });
  });

  it("gives the trigger a definite height too, so its row does not jump when the image arrives", () => {
    render(<Wrapper triggerWidth={80} aspectRatio={2} />);

    expect(trigger()).toHaveStyle({ width: "80px", aspectRatio: "2" });
  });

  it("stretches each image to its box rather than letting the image set the height", async () => {
    const user = userEvent.setup();
    render(<Wrapper />);
    await openPicker(user);

    expect(tile("Portrait 1").querySelector("img")).toHaveClass("h-full");
  });
});

describe("values claimed elsewhere", () => {
  const used = [{ value: "/portraits/ruler_02.png", usedBy: "Faction 1" }];

  it("are disabled, so they cannot be chosen", async () => {
    const user = userEvent.setup();
    render(<Wrapper usedValues={used} />);
    await openPicker(user);

    expect(tile("Portrait 2")).toBeDisabled();
    expect(tile("Portrait 1")).toBeEnabled();
  });

  it("raise nothing and leave the grid open when clicked", async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();
    render(<Wrapper usedValues={used} onChange={onChange} />);
    await openPicker(user);

    await user.click(tile("Portrait 2"));

    expect(onChange).not.toHaveBeenCalled();
    expect(screen.getByRole("dialog", { name: "Ruler portrait" })).toBeInTheDocument();
  });

  // Whatever claimed the current value is presumably the very thing being edited, so striking it through would leave
  // the picker with no way back to its own selection.
  it("exempt the current selection", async () => {
    const user = userEvent.setup();
    render(<Wrapper value="/portraits/ruler_02.png" usedValues={used} />);
    await openPicker(user);

    expect(tile("Portrait 2")).toBeEnabled();
    expect(tile("Portrait 2")).toHaveAttribute("aria-pressed", "true");
  });

  it("are left alone when no used list is given at all", async () => {
    const user = userEvent.setup();
    render(<Wrapper />);
    await openPicker(user);

    expect(tile("Portrait 2")).toBeEnabled();
  });
});

describe("the empty placeholder", () => {
  it("is a question mark by default", () => {
    render(<Wrapper />);

    expect(trigger()).toHaveTextContent("?");
  });

  // For a field where "nothing chosen" means something worth drawing rather than reading as an unfinished box.
  it("is whatever the caller supplies instead", () => {
    render(<Wrapper emptyIcon={<span>crown</span>} />);

    expect(trigger()).toHaveTextContent("crown");
    expect(trigger()).not.toHaveTextContent("?");
  });

  it("gives way to the image once a value is chosen", () => {
    render(<Wrapper value="/portraits/ruler_01.png" emptyIcon={<span>crown</span>} />);

    expect(trigger()).not.toHaveTextContent("crown");
    expect(trigger().querySelector("img")).toHaveAttribute("src", "/portraits/ruler_01.png");
  });
});

describe("an image that will not load", () => {
  it("falls back to the item's label rather than a broken image", async () => {
    const user = userEvent.setup();
    render(<Wrapper />);
    await openPicker(user);

    const image = tile("Portrait 1").querySelector("img");
    expect(image).not.toBeNull();

    // jsdom never actually loads an image, so the failure is raised by hand - which is exactly what a file
    // that is not there does in a browser.
    fireEvent.error(image!);

    expect(tile("Portrait 1").querySelector("img")).toBeNull();
    expect(tile("Portrait 1")).toHaveTextContent("Portrait 1");
  });
});
