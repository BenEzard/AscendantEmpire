# Depletion of resources

Part of the [Campaign Map](README.md) design. How a depleted tile regrows is in
[replenishment.md](replenishment.md).

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

## Tracking

A tile's depletion state is held in `game.CampaignMapDepletion`, one row per tile that has been fed
on, keyed by the game tile's `CampaignMapId`. The row is created the first time a Force feeds on the
tile and persists between turns; a tile with no row has had nothing taken. A Player's view joins to
it on `PlayerCampaignMap.CampaignMapId`.

> TODO: Tiles within a settlement zone also need to be found for replenishment, and wait on
> Settlements.

## What a tile stores: the amount taken

A tile's depletion state is **Player-neutral**. For each sustenance resource (Forage, Hunt, Fish,
Fodder) the tile stores only **how much has been taken** from it, `Taken` (never below `0`), plus
`FedOnRound`, the last round anyone **fed on it** (see [Replenishment](replenishment.md)). A tile was
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

## Scenario — a unit moves onto a tile

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

## Depletion rules

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

## End-of-turn calculation

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
replenishes each depleted tile as described in [Replenishment](replenishment.md).

## UI implications

- If a unit is on a tile that won't feed it, the player must be **alerted** (to move it, or at
  least warned if it cannot move).
- If a unit attempts to move into a tile that won't have enough food, the player should be warned.

How long a tile can feed a set of Forces is shown by the [support indicator](support-indicator.md).
