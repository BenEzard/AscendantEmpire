# Map Generation

The campaign map can be generated from a source image (e.g. a real-world earth map), reduced to a
manageable palette and converted into tiles.

## Image pipeline

Source assets live under `Empire.GeneratorConsole/New_ImageResources`:

| Step | Filename | Description |
| --- | --- | --- |
| 1 | `1 – earth map - original.jpg` | Original source image. |
| 2 | `2 – earth map reduced to 20 colours.png` | Reduced to 20 colours in Gimp. |
| 3 | `3 – earth map reduced to 20 colours – rgb mode.png` | Copy/paste of the above in Gimp to drop Indexed mode (C# can't handle indexed mode). |
| | `3 – … – legend.png` | Colour-palette legend, produced by `CreateColorPaletteLegend()`. |
| 4 | | (further steps) |

Each colour in the reduced palette maps to a geography/biome combination, producing the tile grid
described in [campaign-map.md](campaign-map.md).

## Turn processing notes

- Calculate research points at the **start** of the turn; spend them at the **end** of the turn.
- Calculate and spend **gold** at the start of the turn; after the Interest technology, apply
  interest.
- A `TileMap` can be worked by **one** Action at a time.
