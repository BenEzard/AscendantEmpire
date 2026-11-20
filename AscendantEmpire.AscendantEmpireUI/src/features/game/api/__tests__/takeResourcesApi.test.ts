import { getTakeResourcesOptions, takeResources } from "../takeResourcesApi";
import { http } from "@/api/http";

vi.mock("@/api/http", () => ({ http: { get: vi.fn(), post: vi.fn() } }));

const get = vi.mocked(http.get);
const post = vi.mocked(http.post);

describe("getTakeResourcesOptions", () => {
  /**
   * The repeated `id=` form is the whole reason the query is built by hand: axios's own array serialisation emits
   * `id[]=`, which the endpoint's [FromQuery(Name = "id")] will not bind, so the call would silently ask about no forces.
   */
  it("sends each force as its own id parameter", async () => {
    get.mockResolvedValue({ data: { resources: [] } });

    await getTakeResourcesOptions([4, 9]);

    expect(get).toHaveBeenCalledWith("/force-orders/actions/take-resources/options?id=4&id=9");
  });

  it("asks about a single force without an array suffix", async () => {
    get.mockResolvedValue({ data: { resources: [] } });

    await getTakeResourcesOptions([7]);

    expect(get).toHaveBeenCalledWith("/force-orders/actions/take-resources/options?id=7");
  });

  it("reads an offered resource's fields off the wire", async () => {
    get.mockResolvedValue({
      data: {
        resources: [
          {
            mapResourceInstanceId: 41,
            slot: 1,
            displayName: "Berry Patch",
            iconPath: "/map-resources/berries.png",
            unitsWouldTake: 1000,
            wouldGain: [{ assetId: 7, assetDisplayName: "Berries", amount: 1000 }],
          },
        ],
      },
    });

    const options = await getTakeResourcesOptions([5]);

    expect(options.resources[0]).toEqual({
      mapResourceInstanceId: 41,
      slot: 1,
      displayName: "Berry Patch",
      iconPath: "/map-resources/berries.png",
      unitsWouldTake: 1000,
      wouldGain: [{ assetId: 7, assetDisplayName: "Berries", amount: 1000 }],
    });
  });
});

describe("takeResources", () => {
  /** The game and player come from the game token, so the body carries only what the order itself is about. */
  it("posts the forces and the chosen resource", async () => {
    post.mockResolvedValue({ data: { displayName: "Berry Patch", xCoord: 1, yCoord: 1, unitsTaken: 0, isExhausted: false, gained: [], forces: [] } });

    await takeResources({ forceIds: [4, 9], mapResourceInstanceId: 41 });

    expect(post).toHaveBeenCalledWith("/force-orders/take-resources", { forceIds: [4, 9], mapResourceInstanceId: 41 });
  });

  it("returns what the take produced", async () => {
    post.mockResolvedValue({
      data: {
        displayName: "Berry Patch",
        xCoord: 3,
        yCoord: 4,
        unitsTaken: 1500,
        isExhausted: true,
        gained: [{ assetId: 7, assetDisplayName: "Berries", amount: 1500 }],
        forces: [
          { unitInstanceId: 4, displayName: "Settler", unitsTaken: 1000 },
          { unitInstanceId: 9, displayName: "Scout", unitsTaken: 500 },
        ],
      },
    });

    const result = await takeResources({ forceIds: [4, 9], mapResourceInstanceId: 41 });

    expect(result.unitsTaken).toBe(1500);
    expect(result.isExhausted).toBe(true);
    expect(result.forces).toHaveLength(2);
  });
});
