# Campaign Map

The campaign map is the strategic world map, made up of **tiles**. (The tactical maps generated for
combat are covered separately in [battlefield-maps.md](../battlefield-maps.md).)

## Properties of the campaign map

- The map has a designated **width** and **height**.
- It may optionally have **vertical and/or horizontal wrapping**, allowing units to circumnavigate
  the map on that axis.
- It may contain **void tiles**, which cannot be entered, seen past, or used to collect assets.

## Documents

| Document | Covers |
| --- | --- |
| [tiles.md](tiles.md) | Tile geography and biome, the assets a biome yields, Player sustenance modifiers, and the total-asset formula. |
| [depletion.md](depletion.md) | How units and settlements deplete a tile's food: the shared `Taken` amount, depletion rules, the end-of-turn calculation, and UI implications. |
| [replenishment.md](replenishment.md) | How a depleted tile regrows at the end of each round, with worked examples and accepted behaviour. |
| [food-yield.md](food-yield.md) | Regeneration and farming bonuses, productivity rates, tile bonuses, and the formula for units which consume. |
| [support-indicator.md](support-indicator.md) | The tile support indicator: how many turns a tile can feed a set of Forces, and how it is shown. |
| [map-resources.md](map-resources.md) | MapResources on a tile: renewable and stratified MapResources, growth and spread, and how they are discovered. |
| [vision.md](vision.md) | Vision on the campaign map: Sight, Detection, dense terrain, and hidden Forces. |
| [infrastructure.md](infrastructure.md) | Infrastructure (terrain improvements) built around settlements. |
