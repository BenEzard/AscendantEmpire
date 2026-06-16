# Campaign Map

The campaign map is the strategic world map, made up of **tiles**. (The tactical maps generated for
combat are covered separately in [battlefield-maps.md](battlefield-maps.md).)

## Properties of the campaign map

- The map has a designated **width** and **height**.
- It may optionally have **vertical and/or horizontal wrapping**, allowing units to circumnavigate
  the map on that axis.
- It may contain **void tiles**, which cannot be entered, seen past, or used to collect resources.

## Properties of tiles

### Geography

The "shape" of the land — Plain (flat), Hilly, Mountainous, Ice, Ocean (water), Coastal (water).
Geography affects movement speed and confers sight advantages or disadvantages. **Geography values
are the same for all empires.**

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

### Biome

The biome is the vegetation/climate/environment on a tile. It affects:

- the type and quantity of resources that can be extracted,
- how quickly those resources replenish,
- an optional **sight penalty**, and
- an optional **movement penalty**.

Together with geography, the biome determines a tile's "base" resource factor. **Each empire starts
with the same biome values** (set by the map).

| Biome | Forage | Hunt | Fish | Fodder | Regen.Rate | Bone | Wood | Stone | SightBarrier | MovePenalty |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Unexplored | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 1 |
| Arid | 100 | 70 | 0 | 50 | 1.5 | 1 | 1 | 1 | 0 | 1 |
| Coastal | 10 | 20 | 300 | 0 | 1.5 | 1 | 0 | 0 | 0 | 1 |
| Desert | 50 | 40 | 0 | 0 | 1.25 | 1 | 0 | 1 | 0 | 1.5 |
| Forest | 300 | 150 | 0 | 100 | 1.8 | 1 | 20 | 1 | 100 | 2 |
| Grassland | 400 | 100 | 0 | 1000 | 1.5 | 1 | 1 | 1 | 0 | 1 |
| Ice | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 1.5 |
| Jungle | 350 | 150 | 0 | 0 | 2 | 1 | 30 | 1 | 100 | 3 |
| Lake | 100 | 70 | 0 | 0 | 1.5 | 1 | 0 | 0 | 0 | 1 |
| Mangrove | 100 | 70 | 0 | 0 | 1.5 | 1 | 10 | 1 | 0 | 1.5 |
| Ocean | 0 | 70 | 0 | 50 | 1.5 | 0 | 0 | 0 | 0 | 1 |
| Tundra | 100 | 70 | 0 | 10 | 1.5 | 1 | 1 | 1 | 0 | 1 |
| Void | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 1 |
| Woodland | 300 | 100 | 0 | 100 | 1.75 | 1 | 20 | 1 | 0 | 1 |

(Hunt, Wood, and Stone above are *generic* resource types.)

**Sustenance values** (Forage, Hunt, Fish, Fodder) can be depleted and renewed. The longer a unit
stays on a tile — and the more individuals in it — the lower its forage gets. Depleted sustenance
renews at the tile's **Regeneration Rate** (the same rate applies to all sustenance values on the
tile).

**Material values** (Bone, Wood, Stone) are *not* depleted: that amount can be extracted per turn
indefinitely, until game mechanics turn them off (e.g. bone becomes useless in modern times).

> Open question: does this open a hack where tiny units are mass-produced to each suck resources
> from a tile?

For empire-specific values, a **Multiplier** and an **AddValue** apply per resource (e.g.
`ForageBaseValue`, `ForageMultiplierValue`, `ForageAddValue`). A tile may also carry **additional
resources** on top of its biome (e.g. a forest tile with a 10,000-wood resource, generic or luxury),
which can also be depleted and renewed.

The total amount of each resource on a tile is:

```
[Base Value] × [Multiplier Value] + [Add Value] + [Additional Resources]
```

For empire-specific multiplier examples, see [resources-data-model.md](../resources-data-model.md).

## Depletion of resources

