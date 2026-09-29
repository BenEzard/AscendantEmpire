import type { ReactNode } from "react";
import { act, renderHook, waitFor } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { getPlayerResources, type PlayerResourceMetaCategory } from "@/features/game/api/playerResourcesApi";
import { resourceToolbarButtonDomId, useResourcePanels } from "../useResourcePanels";

vi.mock("@/features/game/api/playerResourcesApi", () => ({
  getPlayerResources: vi.fn(),
}));

const GAME_ID = 1;
const PLAYER_ID = 7;

const getPlayerResourcesMock = vi.mocked(getPlayerResources);

function wrapper({ children }: { children: ReactNode }) {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>;
}

function aMetaCategory(code: string, categoryCode: string): PlayerResourceMetaCategory {
  return {
    id: code.length,
    code,
    displayName: code,
    iconPath: "",
    sortOrder: 1,
    categories: [
      {
        id: categoryCode.length,
        code: categoryCode,
        displayName: categoryCode,
        categoryType: "StandaloneResources",
        sortOrder: 1,
        groups: [],
        resources: [
          {
            id: 1,
            code: `${categoryCode}.One`,
            displayName: `${categoryCode} One`,
            description: null,
            iconPath: "",
            isGradedResource: false,
            resourceGrade: "None",
            sortOrder: 1,
            neverLuxury: false,
            obsoleteInRounds: null,
            amount: 0,
            amountAddedPerTurn: 0,
            hasStrategicStockpile: false,
            isImported: false,
            isExported: false,
            demandNotMet: false,
            isLuxury: false,
          },
        ],
      },
    ],
  };
}

const TREE = [aMetaCategory("Alpha", "AlphaCategory"), aMetaCategory("Beta", "BetaCategory")];

function renderPanels(gameId: number | undefined = GAME_ID, playerId: number | undefined = PLAYER_ID, enabled = true) {
  return renderHook(() => useResourcePanels({ gameId, playerId, enabled }), { wrapper });
}

/**
 * RS-12: the toolbar holds one button per meta-category and only one panel is open at a time, so what is open — and what that panel shows — is the
 * behaviour worth pinning. The tree itself is never asked for before a game is open.
 */
describe("useResourcePanels", () => {
  beforeEach(() => {
    getPlayerResourcesMock.mockReset();
    getPlayerResourcesMock.mockResolvedValue(TREE);
  });

  it("does not ask the server for a tree before a game is open", () => {
    renderPanels(undefined, undefined, false);

    expect(getPlayerResourcesMock).not.toHaveBeenCalled();
  });

  it("opens a meta-category's panel and shows only that meta-category's categories", async () => {
    const { result } = renderPanels();
    await waitFor(() => expect(result.current.metaCategories).toHaveLength(2));

    act(() => result.current.toggleResourcePanel("Beta"));

    expect(result.current.openMetaCategoryCode).toBe("Beta");
    expect(result.current.openCategories.map((category) => category.code)).toEqual(["BetaCategory"]);
    expect(result.current.allOpenPanelTickers.map((ticker) => ticker.code)).toEqual(["BetaCategory.One"]);
  });

  it("closes the panel when its own button is toggled again", async () => {
    const { result } = renderPanels();
    await waitFor(() => expect(result.current.metaCategories).toHaveLength(2));

    act(() => result.current.toggleResourcePanel("Alpha"));
    act(() => result.current.toggleResourcePanel("Alpha"));

    expect(result.current.openMetaCategoryCode).toBeNull();
    expect(result.current.openCategories).toEqual([]);
  });

  it("leaves only the newly clicked panel open when another one already is", async () => {
    const { result } = renderPanels();
    await waitFor(() => expect(result.current.metaCategories).toHaveLength(2));

    act(() => result.current.toggleResourcePanel("Alpha"));
    act(() => result.current.toggleResourcePanel("Beta"));

    expect(result.current.openMetaCategoryCode).toBe("Beta");
  });

  it("closes the open panel on a pointer press outside it, but not on its own button", async () => {
    // Stand-ins for the two toolbar buttons: the handler only reads the event target, and looks the open one up by the id the toolbar gives it.
    const alphaButton = document.createElement("button");
    alphaButton.id = resourceToolbarButtonDomId("Alpha");
    const elsewhere = document.createElement("div");
    document.body.append(alphaButton, elsewhere);

    const { result } = renderPanels();
    await waitFor(() => expect(result.current.metaCategories).toHaveLength(2));
    act(() => result.current.toggleResourcePanel("Alpha"));

    // MouseEvent rather than PointerEvent, which jsdom does not construct.
    act(() => {
      alphaButton.dispatchEvent(new MouseEvent("pointerdown", { bubbles: true }));
    });
    expect(result.current.openMetaCategoryCode).toBe("Alpha");

    act(() => {
      elsewhere.dispatchEvent(new MouseEvent("pointerdown", { bubbles: true }));
    });
    expect(result.current.openMetaCategoryCode).toBeNull();

    alphaButton.remove();
    elsewhere.remove();
  });
});

describe("resourceToolbarButtonDomId", () => {
  it("slugs a PascalCase meta-category code into a usable DOM id", () => {
    expect(resourceToolbarButtonDomId("ConstructionMaterials")).toBe("toolbar-btn-resource-constructionmaterials");
  });
});
