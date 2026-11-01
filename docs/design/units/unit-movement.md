# Unit Movement

The source of truth for how Units move, on both the campaign map and the battlefield map: the rules, how
they are stored, and where the code enforces them. Other documents link here rather than restating any of it.

Each rule is marked as either **built** (enforced by the code named beside it) or **design** (intended, not
yet enforced). For what a Force *is*, see [military-organisation.md](military-organisation.md); for how a
template's movement stats are assembled, see [unit-design-system.md](../unit-design/unit-design-system.md).

## Shared foundations

### Two pools

A Unit holds two separate movement pools on `units.UnitInstances`:

| Column | Spent on | Allowance from |
|---|---|---|
| `CampaignMovementRemaining` | The campaign map | `CompiledUnitTemplate.CampaignMovementPoints` |
| `BattleMovementRemaining` | A battlefield map | `CompiledUnitTemplate.BattlefieldMovementPoints` |

They are kept apart because they are spent on different maps against different costs, and a Unit drawn into a
Battle still holds the campaign movement it will need once the Battle releases it.

### Representation

Every movement value — both pools and both allowances — is a raw fixed-point `long` on the Scale 1,000
convention of [`ScaledAmount`](../../../AscendantEmpire.Application/Helpers/ScaledNumerics/ScaledAmount.cs): a
stored `1000` means 1.0 MP. This matches every other movement, power and hitpoint value in the system, so a
pool and its allowance compare directly with no conversion.

- **"None left" needs no conversion.** `<= 0` / `> 0` hold at any scale.
- **"One whole point" is `ScaledAmount.Scale`.**
- **Decimal MP exists only at the API boundary.** The read handlers (`GetPlayerForcesHandler`,
  `GetForcesByLocationHandler`, the campaign map read) and `StepForceHandler`'s result convert raw values to
  decimal MP, so no client needs to know the convention.

### Creation

A new Unit starts with **both pools full**: `CampaignMovementRemaining` set to its template's
`CampaignMovementPoints`, and `BattleMovementRemaining` to its `BattlefieldMovementPoints`. `SpawnUnitHandler`
does this for a spawned Unit, and `GameSetupService` for each player's starting Units.

### Movement domain

