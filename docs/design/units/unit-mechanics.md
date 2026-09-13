# Unit Mechanics

Covers how units face, see, and attack. For how units move, see [unit-movement.md](unit-movement.md); for how
Units are grouped into Commands, see [military-organisation.md](military-organisation.md); for how a unit is
designed from components, see [unit-design-system.md](../unit-design/unit-design-system.md).

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
