import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";

import AvailableResearchTable from "../AvailableResearchTable";
import { cost, grant, researchable } from "./researchFixtures";

// Purely presentational — data in, one callback out. Every card the grid is given is researchable by definition, so nothing here is about
// filtering; the cases are about what the card can destroy by rendering it carelessly: a bucket the technology does not use appearing in the
// stacked bar, a free technology looking like one whose cost simply was not sent, and a grant whose target has been deleted being hidden.

function renderGrid(overrides: Partial<React.ComponentProps<typeof AvailableResearchTable>> = {}) {
  const onSelect = vi.fn();

  render(<AvailableResearchTable technologies={[researchable(1)]} onSelect={onSelect} {...overrides} />);

  return { onSelect };
}

/** One technology's bar segment for one bucket, or null when the technology does not use that bucket. */
function segment(technologyId: number, bucket: "directed" | "experience" | "diffusion"): HTMLElement | null {
  return screen.queryByTestId(`research-bar-segment-${technologyId}-${bucket}`);
}

/** The figure printed under one bucket's segment, or null when the technology does not use that bucket. */
function figure(technologyId: number, bucket: "directed" | "experience" | "diffusion"): HTMLElement | null {
  return screen.queryByTestId(`research-bucket-${technologyId}-${bucket}`);
}

// ─── The stacked bar ────────────────────────────────────────────────────────────

describe("a bucket the technology does not use", () => {
  it("has no segment in the bar and no figure under it", () => {
    // The sharpest point in the data model: an unused bucket has no requirement at all, rather than a requirement of zero. A flat green
    // stripe for "no experience needed" would say the opposite of what is true, so the segment is absent rather than empty.
    renderGrid({ technologies: [researchable(1, { directedRequired: 500, experienceRequired: null, diffusionRequired: null })] });

    expect(segment(1, "directed")).toBeInTheDocument();
    expect(segment(1, "experience")).not.toBeInTheDocument();
    expect(segment(1, "diffusion")).not.toBeInTheDocument();
    expect(figure(1, "experience")).not.toBeInTheDocument();
  });

  it("is distinguished from a bucket genuinely set to zero", () => {
    // Nothing in the seeded catalogue does this, but a zero is a real requirement the data model can express, and it must still draw.
    renderGrid({ technologies: [researchable(1, { experienceRequired: 0 })] });

    expect(segment(1, "experience")).toBeInTheDocument();
    expect(figure(1, "experience")).toHaveTextContent("0 / 0");
  });
});

describe("the bar", () => {
  it("sizes each segment by its share of the total requirement", () => {
    // 500 directed against 250 experience is two thirds blue, one third green — the bar says at a glance where most of the work is. Asserted
    // on the grid track rather than the segment, because the track is what sizes it; the segment just fills whatever track it lands in.
    renderGrid({ technologies: [researchable(1, { directedRequired: 500, experienceRequired: 250 })] });

    expect(screen.getByTestId("research-bar-track-1")).toHaveStyle({
      gridTemplateColumns: `${(500 / 750) * 100}fr ${(250 / 750) * 100}fr`,
    });
  });

  it("gives one used bucket the whole width", () => {
    renderGrid({ technologies: [researchable(1, { directedRequired: 100 })] });

    expect(screen.getByTestId("research-bar-track-1")).toHaveStyle({ gridTemplateColumns: "100fr" });
  });

  it("fills each segment in proportion to the points in that bucket", () => {
    renderGrid({ technologies: [researchable(1, { directedRequired: 400, directedPoints: 100 })] });

    // The fill is the segment's only child, and its width is a percentage of the segment rather than of the whole bar.
    expect(segment(1, "directed")?.firstElementChild).toHaveStyle({ width: "25%" });
  });

  it("shows progress against this player's own requirement, grouped for readability", () => {
    renderGrid({ technologies: [researchable(1, { directedRequired: 1500, directedPoints: 250 })] });

    expect(figure(1, "directed")).toHaveTextContent("250 / 1,500");
  });

  it("says so rather than drawing an empty bar when a technology uses no bucket at all", () => {
    // Guarded by CK_Technology_AtLeastOneBucket, so this is a broken row rather than a state to design for — but an empty bar would read as
    // "no research needed", which is the one thing it must not say.
    renderGrid({ technologies: [researchable(1, { directedRequired: null, experienceRequired: null, diffusionRequired: null })] });

    expect(screen.getByTestId("research-no-buckets-1")).toHaveTextContent("should not be possible");
  });
});

// ─── Cost ───────────────────────────────────────────────────────────────────────

