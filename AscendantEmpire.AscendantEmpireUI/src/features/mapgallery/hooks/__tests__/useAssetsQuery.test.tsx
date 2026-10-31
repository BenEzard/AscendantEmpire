import type { ReactNode } from "react";
import { renderHook, waitFor } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import type { AssetSummary } from "@/types/map/MapResource";
import { getAssets } from "../../api/mapResourcesApi";
import { useAssetsQuery } from "../useMapResourceCatalogue";

// Only getAssets is exercised here, but the hook module imports the whole api surface, so every export it names is stubbed.
vi.mock("../../api/mapResourcesApi", () => ({
  getAssets: vi.fn(),
  getMapResourceCatalogue: vi.fn(),
  putMapResourceCatalogue: vi.fn(),
  deleteMapResourceType: vi.fn(),
  deleteMapResourceCategory: vi.fn(),
}));

const getAssetsMock = vi.mocked(getAssets);

function wrapper({ children }: { children: ReactNode }) {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>;
}

function anAsset(id: number, displayName: string, isNaturallyOccurring: boolean): AssetSummary {
  return { id, code: displayName, displayName, metaCategoryDisplayName: "Construction Materials", isNaturallyOccurring };
}

describe("useAssetsQuery", () => {
  it("keeps only the naturally occurring Assets, so no picker offers something a tile could not give up", async () => {
    getAssetsMock.mockResolvedValue([anAsset(1, "Oak Log", true), anAsset(2, "Oak Plank", false), anAsset(3, "Limestone", true)]);

    const { result } = renderHook(() => useAssetsQuery(), { wrapper });

    await waitFor(() => expect(result.current.data).toBeDefined());

    expect(result.current.data?.map((asset) => asset.displayName)).toEqual(["Oak Log", "Limestone"]);
  });

  it("returns nothing when the catalogue holds no naturally occurring Asset, which is what drives the pickers' empty hint", async () => {
    getAssetsMock.mockResolvedValue([anAsset(1, "Oak Plank", false), anAsset(2, "Sandstone Block", false)]);

    const { result } = renderHook(() => useAssetsQuery(), { wrapper });

    await waitFor(() => expect(result.current.data).toBeDefined());

    expect(result.current.data).toEqual([]);
  });
});
