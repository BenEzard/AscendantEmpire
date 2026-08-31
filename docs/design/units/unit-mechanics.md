# Unit Mechanics

Covers how units move, face, see, and attack. For how Units are grouped into Commands, see
[military-organisation.md](military-organisation.md); for how a unit is designed from components, see
[unit-design-system.md](../unit-design/unit-design-system.md).

## Movement

Definitions:

- **Full move** — the unit expends all of its movement points.
- **Partial move** — some movement points remain at the end of the turn.
- **Skipped move** — none of the unit's movement points are expended.
- **Movement bonus** — a beneficial terrain improvement (road, railroad) or technology that lets the
  unit move further.
- **Movement penalty** — an obstacle that slows movement (wooded or swampy areas).
- **Movement sap** — an obstacle that, once crossed, consumes *all* remaining movement points for
  the turn regardless of how many there were (e.g. a river in the early game).
- **Movement obstacle** — something that blocks movement entirely, by its nature (e.g. mountain) or
  by an aspect of the unit (e.g. chariots can't enter swamps without a road).

### On the campaign map

- A Command moves at the speed of its **slowest** Unit.
- Where a member of a Command cannot enter a given tile, the player can **separate** the incapable
  Units from the Command.
- Health damage does **not** affect a unit's speed.

### On the battlefield map

- Movement on the battle map is **double** that on the campaign map.
- Campaign-map movement bonuses/penalties also apply on the battle map.
- **Ascending altitude** (land units): +25% movement points for a moderate incline, +50% for a steep
  incline. **Descending** a steep incline: +25%.

### Calculation rules

1. A unit cannot move without a supply of **FUEL** (if applicable). Fuel used is the same regardless
   of movement bonuses, penalties, or terrain.
2. A unit cannot enter a tile until it has **≥ 50%** of the movement points required to cross it.
3. If movement points are not 0 at end of turn, the remainder goes into a **Strength** variable (up
   to a max of 1).
4. At the start of the turn, any Strength is applied to movement points.
5. A unit that does not move on the campaign map does **not** expend fuel; a unit on the battle map
   expends fuel regardless of whether (or how much) it moves.
6. If a campaign tile has a detected enemy unit, or fortifications that would damage or fully block
   movement, the **battle map is triggered** when the unit attempts to enter.

### Scenarios

`MP` = unit movement points, `MC` = movement cost of the geography.

| Scenario | Walkthrough |
| --- | --- |
| Warrior onto Plain (MP=1, MC=1) | T1: required to enter `MC×0.5 = 0.5` (enter); MP after `= 0`. T2: MP `= 1` (`++MP + strength`). |
| Warrior onto Plain+Forest (MP=1, MC=2) | T1: required `1` (enter); MP after `= −1`. T2: MP `= 0`, no move. T3: MP `= 1`, moves. |
| Cavalry onto Plain (MP=2, MC=1) | T1: required `0.5` (enter); MP after `= 1` — can move another tile. T2: MP `= 2`. |
| Cavalry onto Forest (MP=2, MC=2) | T1: required `1` (enter); MP after `= 0`. T2: MP `= 2`, can move. |
| Cavalry +10% bonus (MP=2.2) over Forest, Forest, Forest, Plain | Strength accumulates the leftover each turn (0.2, 0.4, 0.6…), eventually banking enough to make multiple moves in a later turn. Demonstrates Rules 2–4 across several turns. |
| Warrior on Plain with road (MP=1, MC=1) | Road halves MC to `0.5`. Required to enter `0.25`; the unit makes two moves in one turn (MP after move 1 `= 0.5`, after move 2 `= 0`). |

### Preserving movement points

A player may take a partial move or skip a move. On a partial move, the leftover movement points go
to the **Strength** value (max 1). This makes the unit slightly more effective in combat and may
increase its chance of sapping or being able to retreat.

## Positioning (facing)

A unit facing one direction that is attacked from another side suffers a **defensive penalty**.

- Changing a unit's facing does **not** use movement points, but must be done before the end of the
  round.
- A unit **retains its facing** regardless of how it moves (e.g. a unit facing left that moves down
  a tile is still facing left).
- The UI must make it easy to move *all selected units* as a group.
- Some units (cavalry, artillery) have attacks that can **push** defenders off their
  position/facing, making them vulnerable or breaking through enemy lines.

### Tactics

When a unit is attacked, the **9 tiles around it** are analysed. Enemy units on sides *other than*
the attacker's confer additional defensive penalties:

- Each additional enemy unit reduces defensive value (the source notes roughly −5% per enemy above).
- When a unit is attacked **and has enemies opposite the attacker**, the defender receives a **20%
  defensive penalty**.

## Vision: fog of war

There is fog of war over most of the battle map. (Exception: no fog of war for ocean or coastal,
unless land impedes the view.)

- When the battle tile is within their own territory, defenders get **perfect vision initially**
  (the whole battle map). As the enemy moves in, that vision is lost on those tiles — e.g. if a unit
  moves onto A1 then A2, the defender can't see what else subsequently moved onto those tiles unless
  within its normal field of vision.
- Specialists attached to a Force can affect vision and other stats.
- Units **in the same Command share vision**. Units that are not in a Command see only their own
  vision. (E.g. an Egyptian cavalry unit on a hilltop sees two enemies in the valley; when the turn
  switches to an *unaffiliated* Egyptian infantry, that infantry can't see where those enemies were
  — the player may remember, but it's harder.)

## Strength

A unit's **Strength** is how much effort it can put into building or combat. (Movement is excluded —
it has its own calculation.)

```
Strength = number of healthy combatants + (number of wounded combatants × 50%)
```

## Ranged attack

With the exception of aerial attacks, artillery, and ICBMs (which can occur on the campaign map),
all ranged weaponry is used on the **battle map only**.

Range is expressed in **battle-map tiles** (a range of 10 = 10 battle tiles). A `c` suffix means
**campaign tiles** (`1c` is the neighbouring tile, `2c` the neighbour's neighbour). A weapon with
range `1c` can fire anywhere within a battle map.

### On the battle map

- Weapons like a bow, sniper rifle, or mortar can attack an enemy unit (even when not assaulting the
  tile) on the attacker's turn.
- Range is multiplied (roughly ×5) versus the campaign map. Higher altitude slightly increases
  range.

### Open questions

- Should a weapon have a **minimum** range (e.g. artillery)? Part of the component system, or a
  modifier on a few weapons only?
- Should a ranged unit get a "free shot" when an attacker enters its tile (defending), and/or when
  attacking? (Leaning yes, except for ambush.)
- Can a ranged unit fire multiple times at an incoming unit (on its own or the enemy's turn)?
- When can a unit retreat / how committed is an attack? (Likely depends on the power differential —
  a much stronger unit should simply obliterate the weaker one.)
