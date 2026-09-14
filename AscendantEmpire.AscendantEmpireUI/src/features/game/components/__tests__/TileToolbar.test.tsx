import { render, screen } from "@testing-library/react";
import TileToolbar from "../TileToolbar";

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
});
