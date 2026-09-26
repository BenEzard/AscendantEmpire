# Properties of tiles

Part of the [Campaign Map](README.md) design.

## Geography

The "shape" of the land — Plain (flat), Hilly, Mountainous, Ice, Ocean (water), Coastal (water).
Geography affects movement speed and confers sight advantages or disadvantages. **Geography values
are the same for all empires.** How geography and biome combine into a tile's movement cost is in
[unit-movement.md](../units/unit-movement.md#tile-cost--built).

| Code | Name | SightBarrier | SightAdvantage | MovementCost | DomainType |
| --- | --- | --- | --- | --- | --- |
| ? | Unexplored | 100 | 0 | 1 | ? |
| C | Coastal | 100 | 0 | 1 | W |
| H | Hilly | 200 | 100 | 2 | L |
| I | Ice | 100 | 0 | 1 | L |
| M | Mountainous | 300 | 200 | 3 | L |
| O | Ocean | 100 | 0 | 1 | W |
| P | Plain | 100 | 0 | 1 | L |
| V | Void | 1000 | 0 | 999 | V |

*Values are indicative only.*

## Rivers

Rivers run **between** tiles, along their edges, never on a tile. Each river edge is **Medium** or
**Large** (`RiverWidth`: `None`, `Medium`, `Large`).

Every tile owns only its **South** and **East** edges, as two columns on both
`mapgallery.CampaignMapGallery` and `game.CampaignMap`: `SouthRiver` and `EastRiver`, each defaulting
to `None`. A tile's North edge is the `SouthRiver` of the tile above it, and its West edge is the
`EastRiver` of the tile to its left. So every edge is stored exactly once, the two banks of a river
can never disagree, and a tile may have rivers on any combination of its four sides.

- On an axis that **wraps**, the last column's East edge (last row's South edge) is the edge it
  shares with column 0 (row 0).
- On an axis that **does not wrap**, those edges face off the map and must be `None`. The movement
  rules read them as `None` whatever is stored.

The widths live **on the tile** rather than in a sparse table of river edges. Both columns are `tinyint`, so a
tile carries 2 bytes whether or not it has a river — about 80 KB for a 40,000-tile map. A row in a separate edge
table would cost roughly 20–30 bytes with its row overhead and index, so it would only save space while rivers
bordered a small minority of tiles, and it would add a join to every step and map read, a second copy at game
setup, another step to game deletion, and a unique key to stop an edge being stored twice.