A unit moving onto or through a tile depletes some of its resources (typically via scavenging),
scaled by unit size. Enough scavenging — too large a unit, too many units passing through, or units
lingering — drives the available amount to **0**.

The purpose of these mechanics:

- Force **nomadic behaviour** early-game (until food technology improves, or a food-rich location
  is found).
- Make **supply chains** more important (especially after the early game).
- Make **raiding** of settlements more likely than conquering in the early game.
- Add pressure on both the besieged and the besieging.
- Make "Stacks of Death" less likely, or harder to supply.
- Enable **scorched earth, salting, and cavalry rushes** (à la the Mongols).

### Tracking

Tiles visited recently (those below maximum resource values) and tiles within a settlement zone are
held in a separate index table for easy access.

### Scenario — a unit moves onto a tile

Suppose Empire A has two tech improvements raising Grassland Hunt to 200 and Regen.Rate to 1.7, and
moves a 100-man unit onto a grassland tile. A record is created in `OccupiedTiles` using Empire A's
biome values:

| | Forage | Hunt | Fish | Fodder | Regen.Rate |
| --- | --- | --- | --- | --- | --- |
| Tile food supply | 400 | 200 | 0 | 1000 | 1.7 |

At end of turn, 100 food is deducted (100 men). Human-consumable food on the tile is
`400 + 200 + 0 = 600`, so Forage is reduced by `2/3 × 100 = 66` and Hunt by `1/3 × 100 = 33`:

| | Forage | Hunt | Fish | Fodder | Regen.Rate |
| --- | --- | --- | --- | --- | --- |
| Tile food supply | 334 | 67 | 0 | 1000 | 1.7 |

### Replenishment

At the end of each round, each foodstuff is replenished:

- if the current amount on the tile is `0` → `[Empire Resource Initial Value] × 0.1`;
- if the current amount is `> 0` → `current amount × [Regeneration Rate]`, capped at
  `[Empire Biome Resource Initial Value]`.

### Depletion rules

- Units and settlement populations are fed at the **end of their turn**.
- A hostile unit within a settlement's controlled area **gathers the resources and the settlement
  gets nothing** — even if the unit only takes 5%, it blocks the settlement from collecting the
  rest.
- A unit **passing through** a tile (not ending its turn there) consumes **20%** of its food needs
  from that tile, leaving less to deduct on the tile it ends on.
- There is a maximum a unit can harvest per tile (`UNIT_HARVEST_CAP`), regardless of availability.
- **Pillaging** a tile (consuming the unit's turn) harvests more than `UNIT_HARVEST_CAP` allows,
  feeding the unit for multiple turns and depleting the tile.
- **Scorched earth** sets all resource quantities on the tile to `0`. The unit on the tile is fed
  for that turn, but the extra resources are *not* added to the unit's supply.

**Consumption per unit (within a formation):** if `[Food Required] > 0`,
`Σ([Number of individuals] × [Food requirements of Unit Base])`.

### End-of-turn calculation

- Pull tiles that recently had a unit, or are within a settlement zone, from the index table.
- Add up all Action bonuses for the empire (e.g. FORAGE, HUNTING).
- For each unit, calculate food required (based on individuals and the food requirement of their
  base, plus attached specialists). A man = 1 food; a mounted man = 1 food + 2 fodder.
- If a tile's current food supply is below 10% of its maximum, set it to 10%.

### Regeneration and farming bonuses

Regeneration speed depends on climate, and the same rate applies to all renewable food sources on
the tile. Terrain improvements and equipment add yield:

- Terrain improvement **Irrigation**: +50% yield.
- Terrain improvement **Farm (with crop)**: +300% yield.

Equipment bonuses chain by category, choosing the best available option in each category (e.g.
machinery is picked over a plough animal). One illustrative chain:

```
Machinery (100%)        / plough animal (50%)
  + hoe (20%)           / adze (10%)
  + chemical fertilizer (30%) / animal fertilizer (15%)
  + pesticides (20%)
```

So a farm with machinery, plough animal, hoe, animal fertilizer, and pesticides yields
`100% + 20% + 15% + 20% = 155%` (the plough animal is excluded because machinery wins its category).

**Productivity rate** is how much food one individual can collect:

| Action | Productivity Rate | Notes |
| --- | --- | --- |
| FORAGE | 1.05 | One person collects enough food for 1.05 people. |

### Tile bonuses

Naturally occurring tile bonuses can also boost food:

| Bonus | Occurs on | Imparts |
| --- | --- | --- |
| Oasis | Desert | +100% food |
| Reef | Coastal | +100% food |

### Formula for units which consume (and expand population)

```
Food Harvested = ROUNDUP( IF( People × Productivity Rate < Starting Food,
                              People × Productivity Rate,
                              Starting Food ), 0 )

Food Remaining on Tile (end of turn) = ROUNDUP( IF( People − Food Harvested > 0,
                                                    People − Food Harvested,
                                                    People × 0.25 ), 0 )

Remaining Energy (of population after collecting food) = People after growth × (1 − Energy to extract food)
```

### UI implications

- If a unit is on a tile that won't feed it, the player must be **alerted** (to move it, or at
  least warned if it cannot move).
