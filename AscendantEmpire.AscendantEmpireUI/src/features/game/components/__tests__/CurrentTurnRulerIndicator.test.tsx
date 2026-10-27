// What the toolbar's turn indicator shows in each of the three states it can be in: a turn with a portrait, no
// open turn at all, and a slot from before the portrait column existed.

import { fireEvent, render, screen } from "@testing-library/react";
import CurrentTurnRulerIndicator from "../CurrentTurnRulerIndicator";

const RULER_LABEL = "Empress Aelara (The Ember Pact)";

describe("a turn that is open", () => {
  it("shows the ruler's portrait and label", () => {
    render(<CurrentTurnRulerIndicator portraitPath="/portraits/07_northern_queen.png" rulerLabel={RULER_LABEL} />);

    expect(screen.getByText(RULER_LABEL)).toBeInTheDocument();
    expect(document.querySelector("img")).toHaveAttribute("src", "/portraits/07_northern_queen.png");
  });

  it("names the whole indicator as a sentence, so the portrait needs no alt text of its own", () => {
    render(<CurrentTurnRulerIndicator portraitPath="/portraits/07_northern_queen.png" rulerLabel={RULER_LABEL} />);

    expect(screen.getByLabelText(`It is ${RULER_LABEL}'s turn`)).toBeInTheDocument();
    expect(document.querySelector("img")).toHaveAttribute("alt", "");
  });
});

describe("no open turn", () => {
  // `GET /turn/campaign` 404s between turns, so this is an ordinary state rather than an error. It says so in
  // words because a region of the toolbar that silently disappears reads as a bug.
  it("says so rather than rendering nothing", () => {
    render(<CurrentTurnRulerIndicator portraitPath={null} rulerLabel={null} />);

    expect(screen.getByText("No open turn")).toBeInTheDocument();
    expect(document.querySelector("img")).toBeNull();
  });
});

describe("a portrait that cannot be shown", () => {
  // The only way an empty path reaches here is a Player row predating the column, which backfilled to "".
  it("falls back to a glyph when the path is empty, keeping the label", () => {
    render(<CurrentTurnRulerIndicator portraitPath="" rulerLabel={RULER_LABEL} />);

    expect(document.querySelector("img")).toBeNull();
    expect(screen.getByText(RULER_LABEL)).toBeInTheDocument();
  });

  it("falls back to a glyph when the file will not load", () => {
    render(<CurrentTurnRulerIndicator portraitPath="/portraits/99_not_shipped.png" rulerLabel={RULER_LABEL} />);

    const image = document.querySelector("img");
    expect(image).not.toBeNull();

    // jsdom never loads an image, so the failure is raised by hand - what a missing file does in a browser.
    fireEvent.error(image!);

    expect(document.querySelector("img")).toBeNull();
    expect(screen.getByText(RULER_LABEL)).toBeInTheDocument();
  });
});
