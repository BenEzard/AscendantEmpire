# Battlefield Maps

Battlefield maps are the tactical maps where battles play out. Most combat happens here — the
exceptions (artillery bombardments, plane sorties, ICBMs) occur on the [campaign map](campaign-map.md).

> In this document, **Unit** means one fielded body of soldiers - the thing that is placed, moved and fought with on the battle map. Where several Units act under one commander, that is a **Command**; **Force** covers either. See [Terminology.md](../../Terminology.md).

## Triggering a battle

Definitions:

- A unit is **assaulting** an enemy when it attempts to enter the same tile.
- A unit being assaulted is **defending**. A defender may be:
  - **resting** (stationary on the tile),
  - **fortifying** (actively preparing a defensive position),
  - **preparing to ambush**, or
  - **hiding** (hoping not to be seen).
- A stationary **ranged** unit can attack an enemy on a tile within its range of fire.

A battle occurs when an empire moves a unit onto a tile already containing another (non-allied)
empire's units. The exception: if the occupying unit is set to **ambush** or **hide** and has not
been seen by the approaching unit, no battle triggers. Otherwise the battle begins.

On the first battle on a tile, a **battlefield map is generated**; subsequent battles reuse it.
Where a tile is ocean surrounded by ocean, the same battle map is reused to reduce redundancy. (A
similar "base" system on land could reduce generation and storage too.)

## Features of a battle map

A battle map inherits its appearance from the underlying campaign tile:

- geography type (e.g. plain, hilly),
- resources (e.g. forest),
- tile improvements (e.g. farms), and
- any fortifications that have been built.

### Tile boundaries (sourced from neighbouring tiles)

A 2-tile-wide boundary on each side of the battle map comes from the corresponding neighbouring
tiles. For the battle map of tile **E**:

- the top 2 rows are the bottom 2 rows of the tile to the north (tile B),
- the bottom 2 rows are the top 2 rows of the tile to the south (tile H), and so on.

This makes fortifications impactful as enemies approach — especially on the coast (e.g. cannon vs
ships). At the corners are 2×2 voids that cannot be entered (tiles A, C, G, I).

- Units placed within a boundary (from tiles B, D, F, H) **can attack into tile E**.
- The **assaulting** empire can kill units and destroy fortifications in the boundary area.

