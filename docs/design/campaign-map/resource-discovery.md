# Discovery of resources

Part of the [Campaign Map](README.md) design.

Many resources are not automatically visible and must be **discovered**.

- Each resource has a **discovery likelihood**. Some (e.g. special woods) are found the instant a
  unit enters the tile (likelihood 100%); others (e.g. copper) may be 10%; some locations make a
  resource even harder to find than normal.
- A unit moving into or resting on a tile gets that % chance. A unit **fortifying**, or a nearby
  **settlement**, gives an even higher chance.
- First-time discovery of some resources (e.g. metals) adds a **new technology-tree item** that
  must be researched before the empire can use the material.
