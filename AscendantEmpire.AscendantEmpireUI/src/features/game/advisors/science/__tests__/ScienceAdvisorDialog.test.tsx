import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";

import ScienceAdvisorDialog from "../ScienceAdvisorDialog";
import { overview, researchable } from "./researchFixtures";

// The dialog shell, driven through its injected research position. That prop is the seam that makes this testable at
// all — there is no network mocking in this project, so the self-loading path is not exercised here. What is, is that
// the header reads correctly, the card grid is wired up, and the closed state stays closed.
//
// Deliberately not tested: the selection mutation. It calls the real api module, which would reach for the network, so
// the grid has its own test for the callback, and the end-to-end path is checked against the running app.

function renderDialog(overrides: Partial<React.ComponentProps<typeof ScienceAdvisorDialog>> = {}) {
  const onClose = vi.fn();

  // The dialog calls usePlayerResearchQuery unconditionally — switched off rather than skipped when a position is
  // injected — and useMutation besides, so it still needs a provider.
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });

  render(
    <QueryClientProvider client={queryClient}>
      <ScienceAdvisorDialog show onClose={onClose} research={overview([researchable(1), researchable(2)])} {...overrides} />
    </QueryClientProvider>,
  );

  return { onClose };
}

describe("with a research position", () => {
  it("renders a card per researchable technology", () => {
    renderDialog();

    expect(screen.getByTestId("research-card-1")).toBeInTheDocument();
    expect(screen.getByTestId("research-card-2")).toBeInTheDocument();
  });

  it("carries the cards on a tab titled Research, counting them", () => {
    // PagedDialog prefixes every tab with its own number and renders the label, number and count as adjacent spans, so the accessible name
    // comes out as "1Research(2)" with no separators. Anchored on the number rather than matching it, since which tab index this is belongs
    // to PagedDialog, and anchored at all so this cannot accidentally match a card's own "Research <technology>" button.
    renderDialog();

    expect(screen.getByRole("button", { name: /^\d+Research\(2\)$/ })).toBeInTheDocument();
  });

  it("drops the count rather than showing a zero when nothing is researchable", () => {
    // "Research (0)" would advertise a page with nothing on it; the tab still opens, and the grid explains why it is empty.
    renderDialog({ research: overview([]) });

    expect(screen.getByRole("button", { name: /^\d+Research$/ })).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /^\d+Research\(/ })).not.toBeInTheDocument();
    expect(screen.getByTestId("available-research-empty")).toBeInTheDocument();
  });

  it("names the current project above the grid", () => {
    // Answerable without reading every card, which is the point of stating it as well as marking it.
    renderDialog({
      research: overview([researchable(1, { isCurrentProject: true })], { currentTechnologyId: 1, currentTechnologyDisplayName: "Fire-Making" }),
    });

    expect(screen.getByTestId("science-advisor")).toHaveTextContent("Researching: Fire-Making");
  });

  it("says nothing is selected rather than leaving the line blank", () => {
    // A null current project is a real state — the one the end-turn research prompt exists for — so it is spelled out.
    renderDialog({ research: overview([researchable(1)]) });

    expect(screen.getByTestId("science-advisor")).toHaveTextContent("Researching: Nothing selected");
  });

  it("warns that research does not accumulate yet", () => {
    // Said plainly rather than left to be inferred from a project that never advances: nothing generates research
    // points at the end of a turn, so a chosen project records intent and no more.
    renderDialog();

    expect(screen.getByTestId("science-advisor")).toHaveTextContent("Research does not accumulate yet");
  });

  it("shows the banked directed points and the Round they were counted in", () => {
    renderDialog({ research: overview([researchable(1)], { unspentDirectedPoints: 1250, currentRoundNumber: 7 }) });

    expect(screen.getByTestId("science-advisor")).toHaveTextContent("1,250");
    expect(screen.getByTestId("science-advisor")).toHaveTextContent("Round 7");
  });
});

describe("closing", () => {
  it("reports the close when the footer button is pressed", async () => {
    const { onClose } = renderDialog();

    await userEvent.click(screen.getByTestId("science-advisor-close"));

    expect(onClose).toHaveBeenCalledOnce();
  });

  it("renders nothing while hidden", () => {
    renderDialog({ show: false });

    expect(screen.queryByTestId("science-advisor")).not.toBeInTheDocument();
  });
});