Game setup copies both columns from the gallery map. What rivers do to movement is in
[unit-movement.md](../units/unit-movement.md#rivers--built).

### What a player is shown

Rivers are **explored terrain**, like biome and geography: a player sees them on any tile they have
explored, whether or not one of their observers is watching it now. They **do not block vision**, and
being across a river never hides a tile — a river belongs to a *step*, not to a tile, so fog of war is
unaffected by it.

The map read sends each explored tile all four of its own edges — `Rivers { North, East, South, West }`
— resolved server-side from its own `SouthRiver`/`EastRiver` and its neighbours', wrapping included. An
unexplored tile sends no rivers at all, which is a different answer from four edges of `None`: the
latter says the player knows there is no river here.

Because a tile's edge is also its neighbour's, **an edge is visible as soon as either of the two tiles
it separates has been explored**. That is what puts a river on the boundary of explored ground rather
than half a tile short of it.

Both banks therefore report the same edge, and the client draws each one once: it draws every tile's
South and East, plus a North or West only where the neighbour that would otherwise have drawn it is
unexplored. The line is drawn along the boundary between the two tiles, and **Large is drawn twice as
thick as Medium**.

## Biome

The biome is the vegetation/climate/environment on a tile. It affects:

- the type and quantity of resources that can be extracted,
- how quickly those resources replenish,
- an optional **sight penalty**, and
- an optional **movement penalty**.

Together with geography, the biome determines a tile's "base" resource factor. **Each empire starts
with the same biome values** (set by the map).

| Biome | Forage | Hunt | Fish | Fodder | Regen % | Bone | Wood | Stone | SightBarrier | MovePenalty |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Unexplored | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 1 |
| Arid | 100 | 70 | 0 | 50 | 50% | 1 | 1 | 1 | 0 | 1 |
| Coastal | 10 | 20 | 300 | 0 | 50% | 1 | 0 | 0 | 0 | 1 |
| Desert | 50 | 40 | 0 | 0 | 25% | 1 | 0 | 1 | 0 | 1.5 |
| Forest | 300 | 150 | 0 | 100 | 80% | 1 | 20 | 1 | 100 | 2 |
| Grassland | 400 | 100 | 0 | 1000 | 50% | 1 | 1 | 1 | 0 | 1 |
| Ice | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 1.5 |
| Jungle | 350 | 150 | 0 | 0 | 100% | 1 | 30 | 1 | 100 | 3 |
| Lake | 100 | 70 | 0 | 0 | 50% | 1 | 0 | 0 | 0 | 1 |
| Mangrove | 100 | 70 | 0 | 0 | 50% | 1 | 10 | 1 | 0 | 1.5 |
| Ocean | 0 | 70 | 0 | 50 | 50% | 0 | 0 | 0 | 0 | 1 |
| Tundra | 100 | 70 | 0 | 10 | 50% | 1 | 1 | 1 | 0 | 1 |
| Void | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 1 |
| Woodland | 300 | 100 | 0 | 100 | 75% | 1 | 20 | 1 | 0 | 1 |

(Hunt, Wood, and Stone above are *generic* resource types.)

**Sustenance values** (Forage, Hunt, Fish, Fodder) can be depleted and renewed. The longer a unit
stays on a tile — and the more individuals in it — the lower its forage gets. Depleted sustenance
renews by the biome's **Regeneration Percent**: the share of each sustenance `Base` that regrows every round (the same percentage applies to all sustenance values on the tile). How depletion and renewal are calculated is in [Depletion of resources](depletion.md).

A biome with any sustenance value (a non-`null` Forage, Hunt, Fish or Fodder base, `0` included) must have a Regeneration Percent **more than 0% and at most 100%**. A biome with none must leave it empty, since it has nothing to regrow. The biome validator enforces both. It is stored as a fraction (`map.CampaignBiomes.RegenerationPercent`, `0.25` = 25%, to three decimal places) and entered as a percent in the Biome Manager.

**Material values** (Bone, Wood, Stone) are *not* depleted: that amount can be extracted per turn
indefinitely, until game mechanics turn them off (e.g. bone becomes useless in modern times).

> Open question: does this open a hack where tiny units are mass-produced to each suck resources
> from a tile?

Only the **sustenance values** are player-specific. A Player's own Forage, Hunt, Fish and Fodder
values per biome are raised (or lowered) by modifiers from tech advances, specialists and
departments. Each modifier can carry a flat amount, a percentage of base, or both. Some modifiers
can be removed later (e.g. a specialist is lost), so a Player's value is always derived from the
modifiers they currently hold, never accumulated. The **material values** (Bone, Wood, Stone,
Special) are static: the same for every Player in every game.

The **Regeneration Percent** is *not* player-specific. It is the biome's percentage, raised only on a claimed tile by the claimant's Settlement-scoped modifiers (see [Replenishment](replenishment.md)).

A modifier applies **empire-wide**, or to the **territory of one Settlement**.

Modifiers are stored in `game.PlayerSustenanceModifier`, one row per Player, source, resource and biome (a null biome means every biome), each with a `FlatValue` and a `PercentValue`. Losing a source deletes its rows. A Player's effective values are read only through `IPlayerSustenanceValueReader`, which reads the base values live from `map.CampaignBiomes` and applies the formula below. Nothing writes modifiers yet.

> TODO: Settlement-scoped modifiers wait on Settlements, which are not modelled yet. A Settlement
> will be its own entity (it may also be added to `StructureInstance`, so one read returns every
> map object).

A `null` base value means the resource does not exist in that biome, and it can never gain a
value: modifiers do not apply to it. A resource that can be raised in a biome starts at `>= 0`.

A tile may also carry **additional resources** on top of its biome (e.g. a forest tile with a
10,000-wood resource, generic or luxury), which can also be depleted and renewed.

The total amount of each resource on a tile is:

```
[Base Value] + [Base Value] × Σ[Percent Modifiers] + Σ[Flat Modifiers] + [Additional Resources]
```

Percentages add together rather than compound, and apply to the base only. The summed percentage
is floored at −100% and the resulting value at `0`.

For empire-specific multiplier examples, see [resources-data-model.md](../../resources-data-model.md).
