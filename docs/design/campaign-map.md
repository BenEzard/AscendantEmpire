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
are the same for all empires.** How geography and biome combine into a tile's movement cost is in
[unit-movement.md](units/unit-movement.md#tile-cost--built).

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
tile). How depletion and renewal are calculated is in [Depletion of resources](#depletion-of-resources).

A biome with any sustenance value (a non-`null` Forage, Hunt, Fish or Fodder base, `0` included)
must have a Regeneration Rate of at least **1.05**. A biome with none must leave the rate empty, since
it has nothing to regrow. The biome validator enforces both.

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

The **Regeneration Rate** is *not* player-specific. It is the biome's rate, raised only on a claimed
tile by the claimant's Settlement-scoped modifiers (see [Replenishment](#replenishment)).

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

A tile's depletion state is held in `game.CampaignMapDepletion`, one row per tile that has been fed
on, keyed by the game tile's `CampaignMapId`. The row is created the first time a Force feeds on the
tile and persists between turns; a tile with no row has had nothing taken. A Player's view joins to
it on `PlayerCampaignMap.CampaignMapId`.

> TODO: Tiles within a settlement zone also need to be found for replenishment, and wait on
> Settlements.

### What a tile stores: the amount taken

A tile's depletion state is **Player-neutral**. For each sustenance resource (Forage, Hunt, Fish,
Fodder) the tile stores only **how much has been taken** from it, `Taken` (never below `0`), plus
`FedOnRound`, the last round anyone **fed on it** (see [Replenishment](#replenishment)). A tile was
fed on this round when `FedOnRound` is the current round number, so the marker never needs resetting. It does not store an
amount remaining, because the amount remaining is different for every Player.

Each Player sees, and can extract, only what is left of **their own** effective value:

```
Available to a Player = MAX(0, [Player's Effective Value] − Taken)
```

- **Feeding** adds what was extracted to `Taken`. A Player can never extract more than is
  available to them.
- **Modifier changes** need no reconciliation. Effective values are read live, so a Player who
  gains or loses a modifier simply sees a different amount against the same `Taken`.
- **No Player is recorded** on the tile, and there is no hand-over calculation when a different
  Player arrives: every Player reads the same `Taken`.
- **Shown only on tiles in live sight.** The tile toolbar shows a Player what is available to them
  on the selected tile (`GET campaign-map/player/tiles/{x}/{y}/sustenance`), but only while one of
  their Observers can see it. `Taken` grows whenever anyone feeds, so disclosing it on an unwatched
  tile would reveal where other Players' Forces have been. The toolbar shows each resource as
  `MAX(0, effective − Taken)`, and a resource the biome does not have is left out.

A Player with a higher value can therefore reach food a weaker Player cannot. Once a weaker Player
has stripped a tile, a stronger Player still finds their surplus on it.

> **Why an absolute amount rather than a percentage.** Storing a fraction remaining
> (`fraction × own value`) would make a stronger Player's advantage "more yield from the same
> animals", and a tile emptied by anyone would be empty for everyone. Storing the amount taken gives
> the stronger Player a reserve the weaker Player cannot touch, which is the advantage tech is meant
> to buy. Converting a Player's *view* into another Player's view (rather than storing a neutral
> amount) is order-dependent and leaks food on every hand-over, so it is avoided.

### Scenario — a unit moves onto a tile

Suppose Empire A has tech improvements raising their Grassland Hunt to 200, and moves a 100-man unit
onto an untouched grassland tile. The tile's `Taken` is `0` for every resource, so Empire A sees:

| | Forage | Hunt | Fish | Fodder |
| --- | --- | --- | --- | --- |
| Available to Empire A | 400 | 200 | 0 | 1000 |

At end of turn, the unit needs 100 food (100 men). On land the pool is Forage and Hunt; on water it
is Fish alone. Fodder is for animals and is not eaten by men. The pool available is `400 + 200 = 600`,
and the need is split in proportion to what each resource has, so `2/3 × 100 = 66.667` is taken from
Forage and `1/3 × 100 = 33.333` from Hunt. The split is in thousandths, with any thousandth left over
by rounding going to the resource whose share lost the most, so the amounts taken always sum exactly
to the need:

| | Forage | Hunt | Fish | Fodder |
| --- | --- | --- | --- | --- |
| Taken (stored on the tile) | 66.667 | 33.333 | 0 | 0 |
| Available to Empire A | 333.333 | 166.667 | 0 | 1000 |
| Available to a Player on base values (Hunt 100) | 333.333 | 66.667 | 0 | 1000 |

Had the pool held less than 100, the unit would take everything available and go short by the rest.

### Replenishment

Replenishment runs **once per tile at the end of each round**, after every Player's turn, for each
sustenance resource with a non-`null` base.

Today this is the end-of-round `RegenerateTileSustenance` process. It reads the Game's
`game.CampaignMapDepletion` rows with any `Taken` above `0` (a row regrown to `0` is kept for the next
feed) and treats every tile as unclaimed. A biome with a sustenance base but no rate predates the
validator rule; its tiles are skipped with a warning until the data is re-seeded.

#### Inputs

| Input | Unclaimed tile | Claimed tile (within a Player's territory) |
| --- | --- | --- |
| `Base` | The biome's base value from `map.CampaignBiomes` | The **claimant's** effective value for the tile (empire-wide and Settlement-scoped modifiers) |
| `Rate` | The biome's Regeneration Rate | The biome's Regeneration Rate plus the claimant's Settlement-scoped regeneration modifiers |
| `FedOn` | Whether anyone fed on the tile this round | same |

`Base` sets how much regrows, the 10% refill and the cap. Using the claimant's values on a claimed
tile is how a Player's management of their own land shows: their tech gives a higher `Base` (more
regrowth, a higher refill) and their Settlements can raise `Rate`. Wild land always recovers at the
biome's values, whoever was last on it.

> TODO: Settlement-scoped regeneration modifiers wait on Settlements, as Settlement-scoped
> sustenance modifiers do.

A tile was **fed on** this round if any Force ended its turn on it, or passed through it (the 20%
draw), **whether or not there was anything left to take**. It depends on presence, not on food
extracted; otherwise a Force sitting on an empty tile would let it refill.

> TODO: Whether a Settlement's population feeding on its own territory counts as feeding on the tile
> is deferred to the Settlement mechanics.

#### Calculation

`Base − Taken` is the tile's **base view**: what is left measured against `Base`. It can be negative
when a stronger Player has taken more than `Base`.

| Condition | Fed on this round | Nobody fed on it |
| --- | --- | --- |
| `Taken ≥ Base` (bare at base values) | **No change** | `Taken = 0.9 × Base` |
| `Taken > 0.9 × Base` (base view below 10%) | Regrow | `Taken = 0.9 × Base` |
| Otherwise | Regrow | Regrow |

```
Regrow:  Taken = MAX(0, Taken − (Base − Taken) × (Rate − 1))
```

Regrowing is the base view multiplied by `Rate`, capped at `Base` (so `Taken` never goes below `0`).
A tile below 10% that is left alone for a round jumps to 10% of `Base`, rather than regrowing, and
skips regrowth for that round.

`FedOn` is `FedOnRound` equal to the round that is ending, so nothing needs clearing for the next
round. Amounts are in thousandths, and rounding always goes against the tile: the regrowth is rounded
down and `0.9 × Base` is rounded up.

The rules in effect:

- A tile **can be fully depleted**. While someone keeps feeding on a bare tile it stays bare, for
  every Player, however strong.
- A tile **left alone** for a round recovers to at least 10% of `Base`.
- A **partly depleted** tile regrows at `Rate` whether or not it is fed on.

> **Why nothing changes on a bare tile that is fed on.** The alternative, converting the base view
> back into `Taken` (`Taken = Base − new base view`), forgets whatever was taken above `Base`. A
> stronger Player's surplus (`their value − Base`) would come back in full every round on a tile that
> is meant to be bare, and a Force of that size could camp on it forever while no weaker Player could
> eat at all. The surplus grows with tech (at +100% it is a whole tile's worth every round), which
> would break denial — scorched earth, stripping a tile ahead of an enemy, starving besiegers —
> against stronger Players.
>
> **Why `Base` rather than the highest value of any Player.** Measuring against the highest Player
> value also closes that leak, but every Player's regrowth then speeds up with the best Player's tech,
> and a weaker Player sees nothing on a tile a stronger Player stripped until the stronger Player's
> whole surplus has grown back first.

#### Worked examples

Illustrative values: an unclaimed tile, Hunt `Base` 80, `Rate` 1.1. Player **Default** has Hunt 80
(no modifiers); Player **Advanced** has Hunt 100 (+25%).

**Default, then Advanced.**

| Step | `Taken` | Default sees | Advanced sees |
| --- | --- | --- | --- |
| Untouched | 0 | 80 | 100 |
| Default takes 30 | 30 | 50 | 70 |
| Round end, fed on: regrow `30 − 50 × 0.1` | 25 | 55 | 75 |
| Default leaves; Advanced arrives and takes 30 | 55 | 25 | 45 |

The order of arrival makes no difference: had Advanced taken the first 30, every number would be the
same.

**Default strips the tile; Advanced camps on it.**

| Step | `Taken` | Default sees | Advanced sees |
| --- | --- | --- | --- |
| Default takes 80 | 80 | 0 | 20 |
| Round end, fed on, `Taken ≥ Base`: no change | 80 | 0 | 20 |
| Advanced arrives and takes 20 | 100 | 0 | 0 |
| Round end, fed on, `Taken ≥ Base`: no change | 100 | 0 | 0 |
| Advanced leaves; round end, nobody fed on it: `0.9 × 80` | 72 | 8 | 28 |
| Round end, nobody fed on it: regrow `72 − 8 × 0.1` | 71.2 | 8.8 | 28.8 |

**Claimed tile.** The same tile claimed by Advanced uses `Base` 100. With `Taken` 25 and the tile fed
on, it regrows by `(100 − 25) × 0.1 = 7.5` (to `Taken` 17.5), where wild land regrows by
`(80 − 25) × 0.1 = 5.5`.

#### Accepted behaviour

- **A small Force can live on a tile indefinitely** by eating no more than the tile regrows. The
  most a tile can sustain each round, per resource, is `Base × (1 − 1 / Rate)`: eat it down to
  `Base / Rate` and it regrows to full. For example, Hunt 30 sustains 2.7 a round at `Rate` 1.1, or
  10 at `Rate` 1.5.
- **A round left alone restores a stronger Player's whole surplus** (`Taken = 0.9 × Base` forgives
  anything taken above `Base`). Using it means leaving the tile for a full round, so it rewards
  moving around rather than camping. Stripping a tile and staying on it still denies it to everyone.
- **A base of `0`.** If a Player's modifiers give them a value in a biome whose base is `0` (and not
  `null`), an unclaimed tile is always bare at base values: it stays bare while fed on, and resets to
  `Taken = 0` after a round left alone.

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

> TODO: Decide how scorched earth sets `Taken`. To leave the tile bare for every Player it must be at
> least the highest effective value any Player has for that tile.

**Consumption per Unit (within a Force):** if `[Food Required] > 0`,
`Σ([Number of individuals] × [Food requirements of Unit Base])`.

**Force gathering bonuses** (e.g. a forager specialist attached to a Command) reduce the food the
Force draws from the tile. They do **not** raise the tile's maximum. For example, a 100-man Force
with a forager worth 10 needs only 90 food from the tile.

### End-of-turn calculation

- Pull tiles that recently had a unit, or are within a settlement zone, from the index table.
- Add up all Action bonuses for the empire (e.g. FORAGE, HUNTING).
- For each unit, calculate food required (based on individuals and the food requirement of their
  base, plus attached specialists). A man = 1 food; a mounted man = 1 food + 2 fodder.
- Add each Force's extraction to the tile's `Taken`, and mark the tile as fed on this round.

Today this is the end-of-turn `Gathering` process. It feeds the Player's Forces that are not in a
Battle (so only one side ever feeds from a tile), at 1 troop = 1 food, from the tile each ends its
turn on. The Forces on a tile feed from one pool, and a shortfall is spread across them in proportion
to their troops. Each Force's need, food and shortfall is handed to `IUnderfedForceResolver`, the seam
for the consequences of going hungry; it only logs until the damage model for partial feeding is
designed.

At the end of the **round** (after every Player's turn), the `RegenerateTileSustenance` process
replenishes each depleted tile as described in [Replenishment](#replenishment).

### Regeneration and farming bonuses

Regeneration speed depends on climate, and the same rate applies to all renewable food sources on
the tile. The rate is the biome's; only a claimant's Settlement-scoped modifiers raise it (see
[Replenishment](#replenishment)). Terrain improvements and equipment add yield:

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

### Support indicator

A tile's **support level** says whether it can feed a set of Forces from the food available to the
Player: the pool of Forage and Hunt on land, or Fish on water, less what has been taken — the same
pool `Gathering` feeds from (`TileFoodPool`). The need is 1 food per troop, healthy and injured,
counting only Forces that feed (troops left, not in a Battle).

| Level | Rule | Colour |
| --- | --- | --- |
| Exhausted | Nothing available at all. Shown even with no Forces on the tile. | Dark red |
| Plenty | Available ≥ 2 × need. | Green |
| OneTurn | Available ≥ need. | Yellow |
| Insufficient | Some food, but less than the need. | Red |
| *(none)* | Food available but no need. | No colour |

The rules are checked in that order. The number of turns is plain available ÷ need: it does not count
regrowth at the end of a round, or other Players feeding on the tile.

The level is shown in two places:

- **Tile toolbar.** The selected tile's background. The need is the Player's own Forces standing on
  the tile. It is returned with the tile's sustenance.
- **Hover marker.** While Forces are selected, a circle at the centre of the hovered tile. The need is
  the selected Forces plus the Player's own Forces already on the hovered tile, each counted once. No
  marker is drawn on the selected Forces' own tile. Selecting Forces makes one request,
  `GET campaign-map/player/tiles/support`, which returns the level of every tile in live sight, so
  hovering is a lookup rather than a request per tile.

Both only disclose tiles in the Player's live sight, like the sustenance values: a level reveals what
is left on a tile, which changes as anyone feeds on it.

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

What a Force or Building can see is determined by both **Sight** and **Detection**.

> This section is the *design* of vision. For how it is stored, sent to the client and drawn —
> explored ground versus ground currently watched — see [Fog of War](../fog-of-war.md).

### Sight

Sight is the ability to see the *geography of the land, buildings, and certain resources* from a
distance. It is determined by:

- the Sight value of the observer, worked out by the rules below;
- the presence of specialists with a `SIGHT_PERK`;
- the geography type of the occupied and surrounding tiles;
- the biome type of the occupied and surrounding tiles;
- vision-blocking infrastructure (e.g. a border wall).

A unit automatically sees the tile it occupies. For every additional point of unit sight it may see
one more tile, provided nothing impedes the line of sight.

#### How an observer's Sight is worked out

- A **Unit's** Sight is the **sum** of the Sight values of its components — the base and everything
  fitted to it. Sight is authored per component and is optional: a component with no Sight value
  contributes nothing, so a Unit built entirely from such components sees only the tile it stands on.
- A **Command's** Sight is the **maximum** Sight of the Units within it. A Command does not add its
  members' Sight together.
- A Unit or Command standing on a tile with a **Structure** sees `MAX(own Sight, Structure Sight)`.
- Two Units, or a Unit and a Command, standing on the same tile **do not** share Sight or Detection.
  They observe independently; neither lends the other its range.

> **Implementation note.** Every Unit and every Structure is emitted as its own map observer, and the
> revealed map is the union of what they each see. A union of ranges radiating from the same tile is
> arithmetically the maximum of those ranges, so the Command and Structure rules above already hold
> without being special-cased anywhere in the sight pipeline. Only the per-Unit sum is computed
> explicitly, at template compile time into `CompiledUnitTemplate.TotalSight`.
>
> The rules in this section are computed in exactly one place, the pure `FieldOfView` class, which both the live map read and the intelligence witness service use. See [intelligence-and-sightings.md](intelligence-and-sightings.md#the-witness-service).

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

#### Standing in dense terrain

A sight-blocking biome takes an observer's sight away outright: it reports the tile it occupies and
nothing else. That beats the elevation bonus — a hilly jungle is still a jungle, and the extra height
buys nothing through the canopy.

There is one addition, and it is not a weakening of the blindness. A blinded observer also reveals
each **adjacent tile its own movement domain could not enter**:

| Observer | Reveals | Stays dark |
|---|---|---|
| A land force | Water, mountains, void | Every ordinary land neighbour |
| A ship | Land, mountains, void | Open water |
| A flier | Mountains, void | Everything passable |
| A **structure** | Judged as a land force | — |

Deep in a jungle every neighbour is ordinary land, so a land force reveals **nothing at all** and the
biome is exactly as concealing as it looks. On a jungle coast it reveals the sea and not the shoreline
beside it.

> **Why the addition exists.** With no sight, the only way left to uncover a neighbouring tile is to
> walk onto it. That works for land — step out of the jungle and look back — but not for anything the
> observer is barred from. A jungle-covered coastline could otherwise never reveal the sea beyond it,
> by any legal action in the game, and the same held for a mountain or void tile beside a jungle. The
> reveal is therefore not really *sight*; it is the map conceding what the player could never have
> gone to find out.

The rule asks the movement rules themselves, so "revealed" and "refused a step" are the same
question with the same answer. The map can never show water it would then let a force sail into, nor
refuse a step onto ground it had never revealed.

A **structure** has no movement domain of its own and is judged as a land force. Without that, a
settlement founded on a coastal jungle would sit beside sea nothing could ever uncover — the force
that founded it was standing on the same blocking tile, so it revealed nothing either.

This is all a rule about the *occupied* tile. A sight-blocking tile further out still stops vision
dead at itself, exactly as before, and is what the sight path below is walking to find.

#### Technical implementation of sight

For a unit at (40, 40) with a Sight value of 4:

1. If the occupied tile has a sight-blocking biome, the unit can see no further — sight is zero and the
   walk below never starts. It still reveals the neighbours it could not have reached; see
   [Standing in dense terrain](#standing-in-dense-terrain).
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

1. the Detection value of each Unit in the Force;
2. the size of the unit (more men = more eyes = greater detection);
3. modifiers from any Specialists in the Force;
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
