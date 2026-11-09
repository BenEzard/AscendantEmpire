// Wiring tests for the lobby: that a game can be created from here, that claiming a seat leads straight
// into setting your ruler up, that there is a way back into that afterwards, and that backing out of it
// keeps the seat.
//
// The last two are the point of the Edit button. Cancelling the setup dialog deliberately leaves the
// slot carrying the placeholders the game seeded it with, which is only reasonable while there is a
// way back in — so "cancel keeps the slot" and "Edit reopens" are one guarantee tested from two ends.

import { render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";

import MatchMaker from "../MatchMaker";
// The vi.mock calls below are hoisted above these imports, so both bindings are already mocked.
import { claimPlayerSlot, getMatchMakingGames, getPlayerDefinition } from "../../api/matchMakingApi";
import type { MatchMakingGamesResponse } from "../../types/MatchMakingGamesResponse";
import type { MatchMakingSlot } from "../../types/MatchMakingSlot";

vi.mock("../../api/matchMakingApi", () => ({
  getMatchMakingGames: vi.fn(),
  claimPlayerSlot: vi.fn(),
  getPlayerDefinition: vi.fn(),
  updatePlayerDefinition: vi.fn(),
}));

vi.mock("@/features/auth/api/authApi", () => ({
  enterGame: vi.fn(),
}));

/** What the real dialog hands back on a successful create, so the toast is asserted on the message it would carry. */
const CREATED_MESSAGE = vi.hoisted(() => 'Game "Second Blood" created successfully.');

// Stands in for the New Game dialog: something to assert it opened, and a button that reports a created game. That is
// the whole of MatchMaker's side of the contract — what the form itself validates and sends is NewGameDialog.test's.
vi.mock("../NewGameDialog", () => ({
  default: ({ show, onClose, onCreateSuccess }: { show: boolean; onClose: () => void; onCreateSuccess: (message?: string) => void }) =>
    show ? (
      <div>
        <span>New Game dialog</span>
        <button
          onClick={() => {
            onCreateSuccess(CREATED_MESSAGE);
            onClose();
          }}>
          Create the game
        </button>
      </div>
    ) : null,
}));

// Nothing in the vitest config clears mocks between tests, and these assert on call counts.
beforeEach(() => {
  vi.clearAllMocks();
});

const GAME_ID = 7;
const FREE_PLAYER_ID = 72;
const OWNED_PLAYER_ID = 70;

function slot(overrides: Partial<MatchMakingSlot> & Pick<MatchMakingSlot, "playerId">): MatchMakingSlot {
  return {
    rulerTitle: "Nobody",
    rulerName: `Player ${overrides.playerId}`,
    factionName: `Faction ${overrides.playerId}`,
    factionColour: null,
    rulerLabel: `Nobody Player ${overrides.playerId} (Faction ${overrides.playerId})`,
    isClaimed: false,
    isClaimedByCurrentUser: false,
    // Defaults to defined, so a seat is "Taken" unless a test is specifically about one still setting up. An
    // unclaimed seat's value is never read, which is why this does not have to track isClaimed.
    isDefined: true,
    ...overrides,
  };
}

function lobby(slots: MatchMakingSlot[]): MatchMakingGamesResponse {
  // Not started: these lobbies are still filling, and nothing here goes near the Play button that flag gates.
  return {
    games: [{ gameId: GAME_ID, gameName: "First Blood", numberOfPlayers: slots.length, mapWidth: 64, mapHeight: 64, hasStarted: false, slots }],
  };
}

/** A lobby with one free seat and nothing of the caller's. */
const A_FREE_SEAT = lobby([slot({ playerId: FREE_PLAYER_ID })]);

/** A lobby where the caller already holds a seat, so the Edit affordance is on screen. */
const A_SEAT_OF_OURS = lobby([slot({ playerId: OWNED_PLAYER_ID, isClaimed: true, isClaimedByCurrentUser: true })]);

function renderLobby(games: MatchMakingGamesResponse) {
  vi.mocked(getMatchMakingGames).mockResolvedValue(games);
  // The dialog fetches this the moment it opens; every test here only cares that it did open.
  vi.mocked(getPlayerDefinition).mockResolvedValue({
    playerId: OWNED_PLAYER_ID,
    gameId: GAME_ID,
    rulerTitle: "Nobody",
    rulerName: "Player 70",
    rulerGender: null,
    factionName: "Faction 70",
    factionColour: null,
    peopleName: "People 70",
    demonym: "Person 70",
    cultureName: "Culture 70",
    language: "Language 70",
    rulerPortraitPath: "/portraits/10_roman_ruler.png",
    usedRulerPortraits: [],
    usedFactionColours: [],
    isLocked: false,
  });

  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } });

  render(
    <QueryClientProvider client={queryClient}>
      <MatchMaker />
    </QueryClientProvider>,
  );
}

const claimButton = () => screen.getByRole("button", { name: "Claim" });
const editButton = () => screen.getByRole("button", { name: "Edit" });

/** The setup dialog, identified by its title. */
const setupDialog = () => screen.queryByText("Define your Ruler and Faction");

/** The stubbed New Game dialog, named apart from the button that opens it. */
const newGameDialog = () => screen.queryByText("New Game dialog");

/** Waits for the lobby listing to have rendered. */
async function waitForLobby() {
  await waitFor(() => expect(screen.getByText("First Blood")).toBeInTheDocument());
}