- If a unit attempts to move into a tile that won't have enough food, the player should be warned.

## Discovery of resources

Many resources are not automatically visible and must be **discovered**.

- Each resource has a **discovery likelihood**. Some (e.g. special woods) are found the instant a
  unit enters the tile (likelihood 100%); others (e.g. copper) may be 10%; some locations make a
  resource even harder to find than normal.
- A unit moving into or resting on a tile gets that % chance. A unit **fortifying**, or a nearby
  **settlement**, gives an even higher chance.
- First-time discovery of some resources (e.g. metals) adds a **new technology-tree item** that
  must be researched before the empire can use the material.

## Vision on the campaign map

What a Formation or Building can see is determined by both **Sight** and **Detection**.

### Sight

Sight is the ability to see the *geography of the land, buildings, and certain resources* from a
distance. It is determined by:

- the maximum Sight value of the base components in the Formation, or any Buildings on the same
  tile;
- the presence of specialists with a `SIGHT_PERK`;
- the geography type of the occupied and surrounding tiles;
- the biome type of the occupied and surrounding tiles;
- vision-blocking infrastructure (e.g. a border wall).

A unit automatically sees the tile it occupies. For every additional point of unit sight it may see
one more tile, provided nothing impedes the line of sight.

| Geography | Relative elevation | | Biome | Blocks sight |
| --- | --- | --- | --- | --- |
| Unexplored | 0 | | Unexplored | False |
| Ice | 0 | | Arid | False |
| Coastal | 0 | | Coastal | False |
| Ocean | 0 | | Desert | False |
| Plain | 0 | | Forest | True |
| Hilly | 1 | | Grassland | False |
| Mountainous | 3 | | Ice | False |
| Void | 10 | | Jungle | True |
| | | | Lake | False |
| | | | Mangrove | True |
| | | | Ocean | False |
| | | | Tundra | False |
| | | | Void | True |
| | | | Woodland | True |

#### Technical implementation of sight

For a unit at (40, 40) with a Sight value of 4:

1. If the occupied tile has a sight-blocking biome, the unit can see no further.
2. Calculate the coordinates of tiles surrounding the occupied tile in a rectangle.
3. For each, compute a **relative (focal-offset) grid** — how many x/y values return to the occupied
   tile — and note whether the tile is diagonal, on the same horizontal/vertical, and its radius.

**Sight path** — to find where obstacles block further vision, each tile has a **sight parent** from
which its value flows:

- the occupied tile has no parent;
- a radius-1 tile's parent is the occupied tile;
- tiles on a diagonal/horizontal/vertical from the occupied tile use the preceding tile;
- the remaining tiles are more complex — each quadrant is split into an upper and a lower triangle,
  and the parent is the diagonal tile for as long as possible (e.g. for radius 3, evaluating (2,7)
  looks back at (6,2) then (5,3)).

