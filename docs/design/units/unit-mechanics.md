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

- Battle-map movement is **derived, not doubled**: `BattlefieldMovementPoints = MAX(0, Body.Power −
  SUM(weight of fitted components))`, computed at compile time by `BattleMovementCalculator` and stored on
  `CompiledUnitTemplate.BattlefieldMovementPoints` (UD-52). It used to be specified as double the campaign
  figure, and a lightly-loaded unit still tends to land near that, but the doubling is not the rule.
- Campaign-map movement bonuses/penalties also apply on the battle map.
- **Ascending altitude** (land units): +25% movement points for a moderate incline, +50% for a steep
  incline. **Descending** a steep incline: +25%.

### Calculation rules

1. A unit cannot move without a supply of **FUEL** (if applicable). Fuel used is the same regardless
   of movement bonuses, penalties, or terrain.
2. A unit may enter any tile its movement domain permits **whenever it holds any movement points at
   all**. The tile's cost is what the unit *pays*, not what it must be holding first: the whole cost
   comes off on entry and the balance is allowed to go negative. A slow unit therefore crosses
   expensive ground by going into debt and waiting it out, rather than being permanently barred from
   ground whose cost exceeds its whole allowance.
   - How long a tile holds a unit is
     `TurnsHeld = cost ≥ remaining ? floor((cost − remaining) ÷ allowance) + 1 : 0` — the number of
     turn-ends it spends standing there before it can leave. It is not strict on `remaining`, because
     landing on exactly 0 strands the unit for a turn just as debt does. This is the single count the
     step preview and the supply projection are both meant to read.
   - The one bound on the above is **stranding**: a step is refused when `TurnsHeld` would exceed
     `MAX_STRANDED_TURNS` (currently 6). That is the honest form of what the old ≥ 50% threshold was
     reaching for — a limit expressed in turns, which is the cost the player actually bears, rather
     than as a proportion of the tile that a slow unit could never reach whatever it did. It refuses
     nothing under the authored terrain, where the dearest tile is Hilly Jungle at 4, and exists to
     bound a future high-cost biome.
3. Movement points left over at the end of a turn are **carried** into the next one, up to a maximum of
   **1** point. There is no separate variable holding the carry: what the unit is still holding when its
   turn opens *is* the carry.
4. At the start of the **owning player's turn** — not at the start of the round — the unit is given its
   full template allowance plus that carry. In detail:
   - if the leftover is **at or above** the allowance the unit banks nothing and resets to the plain
     allowance, so skipping a move cannot compound turn on turn;
   - if the leftover is **above 1** but below the allowance, exactly 1 point carries;
   - otherwise the leftover carries in full — which covers a fractional remainder, and covers **debt**,
     which is uncapped so an overspent unit pays it back rather than having it forgiven.
5. A unit that does not move on the campaign map does **not** expend fuel; a unit on the battle map
   expends fuel regardless of whether (or how much) it moves.
6. If a campaign tile has a detected enemy unit, or fortifications that would damage or fully block
   movement, the **battle map is triggered** when the unit attempts to enter.

### Scenarios

`MP` = unit movement points, `MC` = movement cost of the geography.

`Held` = the `TurnsHeld` count from rule 2.

| Scenario | Walkthrough |
| --- | --- |
| Warrior onto Plain (MP=1, MC=1) | T1: enters on whatever it holds; MP after `= 0`. T2: MP `= 1` (allowance + nothing carried) and it moves. **Held 1.** |
| Warrior onto Plain+Forest (MP=1, MC=2) | T1: MP after `= −1`. T2: MP `= 0`, no move. T3: MP `= 1`, moves. **Held 2.** |
| Warrior onto Flat Jungle (MP=1, MC=3) | T1: MP after `= −2`. Moves again on T4. **Held 3.** The old ≥ 50% rule wanted 1.5 points, which a 1 MP unit could never be holding, so this tile was unreachable for ever. |
| Warrior onto Hilly Jungle (MP=1, MC=4) | T1: MP after `= −3`, then `−2`, `−1`, `0`; moves again on T5. **Held 4.** The dearest tile the authored terrain can produce, and refused outright under the old rule. |
| Cavalry onto Plain (MP=2, MC=1) | T1: MP after `= 1` — can move another tile, so the tile costs it no time. **Held 0.** T2: MP `= 2`. |
| Cavalry onto Forest (MP=2, MC=2) | T1: MP after `= 0`. **Held 1.** T2: MP `= 2`, can move. |
| Cavalry (MP=2.2) into Hilly Jungle from a full allowance | T1: MP after `= −1.8`. T2 opens on `0.4` and it may move. **Held 1.** |
| The same Cavalry, but holding only 0.2 when it enters | T1: MP after `= −3.8`. T2 opens on `−1.6` and cannot move; T3 opens on `0.6`. **Held 2** — the price of a permissive gate. |
| Cavalry +10% bonus (MP=2.2) spending two whole tiles a turn | The 0.2 it cannot spend carries, so the unit opens on 2.2, 2.4, 2.6, 2.8, then **3.0** — a third move on turn 5 — and starts over at 2.2. This is what makes a fractional allowance worth having; it demonstrates rules 3–4 across several turns. |
| The same Cavalry, but it skips a turn | It ends the turn holding all 2.2, which is at its allowance, so it banks nothing and simply opens the next turn on 2.2. Sitting still does not accumulate movement. |
| The same Cavalry, stranded on 1.5 by a tile it cannot afford | The carry is capped at 1, so it opens the next turn on `2.2 + 1 = 3.2`, not 3.7. |
| A unit that ended the turn 1.0 in debt | Debt is uncapped and carries in full: it opens on `allowance − 1.0`. |
| Warrior on Plain with road (MP=1, MC=1) | Road halves MC to `0.5`, so the unit makes two moves in one turn (MP after move 1 `= 0.5`, after move 2 `= 0`). The first move holds it not at all; the second lands it on 0. **Held 0, then 1.** |

### Preserving movement points

A player may take a partial move or skip a move. On a **partial** move the leftover carries into the
next turn, up to a maximum of 1 point. On a **skipped** move — where the unit is still holding its whole
allowance — nothing carries: the unit simply opens the next turn on the plain allowance again.

The carry is not a stored variable. It is whatever the unit still has when its turn opens, which is why
there is no separate column for it and no end-of-turn banking step. Note that "Strength" below means the
combat/build figure (`Healthy + Injured × 50%`) and has nothing to do with movement — the two used to
share a name in this document, and no longer do.

### When movement is replenished

At the start of the **owning player's turn**, not at the start of the round, and **after supply has been
reckoned** — a unit whose body requires fuel it does not have gets no campaign movement at all that turn.
In code this is the `ReplenishMovementPoints` process in the start-of-campaign-turn pipeline, ordered
after `CheckUnitSupply` for exactly that reason.

Battle movement is held in its own pool, so a unit drawn into a battle keeps the campaign movement it will
need once the battle releases it. **Nothing replenishes the battle pool yet**: it is granted when the unit
is fielded and never refreshed, because battle rounds are not campaign rounds and the battle turn pipeline
has no processes to hang the refresh on.

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
