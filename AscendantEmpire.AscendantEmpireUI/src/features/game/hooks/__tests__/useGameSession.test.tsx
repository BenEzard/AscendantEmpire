import type { ReactNode } from "react";
import { act, renderHook } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { setAuth } from "@/api/authToken";
import { useGameSession } from "../useGameSession";
import type { OpenGameSelection } from "../../types/OpenGameSelection";

const GAME_ID = 1;
const THIS_PLAYER_ID = 7;
const OTHER_PLAYER_ID = 8;

function wrapper({ children }: { children: ReactNode }) {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>;
}

function selectionFor(playerId: number): OpenGameSelection {
  return {
    gameId: GAME_ID,
    gameName: "Test Game",
    width: 20,
    height: 20,
    playerId,
    rulerLabel: `Ruler ${playerId}`,
    campaignOffsetId: 3,
  };
}

/** Puts a game token in place — logged in, and in this game as this player. */
function holdGameToken(playerId: number): void {
  setAuth({ token: "game-token", refreshToken: "refresh", username: "ben", gameId: GAME_ID, playerId, permissions: [] });
}

/**
 * The session's selection is the game UI's answer to "who am I", so the only player it may ever name is the one the game
 * token names. GM-10: the open-game listing is a public lobby of every ruler in the game, and opening a game from a row
 * used to adopt that row's player — so picking the wrong row played someone else's units.
 */
describe("useGameSession", () => {
  beforeEach(() => {
    localStorage.clear();
  });

  it("opens a game whose player matches the game token", () => {
    holdGameToken(THIS_PLAYER_ID);
    const { result } = renderHook(() => useGameSession(), { wrapper });

    act(() => result.current.setGameSelection(selectionFor(THIS_PLAYER_ID)));

    expect(result.current.playerId).toBe(THIS_PLAYER_ID);
    expect(result.current.gameId).toBe(GAME_ID);
  });

  it("refuses a selection naming a player the token doesn't, rather than playing as them", () => {
    const consoleError = vi.spyOn(console, "error").mockImplementation(() => {});
    holdGameToken(THIS_PLAYER_ID);
    const { result } = renderHook(() => useGameSession(), { wrapper });

    act(() => result.current.setGameSelection(selectionFor(OTHER_PLAYER_ID)));

    expect(result.current.gameSelection).toBeNull();
    expect(consoleError).toHaveBeenCalled();
    consoleError.mockRestore();
  });

  it("does not persist a refused selection, so it can't be restored on the next load", () => {
    const consoleError = vi.spyOn(console, "error").mockImplementation(() => {});
    holdGameToken(THIS_PLAYER_ID);
    const { result } = renderHook(() => useGameSession(), { wrapper });

    act(() => result.current.setGameSelection(selectionFor(OTHER_PLAYER_ID)));
    consoleError.mockRestore();

    const { result: reloaded } = renderHook(() => useGameSession(), { wrapper });

    expect(reloaded.current.gameSelection).toBeNull();
  });

  it("rehydrates the open game on a fresh mount when the token still matches", () => {
    holdGameToken(THIS_PLAYER_ID);
    const { result } = renderHook(() => useGameSession(), { wrapper });
    act(() => result.current.setGameSelection(selectionFor(THIS_PLAYER_ID)));

    const { result: reloaded } = renderHook(() => useGameSession(), { wrapper });

    expect(reloaded.current.playerId).toBe(THIS_PLAYER_ID);
  });

  it("does not rehydrate a game left open by a different player", () => {
    // The previous user played and left their selection behind; a different player now holds the token.
    holdGameToken(OTHER_PLAYER_ID);
    const { result } = renderHook(() => useGameSession(), { wrapper });
    act(() => result.current.setGameSelection(selectionFor(OTHER_PLAYER_ID)));

    holdGameToken(THIS_PLAYER_ID);
    const { result: reloaded } = renderHook(() => useGameSession(), { wrapper });

    expect(reloaded.current.gameSelection).toBeNull();
  });

  it("clears the selection when the game is closed", () => {
    holdGameToken(THIS_PLAYER_ID);
    const { result } = renderHook(() => useGameSession(), { wrapper });
    act(() => result.current.setGameSelection(selectionFor(THIS_PLAYER_ID)));

    act(() => result.current.closeGame());

    expect(result.current.gameSelection).toBeNull();
    expect(result.current.activeDialog).toBeNull();
  });
});