If the attacker enters from the south, the opposite (north) side has a **safe-zone line**, and a unit
that reaches it is added to a **safe list**. Who may move where within the boundaries, and what being safe
means, is in [unit-movement.md](units/unit-movement.md#boundaries-and-the-safe-zone).

### Geography type

If the geography is hilly, individual hills are rendered — opening up vantage points, blind spots
(valleys), and strategic locations (hilltops). Resources like forests are rendered and impose
movement and sight penalties.

### Altitude

Altitude is the average altitude of the tile; the difference between adjacent battle tiles sets the
incline steepness, which affects movement — see [unit-movement.md](units/unit-movement.md#terrain).

Even a **plain** is generated with small tree outcrops, rocks, or other features to make battle more
interesting. Coastal waters may have reefs, sandbanks, tiny islands, or occasional shipwrecks.

### Terrain improvements

Terrain improvements (especially fortifications) are visible and potentially active on the battle
map.

- Possibly, when a unit first fortifies, the player designs the fortifications (from available tech
  and resources) and they are built over time. The UI must be quick, and fortifications must be
  built **before** the player sees their foe (so they can't be tailored to the attacker).
- A **farm** improvement uses most of the land (except edges); no fortifications can be built on the
  used area — the farm must be destroyed first.
- Possible land features: forests, tree outcrops, rock outcrops, marshes, hills, valleys, cliffs,
  lakes, rivers, bridges, reefs.

### Fortifications

Some fortifications are **passive** (e.g. fence, tank trap) and block movement. Others are
**active** and fire on the enemy — active fortifications get **1 turn per round** if visible enemy
units are within range.

> Open question: should active fortifications fire blindly? Defending Forces with high enough
> **Discipline** can target specific enemies; lower-ranked soldiers fire at a random enemy in range.

| Function | Sub-function | Examples | Notes |
| --- | --- | --- | --- |
| MOVEMENT | Impede | Palisade | Blocks all movement |
| | | Walls (wood/stone) | Blocks all movement except tanks |
| | | Medium/large wall (wood/stone/concrete) | Blocks movement; +range for units on top; defensive bonus |
| | | Moat | Blocks movement; can be forded |
| | | Tank traps | Block tank movement |
| | | Horse "x" sticks | Block cavalry movement |
| | Slow | Embankment | Slows movement |
| | Speed up | Road | |
| | Extra paths | Tunnel / tunnel entrance / teleporter | |
| HEALTH | Injure on cross | Caltrops, man traps, anti-personnel mine, anti-vehicle mine, naval mine, laser grid | |
| | | Barbed wire | Damages human units only |
| | Heal | Medic tent | Increases unit survival likelihood |
| SIGHT | Increase | Fire | +sight at night / bad weather |
| | | Tower | +sight of surroundings; +range for units on top |
| | | Underground/underwater sensors, radar | |
| | Decrease | Signal jammer, hidden bunker | |
| | | Camouflage | Makes a unit on the tile less likely to be seen |
| DEFENSE | Defensive bonus | Trenches, bunker | Defence against ranged units |
| DETONATION | Detonation points | Claymore | |
| | | Oil bags | Ignitable by archers; lights up the tile (chance of fire spread?) |
| ACTIVE ATTACK | Active weapons | Catapult, artillery, turret | |
| EXPERIENCE | +experience | Command tent | +experience points for defenders |
| TERRAIN | Change terrain | Clear forest (preserve resource), despoil forest (destroy resource), desecrate land (poison/unusable), scorched earth, clear rocks | |
| MORALE | Damage morale | Speaker system | |

**Persistence:** fortifications built *during* combat do **not** remain after the battle. Any
*destruction* of pre-existing fortifications **is** kept after a battle.

## How a battle works

### Starting positions — defender

Depends on what the defender was doing:

- **Resting** → placed in a random zone (a contiguous set of tiles); the player then places each
  unit onto those tiles (one unit per tile).
- **Fortifying** → starts in the predefined positions chosen earlier.
- **Ambush** → sees where the assaulting units are randomly placed, *then* sets unit positions.
- **Hiding** → places units *before* seeing where the assaulting units are.

### Starting positions — assaulter

Placed in a zone along the side of the battle map they entered the campaign tile from. Each zone is
big enough for every unit. Certain advances/settings let a player shuffle units along their edge
before the battle begins.

### Starting positions — interim placement

What is built today (`BattleDeploymentPlanner`), standing in for the two sections above until a
placement step exists. The defender's stance isn't modelled, and nobody chooses where their units go.

- The **attacker** starts on the edge it entered from: the edge behind the direction it stepped.
  Stepping North means it starts on the south edge.
- The **defenders** start on the opposite edge.
- A **diagonal** step came in through a corner. The attacker fills the north or south edge next to
  that corner, starting from the corner end, and the defenders fill the opposite edge from the
  diagonally opposite corner.
- Along a cardinal edge, units spread **outwards from the centre**.
- One unit per tile. The 2×2 corner voids are never used, and when an edge fills up the zone moves
  one row further in. A zone never reaches the middle of the map, so the two sides can't share a tile.
- Whether a tile can actually be stood on (a land unit on a lake, say) isn't considered yet.

### Battle flow

The battle is divided into **rounds**, executed in real time:

- A round is where a player moves all their units. Players alternate, **starting with the
  defender**, moving one unit at a time.
- How often a unit moves in a round, and when its movement is restored, is in
  [unit-movement.md](units/unit-movement.md#rounds).
- A battle lasts at most **X rounds** (suggested: 2× the turns it takes the slowest unit to cross
  the map). At round `X − 3`, the **defender is given perfect vision** of all enemy units.

## Battle objectives

When Empire A moves onto a tile with Empire B, one of the following must happen:

- **≥ 85% of one side is destroyed** — the first side to do this wins.
- **One side's survivors reach the opposite edge** (from where they started) without destroying 85%
  of the enemy — the attacker wins; the units on the edge are "safe" and pass through to the
  adjacent campaign tile (which may trigger a new battle).
- **The maximum number of battle rounds is exceeded** — the **defender wins**; surviving enemy units
  are repulsed to the tile they came from.
- **The attacker retreats** (via a button) — 30% of the remaining force, at random, is killed.
- **The defender retreats** — the attacker wins; on the following campaign turn the attacker may
  destroy all fortifications.

## Battle manoeuvres

- **Tuxedo** — significantly reduces missile damage; slows movement.
- **Porcupine (phalanx)** — attack bonus against cavalry.
- **Shield wall**.
