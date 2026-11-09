// Behaviour tests for FreeTextComboBox: what opens and closes the suggestion list, what a pick raises, and that
// free text is still the value whether or not it is on the list.
//
// Modelled on ImagePicker.test.tsx, the other picker-shaped field in this folder.

import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { useState } from "react";
import FreeTextComboBox, { type FreeTextComboBoxProps } from "../FreeTextComboBox";

const suggestions = ["King", "High King", "Emperor", "Grand Duke"];

// Controlled wrapper so onChange round-trips back through value, as a real caller would wire it.
function Wrapper(props: Partial<FreeTextComboBoxProps>) {
  const [value, setValue] = useState<string>(props.value ?? "");

  return (
    <FreeTextComboBox
      suggestions={suggestions}
      ariaLabel="Ruler Title"
      {...props}
      value={value}
      onChange={(next) => {
        setValue(next);
        props.onChange?.(next);
      }}
    />
  );
}

const field = () => screen.getByRole("combobox", { name: "Ruler Title" });
const option = (name: string) => screen.getByRole("option", { name });
const list = () => screen.queryByRole("listbox");

describe("opening and closing", () => {
  it("shows no list until the field is focused", () => {
    render(<Wrapper />);

    expect(list()).not.toBeInTheDocument();
    expect(field()).toHaveAttribute("aria-expanded", "false");
  });

  it("opens on focus with every suggestion offered", async () => {
    const user = userEvent.setup();
    render(<Wrapper />);

    await user.click(field());

    expect(list()).toBeInTheDocument();
    expect(screen.getAllByRole("option")).toHaveLength(suggestions.length);
  });

  it("closes on Escape", async () => {
    const user = userEvent.setup();
    render(<Wrapper />);

    await user.click(field());
    await user.keyboard("{Escape}");

    expect(list()).not.toBeInTheDocument();
  });

  it("filters on a substring, so a part-typed title still finds the longer ones", async () => {
    const user = userEvent.setup();
    render(<Wrapper />);

    await user.type(field(), "Ki");

    expect(option("King")).toBeInTheDocument();
    expect(option("High King")).toBeInTheDocument();
    expect(screen.queryByRole("option", { name: "Emperor" })).not.toBeInTheDocument();
  });
});

describe("picking a suggestion", () => {
  it("raises the picked suggestion as the value", async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();
    render(<Wrapper onChange={onChange} />);

    await user.type(field(), "Ki");
    await user.click(option("King"));

    expect(onChange).toHaveBeenLastCalledWith("King");
    expect(field()).toHaveValue("King");
  });

  // MM-7: the pick handed focus back to the field, whose focus handler reopened the list the pick had just closed,
  // leaving the dropdown covering the answer the user had settled on.
  it("leaves the list closed, rather than reopening it as focus returns to the field", async () => {
    const user = userEvent.setup();
    render(<Wrapper />);

    await user.type(field(), "Ki");
    await user.click(option("King"));

    expect(list()).not.toBeInTheDocument();
    expect(field()).toHaveAttribute("aria-expanded", "false");
  });

  it("leaves the list closed when the suggestion is picked by keyboard", async () => {
    const user = userEvent.setup();
    render(<Wrapper />);

    await user.type(field(), "Ki");
    await user.keyboard("{ArrowDown}{Enter}");

    expect(field()).toHaveValue("King");
    expect(list()).not.toBeInTheDocument();
  });

  it("keeps focus on the field, so typing carries on where the pick left off", async () => {
    const user = userEvent.setup();
    render(<Wrapper />);

    await user.type(field(), "Ki");
    await user.click(option("King"));

    expect(field()).toHaveFocus();
  });

  it("reopens on the next click in the field, so a longer title is still reachable", async () => {
    const user = userEvent.setup();
    render(<Wrapper />);

    await user.type(field(), "Ki");
    await user.click(option("King"));
    await user.click(field());

    expect(option("High King")).toBeInTheDocument();
  });
});

describe("free text", () => {
  it("takes a value that is on no list, and offers nothing for it", async () => {
    const user = userEvent.setup();
    render(<Wrapper />);

    await user.type(field(), "Warlord");

    expect(field()).toHaveValue("Warlord");
    expect(list()).not.toBeInTheDocument();
  });
});