describe("creating a game", () => {
  it("opens the New Game dialog from the lobby itself, creation having left the game screen's toolbar", async () => {
    const user = userEvent.setup();
    renderLobby(A_FREE_SEAT);
    await waitForLobby();

    expect(newGameDialog()).not.toBeInTheDocument();

    await user.click(screen.getByRole("button", { name: "New Game" }));

    await waitFor(() => expect(newGameDialog()).toBeInTheDocument());
  });

  it("brings the created game into the listing, so its seats are claimable without a manual refresh", async () => {
    const user = userEvent.setup();
    renderLobby(A_FREE_SEAT);
    await waitForLobby();
    expect(vi.mocked(getMatchMakingGames)).toHaveBeenCalledTimes(1);

    await user.click(screen.getByRole("button", { name: "New Game" }));
    await waitFor(() => expect(newGameDialog()).toBeInTheDocument());
    await user.click(screen.getByRole("button", { name: "Create the game" }));

    // The listing is what hands the new game's slots out, so a create that left it stale would hide them.
    await waitFor(() => expect(vi.mocked(getMatchMakingGames)).toHaveBeenCalledTimes(2));
    expect(screen.getByText(CREATED_MESSAGE)).toBeInTheDocument();
    expect(newGameDialog()).not.toBeInTheDocument();
  });
});

describe("claiming a seat", () => {
  it("opens the ruler setup dialog once the claim succeeds", async () => {
    const user = userEvent.setup();
    vi.mocked(claimPlayerSlot).mockResolvedValue(undefined);
    renderLobby(A_FREE_SEAT);
    await waitForLobby();

    expect(setupDialog()).not.toBeInTheDocument();

    await user.click(claimButton());

    await waitFor(() => expect(setupDialog()).toBeInTheDocument());
    expect(vi.mocked(claimPlayerSlot)).toHaveBeenCalledWith(GAME_ID, FREE_PLAYER_ID);
  });

  it("leaves the dialog shut when the claim is refused", async () => {
    const user = userEvent.setup();
    vi.mocked(claimPlayerSlot).mockRejectedValue(new Error("That player slot has already been claimed."));
    renderLobby(A_FREE_SEAT);
    await waitForLobby();

    await user.click(claimButton());

    await waitFor(() => expect(screen.getByText("That player slot has already been claimed.")).toBeInTheDocument());
    expect(setupDialog()).not.toBeInTheDocument();
  });
});

describe("a seat the caller already holds", () => {
  it("offers a way back into setup, so cancelling it is not a dead end", async () => {
    const user = userEvent.setup();
    renderLobby(A_SEAT_OF_OURS);
    await waitForLobby();

    await user.click(editButton());

    await waitFor(() => expect(setupDialog()).toBeInTheDocument());
  });

  it("keeps the seat when the dialog is cancelled", async () => {
    const user = userEvent.setup();
    renderLobby(A_SEAT_OF_OURS);
    await waitForLobby();

    await user.click(editButton());
    await waitFor(() => expect(setupDialog()).toBeInTheDocument());

    await user.click(screen.getByRole("button", { name: "Cancel / Close" }));

    await waitFor(() => expect(setupDialog()).not.toBeInTheDocument());
    // Still ours, and still reachable — the row is unchanged apart from the dialog having gone.
    expect(screen.getByText("Your slot")).toBeInTheDocument();
    expect(editButton()).toBeInTheDocument();
  });

  it("does not offer Claim on a seat that is already taken", async () => {
    renderLobby(lobby([slot({ playerId: 71, isClaimed: true })]));
    await waitForLobby();

    expect(screen.getByText("Taken")).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Claim" })).not.toBeInTheDocument();
  });
});

describe("a lobby waiting on setup rather than on seats", () => {
  it("badges a claimed seat whose player has not been through setup apart from a finished one", async () => {
    renderLobby(lobby([slot({ playerId: 71, isClaimed: true, isDefined: false })]));
    await waitForLobby();

    // "Taken" would read as nothing left to wait for, when this seat is exactly what the game is waiting on.
    expect(screen.getByText("Setting up")).toBeInTheDocument();
    expect(screen.queryByText("Taken")).not.toBeInTheDocument();
  });

  it("says how many players are still setting up, rather than only that the game has not started", async () => {
    renderLobby(
      lobby([
        slot({ playerId: 71, isClaimed: true, isDefined: false }),
        slot({ playerId: 72, isClaimed: true, isDefined: false }),
        slot({ playerId: 73, isClaimed: true, isDefined: true }),
      ]),
    );
    await waitForLobby();

    expect(screen.getByText(/2 still setting up/)).toBeInTheDocument();
    expect(screen.queryByText(/waiting to start/)).not.toBeInTheDocument();
  });

  it("falls back to waiting-to-start when the seats themselves are what is outstanding", async () => {
    renderLobby(lobby([slot({ playerId: 71, isClaimed: true }), slot({ playerId: FREE_PLAYER_ID })]));
    await waitForLobby();

    expect(screen.getByText(/waiting to start/)).toBeInTheDocument();
  });
});

describe("faction colours in the listing", () => {
  it("draws each seat's colour beside it, and marks the ones nobody has picked", async () => {
    renderLobby(
      lobby([
        slot({ playerId: OWNED_PLAYER_ID, factionColour: "#8200db", isClaimed: true, isClaimedByCurrentUser: true }),
        slot({ playerId: FREE_PLAYER_ID }),
      ]),
    );
    await waitForLobby();

    const rows = screen.getAllByRole("listitem");

    expect(within(rows[0]).getByTitle("Faction colour #8200db")).toBeInTheDocument();
    expect(within(rows[1]).getByTitle("No faction colour picked yet")).toBeInTheDocument();
  });
});
