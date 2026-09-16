import { fireEvent, render, screen } from "@testing-library/react";
import TileToolbar from "../TileToolbar";
import { formatSustenanceAmount } from "../formatSustenanceAmount";

describe("TileToolbar", () => {
  it("renders nothing without a tile", () => {
    render(<TileToolbar tile={null} />);

    expect(screen.queryByLabelText("Tile toolbar")).not.toBeInTheDocument();
  });

  /** The same two facts the tile-click toast used to carry, so replacing the toast loses nothing. */
  it("shows the tile's coordinates and terrain", () => {
    render(<TileToolbar tile={{ x: 3, y: 4, terrainLabel: "Grassland, Hills" }} />);

    const toolbar = screen.getByLabelText("Tile toolbar");

    expect(toolbar.querySelector(".tile-toolbar__coordinates")?.textContent).toBe("(3, 4)");
    expect(toolbar.querySelector(".tile-toolbar__terrain")?.textContent).toBe("Grassland, Hills");
  });

  /** A resource the biome doesn't have is left out, but one that is merely stripped still shows its 0 — the two mean different things to a player. */
  it("shows each present sustenance resource and leaves out the absent ones", () => {
    render(
      <TileToolbar
        tile={{ x: 3, y: 4, terrainLabel: "Grassland, Flat", sustenance: { isObserved: true, forage: 334.5, hunt: 0, fish: null, fodder: 1250 } }}
      />,
    );

    expect(screen.getByLabelText("Forage 334")).toBeInTheDocument();
    expect(screen.getByLabelText("Hunt 0")).toBeInTheDocument();
    expect(screen.getByLabelText("Fodder 1.2k")).toBeInTheDocument();
    expect(screen.queryByLabelText(/^Fish/)).not.toBeInTheDocument();
  });

  /** Terrain first, then what the player can feed on, then the coordinates — the order a player reasons about a tile in. */
  it("lays the lines out as terrain, sustenance, then coordinates", () => {
    render(<TileToolbar tile={{ x: 3, y: 4, terrainLabel: "Grassland, Flat", sustenance: { isObserved: true, forage: 400, hunt: null, fish: null, fodder: null } }} />);

    const lineClasses = Array.from(screen.getByLabelText("Tile toolbar").children).map((line) => line.className);

    expect(lineClasses).toEqual(["tile-toolbar__terrain", "tile-toolbar__sustenance", "tile-toolbar__coordinates"]);
  });

  /** One tooltip covers the icon and its number, naming the resource and the amount together. */
  it("shows the resource and amount in a tooltip when the icon and number are hovered", async () => {
    render(<TileToolbar tile={{ x: 3, y: 4, terrainLabel: "Grassland, Flat", sustenance: { isObserved: true, forage: 400, hunt: null, fish: null, fodder: null } }} />);

    const amount = screen.getByLabelText("Forage 400").querySelector(".tile-toolbar__sustenance-amount")!;
    fireEvent.mouseEnter(amount.parentElement!);

    expect(await screen.findByRole("tooltip")).toHaveTextContent("Forage 400");
  });

  /** An unwatched tile discloses nothing, so no sustenance group is drawn even if a value somehow arrives with it. */
  it("shows no sustenance for a tile out of sight", () => {
    render(
      <TileToolbar tile={{ x: 3, y: 4, terrainLabel: "Grassland, Flat", sustenance: { isObserved: false, forage: 400, hunt: null, fish: null, fodder: null } }} />,
    );

    expect(screen.queryByLabelText("Sustenance available")).not.toBeInTheDocument();
  });

  it("shows no sustenance before any has arrived", () => {
    render(<TileToolbar tile={{ x: 3, y: 4, terrainLabel: "Grassland, Flat" }} />);

    expect(screen.queryByLabelText("Sustenance available")).not.toBeInTheDocument();
  });
});

describe("formatSustenanceAmount", () => {
  /** Always rounded down, so the bar never promises food the tile doesn't have. */
  it.each([
    [0, "0"],
    [334.9, "334"],
    [999.99, "999"],
    [1000, "1k"],
    [1299, "1.2k"],
    [12345, "12.3k"],
  ])("formats %d as %s", (value, expected) => {
    expect(formatSustenanceAmount(value)).toBe(expected);
  });
});
