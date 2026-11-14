import { fireEvent, render } from "@testing-library/react";
import type { Geography } from "@/types/map/Geography";
import Tile from "../Tile";
import type { TileCell, TileMapResourceMarker } from "../TileTypes";

const TILE: TileCell = { x: 3, y: 4, biomeTypeId: "Grassland", geographyTypeId: "Flat", isExplored: true };
const GEOGRAPHY = { campaignMapGeographyType: "Flat", name: "Plains" } as Geography;

const berries = (overrides: Partial<TileMapResourceMarker> = {}): TileMapResourceMarker => ({
  slot: 1,
  name: "Berries",
  iconPath: "/resources/food/berries_40px.png",
  estimatedQuantity: 1250,
  ...overrides,
});

function renderTile(mapResources?: TileMapResourceMarker[], title?: string, tile: TileCell = TILE) {
  return render(<Tile tile={tile} sizePx={50} geography={GEOGRAPHY} mapResources={mapResources} title={title} />);
}

describe("Tile MapResources", () => {
  /** The same corner and the same styling hook the map editor's tile uses, so a berry patch looks the same in the editor and in the game. */
  it("draws a marker per resource in the tile's resource corner", () => {
    const { container } = renderTile([berries(), berries({ slot: 2, name: "Copper", iconPath: "/resources/ore/copper_40px.png" })]);

    const images = container.querySelectorAll(".tile-map-resources img");

    expect(Array.from(images).map((image) => image.getAttribute("src"))).toEqual(["/resources/food/berries_40px.png", "/resources/ore/copper_40px.png"]);
  });

  /** MapResourceIcon's fish is the map editor's stand-in. A type the biome already says must draw nothing at all to a player. */
  it("draws no marker for a resource whose type has no icon", () => {
    const { container } = renderTile([berries({ name: "Wood", iconPath: null })]);

    expect(container.querySelector(".tile-map-resources")).toBeNull();
  });

  it("draws only the resources that have an icon", () => {
    const { container } = renderTile([berries({ name: "Wood", iconPath: null }), berries({ slot: 2 })]);

    expect(container.querySelectorAll(".tile-map-resources img")).toHaveLength(1);
  });

  /** The resources are named after whatever title the grid gave the tile, rather than replacing it. */
  it("names the resources under the caller's title", () => {
    const { container } = renderTile([berries()], "(3, 4); Grassland, Flat");

    expect(container.querySelector(".tile")).toHaveAttribute("title", "(3, 4); Grassland, Flat\nMapResources: Berries (~1.2k)");
  });

  /** A no-icon resource is still the player's knowledge, so the title is where it is named. */
  it("names a resource with no icon in the title", () => {
    const { container } = renderTile([berries({ name: "Wood", iconPath: null })], "(3, 4); Forest, Flat");

    expect(container.querySelector(".tile")).toHaveAttribute("title", "(3, 4); Forest, Flat\nMapResources: Wood (~1.2k)");
  });

  /** No estimate means no number — a patch the player cannot see the size of is not one that has been stripped bare. */
  it("names a resource without an estimate when there is none", () => {
    const { container } = renderTile([berries({ estimatedQuantity: null })], "(3, 4); Grassland, Flat");

    expect(container.querySelector(".tile")).toHaveAttribute("title", "(3, 4); Grassland, Flat\nMapResources: Berries");
  });

  /** Ground the player has never been to is solid fog over a placeholder biome: a resource drawn on it would be the one thing showing through. */
  it("draws and names nothing on an unexplored tile", () => {
    const { container } = renderTile([berries()], "(3, 4); Unknown", { ...TILE, isExplored: false });

    expect(container.querySelector(".tile-map-resources")).toBeNull();
    expect(container.querySelector(".tile")).toHaveAttribute("title", "(3, 4); Unknown");
  });

  it("leaves the title alone on a tile with no resources", () => {
    const { container } = renderTile(undefined, "(3, 4); Grassland, Flat");

    expect(container.querySelector(".tile")).toHaveAttribute("title", "(3, 4); Grassland, Flat");
  });
});

describe("Tile double click", () => {
  it("reports the tile that was double clicked", () => {
    const onDoubleClick = vi.fn();
    const { container } = render(<Tile tile={TILE} sizePx={50} geography={GEOGRAPHY} onDoubleClick={onDoubleClick} />);

    fireEvent.doubleClick(container.querySelector(".tile")!);

    expect(onDoubleClick).toHaveBeenCalledWith(TILE);
  });

  /** A browser sends both clicks of a double click as well, and the first of them must still do its ordinary work — selecting the force standing there. */
  it("still reports the single clicks a double click is made of", () => {
    const onClick = vi.fn();
    const { container } = render(<Tile tile={TILE} sizePx={50} geography={GEOGRAPHY} onClick={onClick} onDoubleClick={vi.fn()} />);

    const element = container.querySelector(".tile")!;
    fireEvent.click(element);
    fireEvent.click(element);
    fireEvent.doubleClick(element);

    expect(onClick).toHaveBeenCalledTimes(2);
  });
});
