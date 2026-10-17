# Settlements

## Types of settlement

In rough order of growth:

- Camp
- Village
- Hamlet
- Town
- City
- Major City

## Territory

A settlement owns the tiles around it. It starts with a **radius** of owned tiles, and after that
**growth is the mechanism and selection is the direction**: growing a tier grants a number of new
tiles, and the player chooses where they go rather than the radius simply enlarging.

A tile owned through a settlement carries that settlement, which is what lets Settlement-scoped
modifiers reach it (see [campaign-map/tiles.md](campaign-map/tiles.md#biome)). Conquering a
settlement's own tile transfers its whole territory at once; razing it abandons that territory
instead. The states, the transfer rules and the rebellion period are in
[campaign-map/tile-ownership.md](campaign-map/tile-ownership.md).

## Asset banks

Definitions:

- Each settlement has an **Asset Bank** — the collection of assets the settlement holds. An asset
  bank is created when the settlement is created.
- A settlement is **linked** when connected to another settlement via a transport network (road,
  river, railroad). Coastal settlements with a harbour and ship technology are automatically linked
  to other harboured settlements.
- Initially each settlement forms its own **Asset Bank Group**. When settlements are linked, they
  share an Asset Bank Group, representing the ability to share assets.
- Settlements **not** linked cannot share assets outside their group. When a settlement is
  separated from others, it reverts to its own asset bank (or any it is still connected to).
- When a linked settlement lacks an asset but another in the group has it, that bank is debited.
  If the asset isn't held in sufficient quantity across the group, the purchase cannot be made.
- When two asset banks are merged, all their assets are combined.

> Open idea (liked): an "internal transaction" between settlements — e.g. a mining settlement
> exporting stone everywhere should be considered "richer" (population boom? commerce bonus?) than
> the areas it exports to.

## Buildings

Buildings are constructed within a settlement and provide outputs or capabilities.

| Building | Acquisition Cost | Upkeep | Requires | Provides | Description |
| --- | --- | --- | --- | --- | --- |
| Meeting Place | | | Built in: Camp | 10 units of craft materials per turn | Where the tribe gathers, and consequently where they output craft. |

> Infrastructure built on tiles *outside* a settlement is linked to the settlement in which it was
> created. See [campaign-map/infrastructure.md](campaign-map/infrastructure.md).
