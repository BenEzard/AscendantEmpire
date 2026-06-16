# Settlements

## Types of settlement

In rough order of growth:

- Camp
- Village
- Hamlet
- Town
- City
- Major City

## Asset banks

Definitions:

- Each settlement has an **Asset Bank** — the collection of assets the settlement holds. An asset
  bank is created when the settlement is created.
- A settlement is **linked** when connected to another settlement via a transport network (road,
  river, railroad). Coastal settlements with a harbour and ship technology are automatically linked
  to other harboured settlements.
- Initially each settlement forms its own **Asset Bank Group**. When settlements are linked, they
  share an Asset Bank Group, representing the ability to share resources.
- Settlements **not** linked cannot share resources outside their group. When a settlement is
  separated from others, it reverts to its own asset bank (or any it is still connected to).
- When a linked settlement lacks a resource but another in the group has it, that bank is debited.
  If the resource isn't held in sufficient quantity across the group, the purchase cannot be made.
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
> created. See [campaign-map.md](campaign-map.md#infrastructure-terrain-improvements).