describe("the cost to unlock", () => {
  it("says Free outright when there are no cost rows", () => {
    // Stated rather than left as an empty row, because "no acquisition cost" is what lets a technology be chosen at all right now — an empty
    // space would read as information the server failed to send.
    renderGrid({ technologies: [researchable(1, { acquisitionCosts: [] })] });

    expect(screen.getByTestId("research-card-1")).toHaveTextContent("Free");
  });

  it("shows one icon per Asset with its quantity beneath", () => {
    renderGrid({ technologies: [researchable(1, { acquisitionCosts: [cost(7, "Flint", 20), cost(8, "Hide", 5)] })] });

    const card = screen.getByTestId("research-card-1");

    expect(card).toHaveTextContent("20");
    expect(card).toHaveTextContent("5");
    expect(card).not.toHaveTextContent("Free");

    // The Asset is named through the icon rather than in text, so the accessible name is what carries it.
    expect(screen.getByLabelText("Flint")).toBeInTheDocument();
    expect(screen.getByLabelText("Hide")).toBeInTheDocument();
  });
});

// ─── Grants ─────────────────────────────────────────────────────────────────────

describe("what the technology grants", () => {
  it("says it is not recorded rather than leaving the row blank", () => {
    // The case every card hits today: the seeded catalogue authors no grants at all, carrying what a technology gives as prose in its
    // description instead.
    renderGrid({ technologies: [researchable(1, { grants: [] })] });

    expect(screen.getByTestId("research-card-1")).toHaveTextContent("Not yet recorded");
  });

  it("names each thing it unlocks", () => {
    renderGrid({ technologies: [researchable(1, { grants: [grant(3, "Flint Axe")] })] });

    expect(screen.getByTestId("research-card-1")).toHaveTextContent("Flint Axe");
  });

  it("marks a grant that retires something rather than unlocking it", () => {
    renderGrid({ technologies: [researchable(1, { grants: [grant(3, "Digging Stick", { grantEffect: "Obsolete" })] })] });

    expect(screen.getByTestId("research-card-1")).toHaveTextContent("Retires Digging Stick");
  });

  it("reports a grant whose target no longer exists rather than hiding it", () => {
    // A grant carries no foreign key, so it can outlive what it pointed at. The server reports the dangling target as nulls, and the card
    // shows it: a grant pointing at nothing is catalogue damage somebody should see.
    renderGrid({ technologies: [researchable(1, { grants: [grant(42, "", { targetCode: null, targetDisplayName: null })] })] });

    expect(screen.getByTestId("research-card-1")).toHaveTextContent("Asset 42 (no longer exists)");
  });
});

// ─── Selecting ──────────────────────────────────────────────────────────────────

describe("choosing a project", () => {
  it("reports the technology the player picked", async () => {
    const { onSelect } = renderGrid({ technologies: [researchable(1), researchable(2)] });

    await userEvent.click(screen.getByTestId("research-select-2"));

    expect(onSelect).toHaveBeenCalledExactlyOnceWith(2);
  });

  it("names the technology on the tick, since the tick carries no text of its own", () => {
    renderGrid({ technologies: [researchable(1, { displayName: "Fire-Making" })] });

    expect(screen.getByRole("button", { name: "Research Fire-Making" })).toBeInTheDocument();
  });

  it("marks the current project rather than offering it again", () => {
    // Choosing the current project a second time would be a no-op the player could only read as the button not working, so the card says
    // what it is instead of inviting the press.
    renderGrid({ technologies: [researchable(1, { isCurrentProject: true })] });

    expect(screen.getByTestId("research-current-1")).toHaveTextContent("Researching");
    expect(screen.queryByTestId("research-select-1")).not.toBeInTheDocument();
  });

  it("disables every tick while a selection is in flight, and marks only the one being chosen as busy", () => {
    // One request at a time: a second press mid-flight would race the first, and the player would have no way to tell which choice won.
    renderGrid({ technologies: [researchable(1), researchable(2)], selectingTechnologyId: 2 });

    expect(screen.getByTestId("research-select-1")).toBeDisabled();
    expect(screen.getByTestId("research-select-2")).toBeDisabled();
    expect(screen.getByTestId("research-select-2")).toHaveAttribute("aria-busy", "true");
    expect(screen.getByTestId("research-select-1")).toHaveAttribute("aria-busy", "false");
  });
});

// ─── Nothing researchable ───────────────────────────────────────────────────────

describe("with nothing researchable", () => {
  it("explains why rather than showing an empty grid", () => {
    // A real state rather than an impossible one: a player who has discovered everything their prerequisites reach has nothing left to pick,
    // so the grid has to say why instead of looking broken.
    renderGrid({ technologies: [] });

    expect(screen.getByTestId("available-research-empty")).toHaveTextContent("waiting on prerequisites");
    expect(screen.queryByTestId("available-research-grid")).not.toBeInTheDocument();
  });
});