Notation: `US` = Unit Sight, `B` = Biome, `G` = Geography, `b` = buff, `p` = perk. A value of 200
gives a radius of 2.

| Occupied tile | R1 | R2 | R3 | Outcome |
| --- | --- | --- | --- | --- |
| US=100, B=Jungle (200b) | | | | `US > Bb` false → 0; current tile only |
| US=100, G=Hilly (200b;100p), B=Jungle (200b) | | | | `US > Bb` false → 0; current tile only |
| US=100, G=Plain (100b;0p), B=Arid (0b;0p) | | | | `US > Bb`=100; +Gp=100; see 1 tile |
| US=100, G=Hilly (200b;100p), B=Arid | B=Arid | B=Arid | | `US > Bb`=100; +Gp=200; see 2 tiles |
| US=100, G=Mountainous (300b;200p), B=Arid | B=Arid | B=Arid | B=Arid | `US > Bb`=100; +Gp=300; see 3 tiles |
| US=100, G=Plain, B=Arid | B=Jungle (200b) | | | see 1 tile, then blocked by Jungle |
| US=100, G=Hilly (200b;100p) | B=Jungle (200b) | | | see 1 tile, then blocked by Jungle |

### Detection

Detection is a unit's ability to see **enemy units** on the campaign map. It is determined by:

1. the Detection value of each unit in the Formation;
2. the size of the unit (more men = more eyes = greater detection);
3. modifiers from any specialists in the Formation;
4. the **Disturbance** value of the enemy unit;
5. terrain.

Points 1–3 determine whether a unit *may* detect an enemy on a tile; points 4–5 determine the
*terrain* a unit can see.

| Resource type | Detection when on tile | Detection for surrounding tile |
| --- | --- | --- |
| Wood (Forest/Jungle) | Max detection of 0 when standing on a wood-resource tile (no surrounding tiles shown) | Max detection of 0 when looking at a Forest tile |

## Infrastructure (Terrain Improvements)

### Concept

- Infrastructure is built on campaign tiles **surrounding a settlement**.
- Selection and construction happen through the **Settlement UI** construction list.
- Initial and upkeep costs are **empire-based** and can be modified through techs.

### Inputs

- **Mandatory input** — one or more assets required for the infrastructure to function. If only a
  portion is available, output is **pro-rata**.
- **Optional input** — one or more assets that *improve* functioning. If only a portion is
  available, the improvement is pro-rata.

Inputs are divided into **classes**; for each class the best available option is chosen (by highest
desirability value), each option conferring a different bonus.

### Properties of an infrastructure

| Property | Description |
| --- | --- |
| Code Name | Generic API name |
| Empire Display Name | Name according to the empire |
| Initial Cost | Assets (0+) required to build; normally includes a **Population** amount to staff it (e.g. 50 wood) |
| Effort Cost | Effort required to build |
| Upkeep Cost | Assets (0+) required per turn to keep operational. (Possibly too complicated — undecided. If run by slaves, they need topping up due to attrition.) |
| Requirements | Prerequisite technologies, tile types, terrain improvements, or other rules. E.g. roads need road tech (kept forever once discovered); a hydrodam needs a river tile; an offshore oilrig needs an ocean tile. |
| Specialist Class | If specialists can inhabit it, what kind (e.g. FARMER, ENGINEER). The class sets the progression path (e.g. Blacksmith → Journeyman → Mastersmith). |
| Specialist Slot Count | Number of specialists employable |
| Base Output (in "units") | Base output. May be split by an output slider if the infrastructure can output more than one asset type (e.g. a blacksmith producing mining vs farming equipment from 100 base units, split 70/30 or 20/80). |
| Mandatory Input(s) | Divided into classes; best available chosen |
| Optional Input(s) | Divided into classes; best available chosen |

See [manufacturing-chains.md](manufacturing-chains.md) for fully worked farm and blacksmith
examples.
