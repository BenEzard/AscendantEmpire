import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";

import CapacityBar from "../CapacityBar";
import type { Color } from "../colorpicker/colorTypes";

// The arithmetic is pinned in capacityBarLayout.test.ts. These cover what only rendering can answer: the
// track the bands are sized on, that a band paints its colour and its tooltip is reachable, and the text
// name the bar needs because the tooltips are hover-only.

const BLUE: Color = { value: "#0d6efd", description: "blue-500" };
const GREY: Color = { value: "#6c757d", description: "grey-500" };

const BUILD_TIME_SEGMENTS = [
  { unitWidth: 80, tooltip: "Construction (80)", fillColor: BLUE },
  { unitWidth: 20, tooltip: "Shipping (20)", fillColor: GREY },
];

/** The painted part of each band, in draw order. Inside the Tooltip, which is where a pointer lands. */
function bandsOf(bar: HTMLElement): HTMLElement[] {
  return [...bar.querySelectorAll<HTMLElement>('[data-testid="capacity-band"]')];
}

describe("sizing", () => {
  it("fills its container when no pixel width is given, so a bar in a dialog spans the content area", () => {
    render(<CapacityBar segments={BUILD_TIME_SEGMENTS} defaultFillColor={GREY} />);

    expect(screen.getByRole("img")).toHaveStyle({ width: "100%", height: "10px" });
  });

  it("takes a pixel width when given one, for a bar that has to line up with a column", () => {
    render(<CapacityBar segments={BUILD_TIME_SEGMENTS} defaultFillColor={GREY} pxWidth={120} pxHeight={16} />);

    expect(screen.getByRole("img")).toHaveStyle({ width: "120px", height: "16px" });
  });

  it("passes a caller's className through, so a bar can be spaced by where it sits", () => {
    render(<CapacityBar segments={BUILD_TIME_SEGMENTS} defaultFillColor={GREY} className="mt-2" />);

    expect(screen.getByRole("img")).toHaveClass("mt-2");
  });
});

describe("the track", () => {
  // Grid rather than a flex row, for the reason ResearchProgressBar already documents: Tooltip wraps its
  // child in an inline-flex span, and sizing that span inside a flex row leaves the fill at the mercy of
  // inline layout. A grid track sizes whatever item lands in it.
  it("sizes the bands as grid tracks, which is what gives a band its share of the bar", () => {
    render(<CapacityBar segments={BUILD_TIME_SEGMENTS} defaultFillColor={GREY} />);

    expect(screen.getByRole("img").style.gridTemplateColumns).toBe("80fr 20fr");
  });

  it("gives the remainder a track of its own, so the space left is as wide as it is empty", () => {
    render(
      <CapacityBar
        segments={[{ unitWidth: 40, tooltip: "Carrying 40kg", fillColor: BLUE }]}
        defaultFillColor={GREY}
        totalUnits={100}
        remainderTooltip="60kg free"
      />,
    );

    expect(screen.getByRole("img").style.gridTemplateColumns).toBe("40fr 60fr");
  });
});

describe("the bands", () => {
  it("paints each band in its own colour, which is the whole of what the bar shows", () => {
    render(<CapacityBar segments={BUILD_TIME_SEGMENTS} defaultFillColor={GREY} />);

    const [construction, shipping] = bandsOf(screen.getByRole("img"));

    expect(construction).toHaveStyle({ backgroundColor: BLUE.value });
    expect(shipping).toHaveStyle({ backgroundColor: GREY.value });
  });

  it("shows a segment's tooltip on hover, which is where the figures live", async () => {
    render(<CapacityBar segments={BUILD_TIME_SEGMENTS} defaultFillColor={GREY} />);

    await userEvent.hover(bandsOf(screen.getByRole("img"))[0]);

    expect(await screen.findByRole("tooltip")).toHaveTextContent("Construction (80)");
  });

  it("renders no tooltip for a remainder the caller left unlabelled", async () => {
    render(<CapacityBar segments={[{ unitWidth: 40, tooltip: "Carrying 40kg" }]} defaultFillColor={GREY} totalUnits={100} />);

    await userEvent.hover(bandsOf(screen.getByRole("img"))[1]);

    expect(screen.queryByRole("tooltip")).not.toBeInTheDocument();
  });
});

describe("accessibility", () => {
  it("names the whole breakdown in text, because the tooltips are hover-only and carry the only labels", () => {
    render(<CapacityBar segments={BUILD_TIME_SEGMENTS} defaultFillColor={GREY} />);

    expect(screen.getByRole("img")).toHaveAccessibleName("Construction (80), Shipping (20)");
  });

  it("still has a name when nothing is in the bar yet", () => {
    render(<CapacityBar segments={[]} defaultFillColor={GREY} totalUnits={100} remainderTooltip="Empty" />);

    expect(screen.getByRole("img")).toHaveAccessibleName("Empty");
  });

  it("is decorative rather than an unnamed image when no band carries a label", () => {
    render(<CapacityBar segments={[]} defaultFillColor={GREY} totalUnits={100} />);

    expect(screen.queryByRole("img")).not.toBeInTheDocument();
  });

  it("draws an empty track, not a band, when there is neither a total nor anything in it", () => {
    const { container } = render(<CapacityBar segments={[]} defaultFillColor={GREY} />);

    expect(container.querySelectorAll('[data-testid="capacity-band"]')).toHaveLength(0);
    expect(screen.queryByRole("img")).not.toBeInTheDocument();
  });
});