A Unit's `DomainType` — Land, Water, BelowWater, Air, NearEarthOrbit — comes from its body component and is
copied onto the compiled template. It decides which terrain the Unit may enter; see
[Entering a tile](#entering-a-tile--built).

### Vocabulary

- **Full move** — the Unit expends all of its movement points.
- **Partial move** — some movement points remain at the end of the turn.
- **Skipped move** — none of the Unit's movement points are expended.
- **Movement bonus** — a beneficial terrain improvement (road, railroad) or technology that lets the Unit move
  further.
- **Movement penalty** — an obstacle that slows movement (wooded or swampy areas).
- **Movement sap** — an obstacle that, once crossed, consumes *all* remaining movement points for the turn
  regardless of how many there were. Nothing is a sap today.
- **Movement obstacle** — something that blocks movement entirely, by its nature (e.g. a mountain) or by an
  aspect of the Unit (e.g. chariots cannot enter swamps without a road).

---

## Campaign Movement

### The allowance

A template's campaign allowance is **authored** on its body component (`UnitComponentBody.CampaignMovementPoints`)
and copied unchanged onto `CompiledUnitTemplate.CampaignMovementPoints` by `CompileUnitTemplateHandler`. A human
body is authored at `1000` (1.0 MP).

Health damage does **not** affect a Unit's speed.

### Stepping a Force — built

A Force moves on the campaign map one tile at a time, through `POST /force-orders/step` (`StepForceHandler`).
The request names the Force and one of eight compass directions; the server resolves the neighbour against the
Force's current tile, so a client can never name a distant destination. On a map that wraps, stepping off one
edge arrives at the other (`MapDirections.TryGetNeighbour`); on one that does not, there is no tile that way. A
diagonal step costs the same as an orthogonal one, apart from the rivers it crosses (see [Rivers](#rivers--built)).

In the client, the player steps the selected Force with the **numpad** (`useNumpadMovement`) or by **holding**
one of the eight tiles around it (`GameUI`'s `onTileHold`). Only one Force moves at a time: with several
selected, the client says so rather than moving any. Holding a tile further away says it is out of reach.

A step is refused, in this order, when:

1. the Force does not exist, or belongs to another player;
2. it has no troops left;
3. it is standing on a battlefield map;
4. it holds **no campaign movement** (`CampaignMovementRemaining <= 0`) — see [The entry gate](#the-entry-gate--built);
5. it is not this player's turn, or the turn has no open `PlayerTurn` row to record the step against;
6. the step runs off a non-wrapping edge of the map;
7. the tile cannot be entered, or the step cannot be taken because of a river — see
   [Entering a tile](#entering-a-tile--built) and [Rivers](#rivers--built);
8. the step would strand the Force for too long — see [Stranding](#stranding--built).

A step that passes is paid for, recorded and answered:

- The step's **whole** cost — the tile's cost adjusted for any river crossed or followed — comes off
  `CampaignMovementRemaining`, and the balance may go negative.
- The new position goes through `IForceLifecycleWriter.MoveAsync`, which stages the tile change and a `Moved`
  row in `units.MovementHistory` together; `UnitInstance.CampaignMapTileId` is init-only, so nothing else can
  move a Force. See [intelligence-and-sightings.md](../intelligence-and-sightings.md).
- The response carries the Force's new tile, remaining movement and allowance, the rivers the step crossed and
  ran along (`RiverCrossed`, `RiverAlong`), plus a re-run of the fog-of-war read, so the client redraws what the
  player can now see.

### Entering a tile — built

`MovementEntryRules.GetEntryRefusal` is the single answer to "may a Force of this domain enter this tile?". The
fog-of-war read asks it too — an observer blinded by dense terrain reveals exactly the neighbours its domain
could never have entered (see [campaign-map/vision.md](../campaign-map/vision.md#standing-in-dense-terrain)) — so what the map
reveals and what a step refuses can never disagree.

**Impassable tiles are refused for every domain**, before any domain rule is consulted:

- a geography with `IsTraversable = false` — only **Mountainous** today, refused as "Mountain terrain cannot be entered.";
- a biome with `IsTraversable = false` — only **Void** today.

The ground and what is painted on it each answer for themselves, and either one is enough. The one rule that
combines them is `CampaignTileImpassability`, which the per-Round territory pass reads too, so the ground a Force
is refused is exactly the ground that seals an enclosure ring. **Movement cost does not decide this**:
`CampaignGeography.IMPASSABLE_MOVEMENT_COST` (999) is the cost an untraversable geography carries as data, and the
geography validator holds the two in step, but nothing reads the cost to answer the question.

**Then the domain decides**, through one `IMovementDomainStrategy` per `DomainType`:

| Domain | May enter | Notes |
|---|---|---|
| Land | Dry ground only | Water is refused outright; crossing needs a ford, bridge or transport, none of which exist. A step may not cross a Large river. |
| Water | Water only | As a step, may also sail up a Large river onto bank tiles (see [Rivers](#rivers--built)). Beaching onto an ordinary coast will be a landing action, not a move. |
| BelowWater | Water only | Same as Water, rivers included. Shallow water and depth are undesigned. |

The table is the **tile** rule, which fog of war shares. A step is judged by `MovementStepRules.GetStepRefusal`,
which applies the same impassability check and then asks the domain's `GetStepRefusal`. That differs from the tile
rule only where rivers are concerned, so a blinded observer's reveal is unaffected by them.
| Air | Anything passable | Still pays the tile's cost, and still cannot cross a mountain. |
| NearEarthOrbit | Anything passable | Behaves as Air until orbital movement is designed. |

A tile is **water** when either its biome is (`CampaignBiome.IsWater` — Coastal, Ocean) or its geography is
DeepWater or ShallowWater, so a hand-edited map that sets only one still refuses a land Force.

### Tile cost — built

```
MovementCost = Geography.MovementCost + (Biome.MovementCost − 1)
```

The geography is the ground and the biome modifies it, so a biome contributes only its excess over the neutral
cost of 1 (`MovementTargetTile.MovementCost`). The seeded values:

| Geography | Cost | | Biome | Cost |
|---|---|---|---|---|
| Flat | 1 | | Grassland, Arid, Ice, Tundra, Woodland, Coastal, Ocean | 1 |
| Hilly | 2 | | Desert | 1.1 |
| Deep Water, Shallow Water | 1 | | Forest | 2 |
| Mountainous | 999 (not traversable) | | Jungle | 3 |
| | | | Void | 99 (not traversable) |

So Flat Grassland costs 1, Flat Desert 1.1, Hilly Forest 3, and **Hilly Jungle 4** — the dearest passable tile
the seeded terrain can produce.

What a **step** costs is decided by the Force's domain strategy (`GetStepCost`): the tile cost for Air and orbit, the
tile cost adjusted for rivers for Land, and a flat sailing cost for a ship on a river. See [Rivers](#rivers--built).

### Rivers — built

Rivers run **between** tiles, along their edges. Each river edge is **Medium** or **Large**. A tile stores only its
South and East edges (`SouthRiver`, `EastRiver`), so each edge is stored once; see
[tiles.md](../campaign-map/tiles.md#rivers) for the model.

**Geometry** (`RiverMovementRules.Evaluate`, over a `RiverEdges` snapshot of the 3×3 block around the Force):

- An **orthogonal** step **crosses** the edge the two tiles share.
- A **diagonal** step from A to D, with B beside A horizontally and C beside it vertically, crosses only when the
  river separates A from D, i.e. when both ways round the corner cross it. The width crossed is
  `min(max(A|B, B|D), max(A|C, C|D))`, so a river that merely ends at the corner is never crossed, and the Force
  takes the easier way.
- An **orthogonal** step runs **along** a river when, on one side, both tiles have a river on their edge parallel
  to the step. The run is as wide as its narrower half; if both sides qualify, the wider wins. A diagonal step never
  runs along a river.

**What each domain makes of it:**

| Domain | Crossing Medium | Crossing Large | Along |
|---|---|---|---|
| Land | +1 (`MEDIUM_RIVER_CROSSING_COST`) | **Refused**: "Cannot cross a large river." | −0.25 (Medium), −0.5 (Large) |
| Water, BelowWater | no effect | no effect | May sail up a Large river (below) |
| Air, NearEarthOrbit | ignored | ignored | ignored |

A Land step costs `max(MINIMUM_STEP_COST, tile cost + crossing − along)`, with `MINIMUM_STEP_COST` at 0.25. A step can
both cross a tributary and run along a main river; both apply. The constants are on `RiverMovementRules`.

**Sailing up Large rivers.** A **bank tile** is a land tile with a Large river on one of its edges. A ship may stand on
one, reached and left by these steps only (`RiverMovementRules.GetSailingRefusal`):

- **Water → land**, orthogonally, onto a tile with a Large river on a side parallel to the step — a river mouth.
- **Land → land**, orthogonally, along a continuous Large river.
- **Land → water**, orthogonally, always, so a ship can never be stranded on a river.
- No diagonal step may involve land.

A step onto a bank tile costs a flat `RIVER_SAILING_COST` (0.5), ignoring the land's terrain. Steps onto water pay
the water tile's cost.

**Not built:** bridges, fords and ferries, which will make a Large river crossable for Land Forces. Rivers are
authored in the map editor (ME-44), drawn on the campaign map (CM-53) and generated (ME-45) in later tasks; until
then a river exists only where the columns are set directly. Rivers cut supply (see
[unit-support-and-supply.md](unit-support-and-supply.md#supply-chains)), which waits on the supply system.

### The entry gate — built

A Force may enter any tile its domain permits **whenever it holds any movement at all**. The tile's cost is
what the Force *pays*, not what it must be holding first: the whole cost comes off on entry and the balance may
go negative. A slow Force therefore crosses expensive ground by going into debt and waiting it out, rather than
being permanently barred from ground whose cost exceeds its whole allowance.

A Force sitting on zero, or still in debt, may enter nothing.

### Stranding — built

How long a tile holds a Force is `MovementTargetTile.TurnsHeld`:

```
TurnsHeld = stepCost < remaining ? 0 : floor((stepCost − remaining) ÷ allowance) + 1
```

`stepCost` is the step's river-inclusive cost, the same figure the Force is charged. `TurnsHeld` is the number of turn-ends the Force spends standing there before it can leave, and is exactly the turn on
which [replenishment](#replenishment--built) brings the balance back above zero. The comparison is not strict
on `remaining`, because landing on exactly 0 strands a Force for a turn just as debt does. A Force with no
allowance is held for ever (`int.MaxValue`).

`StepForceHandler` refuses a step whose `TurnsHeld` would exceed **`_MAX_STRANDED_TURNS` (6)**. That refuses
nothing under the seeded terrain — Hilly Jungle holds even a 1 MP Force for 4 turns — and exists to bound a
future high-cost biome. It is kept out of `MovementEntryRules` deliberately: a rule that varied with how much
movement a Force held would make revealed terrain appear and disappear from the map as the Force tired.

`TurnsHeld` is the single count the step preview and the supply projection are meant to read. Neither exists
yet.

### Carry — built

Movement left over at the end of a turn is **carried** into the next, up to a maximum of **1** point. There is
no separate carry column: what the Force is still holding when its turn opens *is* the carry.

- On a **partial** move, the leftover carries, capped at 1.
- On a **skipped** move — the Force still holds its whole allowance — nothing carries, and it opens the next
  turn on the plain allowance. Sitting still does not accumulate movement.
- **Debt** carries in full and uncapped, so an overspent Force pays it back rather than having it forgiven.

A Force may therefore legitimately sit **above** its allowance (a carry on top of it) or **below zero** (debt).
Neither is an error.

### Replenishment — built

At the start of the **owning player's turn** — not the start of the round — every one of that player's Forces is
given its full allowance plus its carry. `MovementReplenishment.ReplenishCampaignMovement` is the rule, a pure
static pinned without a database:

| Leftover when the turn opens | Carry |
|---|---|
| At or above the allowance | 0 — resets to the plain allowance |
| Above 1, below the allowance | Exactly 1 |
| Anything else (a fraction, exactly 1, zero, or debt) | The leftover, in full |

`ReplenishMovementPoints` is the start-of-campaign-turn process that applies it:

- It runs **last** in the start-of-turn pipeline — `AlertSentries` (10,000), `CheckUnitSupply` (20,000),
  `ReplenishMovementPoints` (30,000) — so a supply verdict exists before any allowance is handed out. See
  [Fuel](#fuel).
- Forces standing on a battlefield map **are** replenished, so a Force does not emerge from a long Battle frozen
  in place. Wiped-out Forces are not.
- `UnitInstances.CampaignMovementReplenishedOnRound` makes it run **once per Round**. That stamp is load-bearing:
  a second run in one Round would see a leftover at or above the allowance, bank nothing, and destroy the carry
  the first run granted.
- A Force whose template has no compiled snapshot is skipped and left unstamped, so it is retried next Round,
  rather than failing the pipeline and blocking the player's turn.
- `UnitInstance` has no row version, so two overlapping runs for one player would both write. Campaign turns are
  handed over one at a time, so this is not reachable today.

Replenishing per player keeps the carry alive through the other players' turns, and computes the allowance
against state that is true at the moment it is spent.

### Scenarios

`MP` = the Unit's movement points, `MC` = the tile's movement cost, `Held` = its `TurnsHeld`.

| Scenario | Walkthrough |
| --- | --- |
| Warrior onto Plain (MP=1, MC=1) | T1: MP after `= 0`. T2: MP `= 1` and it moves. **Held 1.** |
| Warrior onto Plain+Forest (MP=1, MC=2) | T1: MP after `= −1`. T2: MP `= 0`, no move. T3: MP `= 1`, moves. **Held 2.** |
| Warrior onto Flat Jungle (MP=1, MC=3) | T1: MP after `= −2`. Moves again on T4. **Held 3.** |
| Warrior onto Hilly Jungle (MP=1, MC=4) | T1: MP after `= −3`, then `−2`, `−1`, `0`; moves again on T5. **Held 4.** The dearest seeded tile. |
| Cavalry onto Plain (MP=2, MC=1) | T1: MP after `= 1` — can move another tile. **Held 0.** T2: MP `= 2`. |
| Cavalry onto Forest (MP=2, MC=2) | T1: MP after `= 0`. **Held 1.** T2: MP `= 2`, can move. |
| Cavalry (MP=2.2) into Hilly Jungle from a full allowance | T1: MP after `= −1.8`. T2 opens on `0.4` and it may move. **Held 1.** |
| The same Cavalry, holding only 0.2 when it enters | T1: MP after `= −3.8`. T2 opens on `−1.6` and cannot move; T3 opens on `0.6`. **Held 2** — the price of a permissive gate. |
| Cavalry (MP=2.2) spending two whole tiles a turn | The 0.2 it cannot spend carries, so it opens on 2.2, 2.4, 2.6, 2.8, then **3.0** — a third move on turn 5 — and starts over at 2.2. This is what makes a fractional allowance worth having. |
| The same Cavalry, skipping a turn | It ends the turn holding all 2.2, which is at its allowance, so it banks nothing and opens the next turn on 2.2. |
| The same Cavalry, stranded on 1.5 by a tile it cannot afford | The carry is capped at 1, so it opens on `2.2 + 1 = 3.2`, not 3.7. |
| A Unit that ended the turn 1.0 in debt | Debt carries in full: it opens on `allowance − 1.0`. |
| Warrior on Plain with road (MP=1, MC=1) — *design* | A road halves MC to `0.5`, so the Unit makes two moves in one turn (MP after move 1 `= 0.5`, after move 2 `= 0`). **Held 0, then 1.** Roads are not built. |

### Fuel

**Design, not enforced.** A body component requires **0 or 1** fuel type to power its movement. Supply is
reckoned by `CheckUnitSupply` at the start of the owning player's turn, before replenishment, and:

- a Unit whose body requires fuel it does not have receives **no campaign movement at all** that turn — its
  remaining movement is set to zero, not to the allowance and not to the carry;
- fuel used is the same regardless of movement bonuses, penalties or terrain;
- a Unit that does not move on the campaign map expends no fuel;
- when a Command has fuel for some but not all of its Units, it either stays put or leaves the unsupplied Units
  behind.

`CheckUnitSupply` is a stub and there is no asset system for it to read, so no Unit is denied movement for
want of fuel. The ordering that makes the rule implementable is in place and pinned by a test, and the rule is
recorded as a `TODO` on `ReplenishMovementPoints`. See [unit-support-and-supply.md](unit-support-and-supply.md)
for supply chains and the other support types.

### Commands

**Design.** A Command moves at the speed of its **slowest** Unit — `MIN(members)`, derived on read rather than
stored. Where a member cannot enter a tile, the player can **separate** the incapable Units from the Command.

Nothing can form a Command yet, so every Force that moves is a single Unit.

### Movement modifiers

**Not applied.** No unit bonus, penalty or modifier affects any step today. The tile cost, adjusted for
[rivers](#rivers--built), is the whole price.

Nothing reads modifier data. The `MoveModifier` enum names the intended bonuses (`JungleCrasher`,
`ForestWalker`, `RiverRunner`, `Amphibious`, `IgnoreHillsPenalty`, `RoadAffinity`, `RailAffinity`,
`MagTubeAffinity`), and the legacy `CompiledDesignStats.ModifiersJson` column holds modifiers as JSON, but
`CompiledUnitTemplate` has no equivalent, nothing parses the JSON, and there is no authoring UI for movement
modifiers.

**Design.** Campaign movement bonuses and penalties also apply on the battlefield map.

### Battles on entry

**Partly built.** A step onto a campaign tile holding another player's Forces starts a Battle
(`StepForceHandler`, through `IBattleEngagementService`):

- The Force pays for the step and moves onto the tile exactly as an ordinary step would.
- The tile's battle map is generated if it has never had one, and reused if it has. If it can't be generated, the
  step is refused and nothing moves.
- The attacker and every enemy Force on the tile are placed on battle map tiles (`BattleMapTileId`), which is
  what marks them as in battle — a Force in battle is refused any further campaign step. See
  [battlefield-maps.md](../battlefield-maps.md#starting-positions--interim-placement) for where they are placed.
- A `battle.Battle` is commenced with one participant per player, attacker first, and its history is recorded,
  all in the same transaction as the move.
- A step onto a tile that already has an open Battle is refused. Joining it instead is COMM-4.

Not built: every Force is assumed to see every other, so detection, ambush and hiding play no part; there is no
alliance model, so any other player's Force is an enemy; fortifications don't trigger anything; and no battle
turn queue is created (COMM-3). See [battlefield-maps.md](../battlefield-maps.md#triggering-a-battle).

### Multi-turn move orders

**Not built.** `POST /force-orders/move` (`MoveForceHandler`) resolves the destination tile and then returns
success without doing anything. `PathFinder` — a Dijkstra search over the eight-way grid with wrapping and
per-edge boundary costs — is not registered in DI and has no production caller. `units.ForceOrders` and
`units.ForceOrderMovementSteps` hold the tables such an order would use.

**Design.** Players may plan moves in advance while it is not their turn; when their turn starts, if no adverse
changes are detected, the pre-programmed moves are actioned automatically. See
[turn-structure.md](../turn-structure.md).

### What the client is told — built

The campaign map read (`PlayerForce` → `ObservedForce` → `IForceStub` / `DTForceStub`) carries, per Force:

| Field | Type | Meaning |
|---|---|---|
| `HasCampaignMovementRemaining` | `bool?` | `CampaignMovementRemaining > 0` |
| `CampaignMovementRemaining` | `decimal?` | What is left, in decimal MP. May be negative or above the allowance. |
| `CampaignMovementPoints` | `decimal?` | The allowance, as the denominator for the figure above. |
| `BattleMovementRemaining` | `decimal?` | The battle pool, in decimal MP. |

**All four are `null` for a Force the viewer does not own.** How far an enemy can still move is intel the vision
rules never granted; seeing that a Force is standing there is one thing, knowing whether it can still act is
another. The client normalizer defaults a missing `hasCampaignMovementRemaining` to `true`, so nothing is faded
on no evidence, and leaves the numbers `null`, so nothing is shown.

The boolean is a display hint, never authorisation: whether a Force may act is decided server-side against a
freshly built context.

The client uses these in three places:

- The [force selection bar](force-selection-toolbar.md) fades a Force with no movement left (it stays clickable),
  sorts it after those that still have some, and shows an `X / Y` figure. The numerator can legitimately exceed
  the denominator (`3.2 / 2.2`) because of carry.
- The tile tooltip lists `Force #id: X / Y MP` for the player's own Forces.
- A step's response updates the moved Force's remaining movement in place.

The end-of-turn check `CampaignRemainingMovementCheck` lists the player's Forces on the campaign map with
`CampaignMovementRemaining > 0`. It says a Force *has* movement, not that anywhere is open to it.

### Movement in context actions

Two context actions read campaign movement; their full rules are in
[force-context-action-toolbar.md](force-context-action-toolbar.md).

- **Split Force** needs a full turn: `CampaignMovementRemaining >= CampaignMovementPoints` (`HasFullMovement`).
  With carry this does not prove the Force has not moved — a Force can open above its allowance and, having
  moved, still sit at or above it. Tightening it needs the total granted this turn, which is not stored, so it is
  knowingly loose and pinned by a test.
- **Create Village** and **Establish Camp** need any movement at all (`> 0`), so a Force in debt is refused.

### Open questions

- How are roads, railroads and mag-tubes modelled, and do they halve the tile cost or bypass it?
- How do Land Forces cross water — fords, bridges, transport — and how do ships land on a coast?
- Should submarines be barred from shallow water, and should depth affect what can see them?
- Can Air Forces overfly mountains?
- How does orbital movement work, if not on the tile grid?

---

## Battle Movement

### The allowance — built

A template's battlefield allowance is **derived, never authored** (UD-52):

```
BattlefieldMovementPoints = MAX(0, Body.Power − SUM(Weight of fitted components))
```

Every piece of kit is paid for in tiles: a man carrying a knife crosses 6.5 tiles a turn, the same man with a
sword or spear 6, and in armour with a spear and shield 5.

Three things about the formula are easy to get wrong:

- **The body's own weight is excluded.** Power is what the body can move with *once it is already carrying
  itself*, so subtracting its weight again would charge for it twice. Only components fitted into the body's
  slots subtract. This is why `CompiledUnitTemplate.TotalWeight` (which *does* include the body) and the movement
  figure disagree — that is correct, not a bug.
- **It is floored at zero.** An over-loaded design stands still rather than acquiring negative movement.
- **Fractions are exact.** Everything is raw Scale 1,000, so 6.5 tiles is simply `6500`.

The **agreed calibration** — the numbers that make the design targets come out right:

| Item | Decimal | Raw |
|---|---|---|
| Body power (human) | 7.0 | 7,000 |
| Spear / sword | 1.0 | 1,000 |
| Knife | 0.5 | 500 |
| Armour | 0.5 | 500 |
| Shield | 0.5 | 500 |

Which yields: sword or spear alone **6.0** tiles, knife alone **6.5**, armour + spear + shield **5.0**. Power is
anchored at 7.0 because that makes the existing catalogue values (the human body's power, the primitive spear's
weight) correct as they stand. Only the spear exists as a component; the other weights are the calibration those
components should be authored with.

The arithmetic lives in **two places on purpose**:

- `BattleMovementCalculator` (`Features/UnitComponents/Services`) is authoritative. `CompileUnitTemplateHandler`
  calls it and writes the result into the snapshot, ignoring whatever sits on the body.
- `calculateBattleMovementPoints` (`features/unitdesign/model/battleMovement.ts`) mirrors it, because the designer
  shows the figure live as components are dragged in and out, before a save. `BattleMovementCalculatorTests.cs`
  and `battleMovement.test.ts` pin both to the same calibration cases; change one and you must change the other.

### Power has a second consumer

Battlefield movement is no longer the only thing that reads `Body.Power`. **Construction reads it too**, as how
hard a body can work:

```
BuildPointsPerTurn = Power × HealthyCount × MIN(1, CampaignMovementRemaining / CampaignMovementPoints)
```

`ConstructionRateCalculator` owns that, and it is what decides how long a settlement takes to go up — see
[settlements.md](../settlements.md#construction). Nothing else in the game reads Power: no cost and no combat
calculation touches it, though it is copied onto the compiled snapshot and surfaced in read and export
projections.

**So the 7.0 anchor now serves two calibrations at once.** Retuning it moves every battlefield allowance *and*
every build time, and the two are pinned by different fixtures — `BattleMovementCalculatorTests` /
`battleMovement.test.ts` for the tiles, `ConstructionRateCalculatorTests` and `StructureSeedDataTests` for the
build. Expect to re-anchor the `BuildPointsRequired` totals in `StructureSeedData` alongside any change to it.

Note which movement figure each uses: the battlefield allowance is derived *from* Power and weight, while the
construction rate reads Power and the **campaign** allowance. The two movement pools are separate (see
[Two pools](#two-pools)), so a change to one does not move the other.

`UnitComponentBody.BattlefieldMovementPoints` still exists as a column but is ignored; the Entity Setup Tool
shows the derived value read-only. Removing the column is tracked as **UD-53**.

### The pool — built

`BattleMovementRemaining` is granted in full when a Unit is fielded (see [Creation](#creation)). **Nothing spends
it and nothing replenishes it.** Battle rounds are not campaign rounds, and the battle turn pipeline's four marker
interfaces have no implementations and no ordering enum to hang a refresh on.

A battle equivalent of `CampaignMovementReplenishedOnRound` could not reuse a single `int`: battle round numbers
restart with each Battle, so it would need the Battle's identity alongside the number.

### Tile cost — built

Battle-map movement costs a **flat 1 MP per tile**. The end-of-turn check `BattleRemainingMovementCheck` lists a
player's Forces on a battlefield map with `BattleMovementRemaining >= ScaledAmount.Scale`. Because nothing spends
the pool, it currently reports every such Force as having movement for as long as the Battle lasts.

A half tile does not buy half a move: 6.5 spends as six tiles with 0.5 stranded. The fraction is precision in the
stat, and starts to matter only if fractional terrain costs arrive.

### Rounds

**Design.** A battle is divided into rounds. In a round, players alternate moving one Unit at a time, starting with
the defender. A Unit can be moved **once per round**, or can **pass** — and passing can increase its strength. When
the round ends, every Unit's movement points are restored. See
[battlefield-maps.md](../battlefield-maps.md#battle-flow) for round limits and objectives.

### Terrain

**Design.**

- Campaign-map movement bonuses and penalties also apply on the battle map.
- **Ascending** (land Units): +25% movement points for a moderate incline, +50% for a steep incline.
  **Descending** a steep incline: +25%.
- Harsh inclines may be impassable for some Units — sheer inclines are impossible for men, harder still for
  horses, and impossible for chariots.
- Forests and similar features impose movement penalties.
- Passive fortifications (palisades, walls, moats, tank traps, horse sticks) block or slow movement, and roads
  speed it up. See [battlefield-maps.md](../battlefield-maps.md#fortifications).

### Boundaries and the safe zone

**Design.** A battlefield map carries a two-tile boundary on each side, taken from the neighbouring campaign tiles
(see [battlefield-maps.md](../battlefield-maps.md#tile-boundaries-sourced-from-neighbouring-tiles)).

- Units placed within a boundary can attack into the central tile but may move only **within their boundary**.
- Defenders within the central tile **cannot move out** of it.
- The **assaulting** empire *can* move into the boundary area.
- The 2×2 corner voids cannot be entered.
- A Unit that reaches the **safe-zone line** on the side opposite its entry has safely crossed the campaign tile.
  Once safe it cannot attack or be attacked, unless it re-enters from that side.

### Fuel

**Design, not enforced.**

- A Unit on the battle map expends fuel whether or not (and however much) it moves.
- A Unit **out of fuel** moves only **30%** of its normal distance per round.
- A Unit that **enters a battle unsupplied** carries two battle rounds' worth of fuel, and cannot move for the rest
  of that battle.
- A Unit that **enters a battle supplied** keeps that supply until the end of that player's next campaign turn.

### What the client is told — built

`BattleMovementRemaining` travels as decimal MP on the same projections as the campaign figures, masked to `null`
for Forces the viewer does not own. See [What the client is told](#what-the-client-is-told--built).

### Open questions

- Can a Unit attack **and** move in the same round? See [open-questions.md](../open-questions.md#combat-domains-and-unit-capabilities).
- Do fractional terrain costs arrive on the battle map, and does a half tile then buy half a move?
