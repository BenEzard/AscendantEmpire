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

  /** The bar shows two figures, never one per asset: everything the player can eat added up under the apple, and fodder — which feeds animals — beside it. */
  it("adds the player's own food into one total and keeps fodder beside it", () => {
    render(
      <TileToolbar
        tile={{ x: 3, y: 4, terrainLabel: "Grassland, Flat", sustenance: { isObserved: true, forage: 334.5, hunt: 0, fish: null, fodder: 1250, supportLevel: null } }}
      />,
    );

    expect(screen.getByLabelText("Sustenance 334: Forage (334) + Hunt (0)")).toBeInTheDocument();
    expect(screen.getByLabelText("Fodder 1.2k")).toBeInTheDocument();
    // One figure per icon on the line itself: the parts are only named in the tooltip hovering the apple raises.
    expect(screen.getByLabelText("Tile toolbar").querySelectorAll(".tile-toolbar__sustenance-amount")).toHaveLength(2);
  });

  /** Fish is food for the player like forage and hunt, so a water tile's total is the apple too rather than an icon of its own. */
  it("counts fish into the sustenance total", () => {
    render(<TileToolbar tile={{ x: 3, y: 4, terrainLabel: "Ocean, Flat", sustenance: { isObserved: true, forage: null, hunt: null, fish: 120, fodder: null, supportLevel: null } }} />);

    expect(screen.getByLabelText("Sustenance 120: Fish (120)")).toBeInTheDocument();
  });

  /** Summed before it is formatted, so a tile with several fractional assets isn't reported as having less food than it holds. */
  it("sums the raw amounts rather than the rounded ones", () => {
    render(<TileToolbar tile={{ x: 3, y: 4, terrainLabel: "Grassland, Flat", sustenance: { isObserved: true, forage: 0.9, hunt: 0.9, fish: 0.9, fodder: null, supportLevel: null } }} />);

    expect(screen.getByLabelText("Sustenance 2: Forage (0) + Hunt (0) + Fish (0)")).toBeInTheDocument();
  });

  /** Nothing the player can eat means no apple at all, rather than an apple showing 0 — the tile has none of those assets, it isn't stripped of them. */
  it("shows no apple on a tile with only fodder", () => {
    render(<TileToolbar tile={{ x: 3, y: 4, terrainLabel: "Desert, Flat", sustenance: { isObserved: true, forage: null, hunt: null, fish: null, fodder: 40, supportLevel: null } }} />);

    expect(screen.getByLabelText("Fodder 40")).toBeInTheDocument();
    expect(screen.queryByLabelText(/^Sustenance \d/)).not.toBeInTheDocument();
  });

  /** Terrain first, then what the player can feed on, then the coordinates — the order a player reasons about a tile in. */
  it("lays the lines out as terrain, sustenance, then coordinates", () => {
    render(<TileToolbar tile={{ x: 3, y: 4, terrainLabel: "Grassland, Flat", sustenance: { isObserved: true, forage: 400, hunt: null, fish: null, fodder: null, supportLevel: null } }} />);

    const lineClasses = Array.from(screen.getByLabelText("Tile toolbar").children).map((line) => line.className);

    expect(lineClasses).toEqual(["tile-toolbar__terrain", "tile-toolbar__sustenance", "tile-toolbar__coordinates"]);
  });

  /** Hovering the apple is the only place the individual amounts are shown, so this pins the sum the tooltip spells out and the order it lists them in. */
  it("names each asset behind the total in the apple's tooltip when it is hovered", async () => {
    render(<TileToolbar tile={{ x: 3, y: 4, terrainLabel: "Grassland, Flat", sustenance: { isObserved: true, forage: 400, hunt: 12, fish: null, fodder: null, supportLevel: null } }} />);

    fireEvent.mouseEnter(screen.getByLabelText("Sustenance 412: Forage (400) + Hunt (12)").querySelector(".tile-toolbar__sustenance-stack")!);

    expect(await screen.findByRole("tooltip")).toHaveTextContent("Forage (400) + Hunt (12)");
  });

  /** Fodder has nothing to break down, so it keeps the plain name-and-amount tooltip every asset used to have. */
  it("names fodder and its amount in a tooltip when it is hovered", async () => {
    render(<TileToolbar tile={{ x: 3, y: 4, terrainLabel: "Grassland, Flat", sustenance: { isObserved: true, forage: null, hunt: null, fish: null, fodder: 40, supportLevel: null } }} />);

    fireEvent.mouseEnter(screen.getByLabelText("Fodder 40").querySelector(".tile-toolbar__sustenance-stack")!);

    expect(await screen.findByRole("tooltip")).toHaveTextContent("Fodder 40");
  });

  /** An unwatched tile discloses nothing, so no sustenance group is drawn even if a value somehow arrives with it. */
  it("shows no sustenance for a tile out of sight", () => {
    render(
      <TileToolbar tile={{ x: 3, y: 4, terrainLabel: "Grassland, Flat", sustenance: { isObserved: false, forage: 400, hunt: null, fish: null, fodder: null, supportLevel: null } }} />,
    );

    expect(screen.queryByLabelText("Sustenance available")).not.toBeInTheDocument();
  });

  /** The background says whether the tile can feed the forces on it; each level has its own colour class. */
  it.each([
    ["Plenty", "tile-toolbar--support-plenty"],
    ["OneTurn", "tile-toolbar--support-one-turn"],
    ["Insufficient", "tile-toolbar--support-insufficient"],
    ["Exhausted", "tile-toolbar--support-exhausted"],
  ] as const)("colours the bar for a %s support level", (supportLevel, expectedClass) => {
    render(
      <TileToolbar tile={{ x: 3, y: 4, terrainLabel: "Grassland, Flat", sustenance: { isObserved: true, forage: 0, hunt: 0, fish: null, fodder: null, supportLevel } }} />,
    );

    expect(screen.getByLabelText("Tile toolbar")).toHaveClass("tile-toolbar", expectedClass);
  });

  /** No forces on a tile with food means no level, and no colour. */
  it("leaves the bar uncoloured with no support level", () => {
    render(<TileToolbar tile={{ x: 3, y: 4, terrainLabel: "Grassland, Flat", sustenance: { isObserved: true, forage: 400, hunt: null, fish: null, fodder: null, supportLevel: null } }} />);

    expect(screen.getByLabelText("Tile toolbar").className).toBe("tile-toolbar");
  });

  /** An unwatched tile discloses nothing, so a level that somehow arrives with it is not drawn either. */
  it("leaves the bar uncoloured for a tile out of sight", () => {
    render(
      <TileToolbar tile={{ x: 3, y: 4, terrainLabel: "Grassland, Flat", sustenance: { isObserved: false, forage: null, hunt: null, fish: null, fodder: null, supportLevel: "Exhausted" } }} />,
    );

    expect(screen.getByLabelText("Tile toolbar").className).toBe("tile-toolbar");
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
