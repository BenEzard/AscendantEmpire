import type { ReactNode } from "react";
import { renderHook, waitFor } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { getCampaignPossibleActions, type CampaignPossibleActions } from "@/features/game/api/turnApi";
import { useCampaignPossibleActionsQuery } from "../useCampaignPossibleActions";

vi.mock("@/features/game/api/turnApi", () => ({
  getCampaignPossibleActions: vi.fn(),
}));

const GAME_ID = 1;
const PLAYER_ID = 7;

const getCampaignPossibleActionsMock = vi.mocked(getCampaignPossibleActions);

function wrapper({ children }: { children: ReactNode }) {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>;
}

function anAnswer(): CampaignPossibleActions {
  return {
    isYourTurn: true,
    possibleActions: [{ category: "Military", description: "Alpha has movement remaining.", xCoord: 3, yCoord: 4, id: 11, recordTypeId: 1 }],
  };
}

/**
 * G-148: the notifications panel is populated as the game opens, from a read-only route.
 *
 * The gate is the point of these tests. The only other way to get this list is the end-turn check, which *ends the turn* when it finds nothing outstanding —
 * so asking out of turn, or before a game is open, has to be something the client simply does not do.
 */
describe("useCampaignPossibleActionsQuery", () => {
  beforeEach(() => {
    getCampaignPossibleActionsMock.mockReset();
    getCampaignPossibleActionsMock.mockResolvedValue(anAnswer());
  });

  it("asks the server what is outstanding when a game is open and the caller says it is the player's turn", async () => {
    const { result } = renderHook(() => useCampaignPossibleActionsQuery(GAME_ID, PLAYER_ID, true), { wrapper });

    await waitFor(() => expect(result.current.data).toBeDefined());

    expect(result.current.data).toEqual(anAnswer());
    expect(getCampaignPossibleActionsMock).toHaveBeenCalledTimes(1);
  });

  it("asks nothing when the caller says it is not the player's turn", async () => {
    const { result } = renderHook(() => useCampaignPossibleActionsQuery(GAME_ID, PLAYER_ID, false), { wrapper });

    await waitFor(() => expect(result.current.isLoading).toBe(false));

    expect(result.current.data).toBeUndefined();
    expect(getCampaignPossibleActionsMock).not.toHaveBeenCalled();
  });

  it("asks nothing when no game is open, whatever the caller says about the turn", async () => {
    const { result } = renderHook(() => useCampaignPossibleActionsQuery(undefined, undefined, true), { wrapper });

    await waitFor(() => expect(result.current.isLoading).toBe(false));

    expect(result.current.data).toBeUndefined();
    expect(getCampaignPossibleActionsMock).not.toHaveBeenCalled();
  });
});
