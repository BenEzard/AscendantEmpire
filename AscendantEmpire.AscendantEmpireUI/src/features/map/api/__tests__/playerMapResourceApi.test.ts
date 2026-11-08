import { getPlayerMapResources } from "../playerMapResourceApi";
import { http } from "@/api/http";

vi.mock("@/api/http", () => ({ http: { get: vi.fn() } }));

const get = vi.mocked(http.get);

describe("getPlayerMapResources", () => {
  it("reads the whole map's resources from the player endpoint", async () => {
    get.mockResolvedValue({ data: { resources: [] } });

    await getPlayerMapResources();

    expect(get).toHaveBeenCalledWith("/campaign-map/player/map-resources");
  });

  it("reads an entry's fields off the wire", async () => {
    get.mockResolvedValue({
      data: {
        resources: [
          {
            x: 3,
            y: 4,
            slot: 2,
            mapResourceTypeId: 7,
            code: "BERRY",
            displayName: "Berries",
            iconPath: "/resources/food/berries_40px.png",
            isObserved: true,
            estimatedQuantity: 1250.5,
          },
        ],
      },
    });

    await expect(getPlayerMapResources()).resolves.toEqual([
      {
        x: 3,
        y: 4,
        slot: 2,
        mapResourceTypeId: 7,
        code: "BERRY",
        displayName: "Berries",
        iconPath: "/resources/food/berries_40px.png",
        isObserved: true,
        estimatedQuantity: 1250.5,
      },
    ]);
  });

  /** An unwatched tile's resource arrives with no quantity, which has to stay null: a 0 in its place would read as a patch stripped bare. */
  it("keeps a missing quantity as null", async () => {
    get.mockResolvedValue({ data: { resources: [{ x: 1, y: 1, slot: 1, isObserved: false }] } });

    const resources = await getPlayerMapResources();

    expect(resources[0].estimatedQuantity).toBeNull();
    expect(resources[0].iconPath).toBeNull();
  });

  it("reads a response with no resources as none", async () => {
    get.mockResolvedValue({ data: {} });

    await expect(getPlayerMapResources()).resolves.toEqual([]);
  });
});
