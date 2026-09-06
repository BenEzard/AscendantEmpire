import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";

import GamesTable from "../GamesTable";
import type { GameSummary } from "../../types/Game";

// Purely presentational: props in, onDelete out. The table never calls the API and never confirms anything —
// the parent owns both — so no network mocking is needed.

function makeGame(overrides: Partial<GameSummary> = {}): GameSummary {
  return {
    gameId: 1,
    gameName: "Test Game",
    numberOfPlayers: 4,
    claimedPlayerCount: 2,
    creationStatus: "SetupComplete",
    playStatus: "AwaitingPlayers",
    createdAt: "2026-01-01T00:00:00+00:00",
    ...overrides,
  };
}

function rowFor(gameName: string): HTMLElement {
  return screen.getByRole("row", { name: new RegExp(gameName) });
}

// ─── Rendering ────────────────────────────────────────────────────────────────

describe("rendering", () => {
  it("shows how full a game is as claimed out of total, so a half-empty lobby is obvious", () => {
    render(<GamesTable games={[makeGame({ claimedPlayerCount: 2, numberOfPlayers: 4 })]} loading={false} onDelete={vi.fn()} />);

    expect(within(rowFor("Test Game")).getByText("2 / 4")).toBeInTheDocument();
  });

  it("labels both statuses in words rather than showing the raw enum names", () => {
    render(
      <GamesTable
        games={[makeGame({ creationStatus: "SetupFailed", playStatus: "Abandoned" })]}
        loading={false}
        onDelete={vi.fn()}
      />,
    );

    const row = rowFor("Test Game");
    expect(within(row).getByText("Setup failed")).toBeInTheDocument();
    expect(within(row).getByText("Abandoned")).toBeInTheDocument();
  });

  it("says there are no games rather than showing an empty grid", () => {
    render(<GamesTable games={[]} loading={false} onDelete={vi.fn()} />);

    expect(screen.getByText("No games found.")).toBeInTheDocument();
  });

  it("shows a loading state on the first load, when there is nothing yet to display", () => {
    render(<GamesTable games={[]} loading onDelete={vi.fn()} />);

    expect(screen.getByText("Loading games…")).toBeInTheDocument();
  });
});

// ─── Deleting ─────────────────────────────────────────────────────────────────

describe("deleting", () => {
  it("hands the whole game to the caller, which needs its name to say what is about to be destroyed", async () => {
    const user = userEvent.setup();
    const onDelete = vi.fn();
    const game = makeGame({ gameId: 12, gameName: "Doomed" });

    render(<GamesTable games={[game]} loading={false} onDelete={onDelete} />);

    await user.click(within(rowFor("Doomed")).getByRole("button", { name: "Delete" }));

    expect(onDelete).toHaveBeenCalledWith(game);
  });

  it("disables the button on the game being deleted, so the purge cannot be started twice", () => {
    render(
      <GamesTable
        games={[makeGame({ gameId: 12, gameName: "Doomed" }), makeGame({ gameId: 13, gameName: "Bystander" })]}
        loading={false}
        deletingGameId={12}
        onDelete={vi.fn()}
      />,
    );

    expect(within(rowFor("Doomed")).getByRole("button", { name: "Deleting…" })).toBeDisabled();
    expect(within(rowFor("Bystander")).getByRole("button", { name: "Delete" })).toBeEnabled();
  });
});
