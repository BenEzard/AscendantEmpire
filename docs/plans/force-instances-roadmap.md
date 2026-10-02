# Force Instances — Roadmap for Tables and Endpoints

A fielded **Force** today is a thin thing. `units.UnitInstances` holds a template id, a tile, a headcount split
two ways and — since F-14 — a movement counter that is refilled when its owner's turn opens; `units.Commands`
exists but nothing can create one.
Everything else the design describes — officers, supply, veterancy, stances, splitting, a roster the player can
actually look at — is either doc-only or a `TODO` in a pipeline stub.

This document plans the route from there to a Force that feels present **on the map** and **in the player's
hands**. It is a plan for *tables and endpoints*, sequenced by dependency, not a design document: where the
design has already ruled, this cites it; where it has not, this raises the ruling as a blocker rather than
inventing one.

Companion to:

* [military-organisation.md](../design/units/military-organisation.md) — the model this builds toward.
* [military-organisation-implementation-notes.md](../design/units/military-organisation-implementation-notes.md)
  — what has actually been built, and the debts carried.
* [unit-command-terminology.md](../work-in-progress/unit-command-terminology.md) — the vocabulary and the
  Unit/Command/Force rulings all of this obeys.
* [unit-mechanics.md](../design/units/unit-mechanics.md), [combat.md](../design/units/combat.md),
  [unit-support-and-supply.md](../design/units/unit-support-and-supply.md) — the rules the later phases serve.

---

## 1. Where things stand

### Tables that exist

| Table | Holds | Notably missing |
|---|---|---|
| `units.UnitInstances` | game, player, tile, template, `InitialStrength`, `HealthyCount`, `InjuredCount`, `CampaignMovementRemaining`, `BattleMovementRemaining`, `Name`, `CommandId` | authorised strength, veterancy, stance, facing, supply, concurrency token, creation turn |
| `units.Commands` | game, owner, tile, `Name` | a commander, membership rules, anything that creates one |
| `units.ForceOrders` | the order, its target (`UnitInstanceId` XOR `CommandId`), progress, debug fields | nothing reads or cancels them over HTTP |
| `units.ForceOrderMovementSteps` | the planned path | — |
| `units.ForceOrderParticipants` | `UnitInstanceId` + `Primary`/`Secondary`/`Result` role | a `CommandId` arm — the same nullable pair `ForceOrder` already got |
| `military.PlayerOfficerRank` | per-player, per-domain rank ladder: title, `MaxSoldiers`, `MaxSpecialists` | the promotion-cap divisor and perk bonus |
| `military.PlayerOrganisationStructure(+Tier)` | per-player commissioned hierarchies | nothing runtime references them |

### Endpoints that exist

| Route | Does |
|---|---|
| `POST /units/temporary-dev-only/spawn` | spawns a Unit; explicitly a dev affordance |
| `GET /units/display` | display info for the player's Units |
| `POST /force-orders/move` | multi-turn pathfound move order |
| `POST /force-orders/step` | one immediate step onto an adjacent tile |
| `GET /force-orders/actions` | the context-action toolbar's availability list |
| `GET\|PUT /military/officer-ranks` | rank ladder configuration |
| `GET /military/organisation-tags`, `GET\|PUT /military/organisation-structures/{tagId}` | hierarchy configuration |

`GetForceOrdersHandler` exists with **no controller action** — the read side of orders is written but unreachable.

### The shape of the gap

Three axes, and the phases below are ordered so each one leaves the game playable:

1. **Across turns** — state that persists and advances. Movement replenishment is **done** (`TM-3`, delivered as
   `F-14`); supply is still a `TODO` in `CheckUnitSupply` and healing a `TODO` in `HealTroops`. This was the
   cheapest gap and the most visible: every Force in the selection toolbar used to render greyed out.
2. **On the map** — what a Force *is* to the tile it stands on. Commands cannot form, nothing splits or merges,
   there is no stance, no facing, no entrenchment, no supply marker.
3. **For the player** — the management layer. No officer exists as an entity, so a Command has no commander and
   none of its rank-derived limits are enforceable. There is no roster, no upkeep view, no history.

---

## 2. Sequencing at a glance

| Phase | Theme | New tables | Depends on | Component |
|---|---|---|---|---|
| 1 | The turn cycle made honest | none (columns only) | — | `TM` |
| 2 | Command lifecycle | none (columns only) | 1 | `TM` |
| 3 | Split and merge | none (columns only) | 2 | `TM` |
| 4 | Officers | `military.Officers` | 2 | `TM` |
| 5 | Runtime formations | `units.UnitFormations` | 4 | `TM` |
| 6 | Veterancy and experience | none (columns only) | 5 | `COMM` |
| 7 | Supply and support | `units.UnitSupplyStatus` | 1 | `TM` |
| 8 | Stance, facing, entrenchment | none (columns only) | 1 | `TM` |
| 9 | The player's military screen | none | 4, 7 | `TM` |
| 10 | End of life and the event log | `units.ForceEvents` | 3 | `TM` |

Only three new tables in the whole plan. That is deliberate: most of what a Force is missing is **state on the
Force**, and a column on `units.UnitInstances` is cheaper to read on the hottest path in the game (the map tile
projection) than a join. New tables appear only where the cardinality genuinely is one-to-many (formations,
events) or where the row is a person in its own right (officers).

---

## Phase 1 — The turn cycle made honest

**Purpose.** Nothing here is new design. It is the set of things the code already claims to do and does not,
and until they are true the later phases cannot be judged by looking at the screen.

### Column changes

`units.UnitInstances`:

| Column | Type | Why |
|---|---|---|
| `AuthorisedStrength` | `int` | The design separates **Authorised** (what this Unit is established at), **Initial** (the ceiling it can ever be reinforced back to) and **Current** (`Healthy + Injured`). Only the last two exist. Reinforcement in a later phase has nothing to aim at without it. |
| ~~`MovementStrengthCarried`~~ | — | **Not added (F-14).** The carry needs no column: what a Force is still holding when its turn opens *is* the carry, so nothing has to be banked at end of turn and nothing can drift out of step with the movement it describes. |
| `BattleMovementRemaining` | `long` | **Added (F-14).** Battle movement as its own pool, so a Force drawn into a Battle keeps the campaign movement it will need once the Battle releases it. Seeded at spawn from `CompiledUnitTemplate.BattlefieldMovementPoints`; nothing replenishes it yet. |
| `CampaignMovementReplenishedOnRound` | `int` | **Added (F-14).** The idempotency stamp for start-of-turn replenishment — see the turn pipeline section below for why it is load-bearing. |
| `CreatedTurnNumber` | `int` | Every order row carries one; the Unit itself does not. Needed by the roster and the event log. |
| `HomeSettlementId` | `int?` | Where the Unit was raised. Drives reinforcement (a bloodied Unit returns home to be topped back up toward its Initial Strength), upkeep attribution, and what happens to a Unit whose home is captured. **Nullable, and not as an edge case**: `GameSetupService` spawns the starter settler and scout at a start position *before* any settlement exists, so the game's first Forces have no home by construction. |
| `RowVersion` | `rowversion` | Two clients acting on one Force is the ordinary case in a parallel-turn game. `Battle` already sets the precedent. |

`units.Commands`: `CreatedTurnNumber`, `RowVersion` — same reasoning.

**`HomeSettlementId` has no settlement to point at yet.** There is no `Settlement` entity; the closest thing is
`StructureInstance`, and a settlement is presumably one of those. Either the column FKs to `StructureInstance`
and is narrowed later, or it waits for a settlement concept — a decision worth taking deliberately rather than
by accident, because it is a foreign key and retargeting one is a migration with data in it.

### Turn pipeline

**Delivered as F-14.** The rules as they now stand are in
[unit-movement.md](../design/units/unit-movement.md#campaign-movement). A new `IStartCampaignTurnProcess`, ordered **last**:

```
AlertSentries           = 10_000
CheckUnitSupply         = 20_000
ReplenishMovementPoints = 30_000
```

This document originally specified `ReplenishMovementPoints = 5000`, ahead of everything. That was **wrong**:
a unit whose body requires fuel it does not have gets no campaign movement at all, and that verdict has to
exist before the allowance is handed out rather than being unpicked afterwards. `CheckUnitSupply` is still a
stub, so nothing is read yet, but the ordering is in place and pinned by a test.

It sets `CampaignMovementRemaining = CampaignMovementPoints + carry` from the compiled template, where the
carry is whatever the Force was still holding — capped at one full point, zero if it did not move at all, and
uncapped when negative so debt is paid back rather than forgiven. There is **no carry column**: the leftover
*is* the carry. The arithmetic lives in `MovementReplenishment`, a pure static pinned without a database.

Idempotency comes from `UnitInstances.CampaignMovementReplenishedOnRound`. It is load-bearing, not defensive:
a second run in one Round would see a leftover at or above the allowance, bank nothing, and silently destroy
the carry the first run just granted.

This was **TM-3**, and it unblocks the split action, the context toolbar's enabled states and the selection
toolbar's ordering all at once.

`HealTroops` (end of round) stays a stub until Phase 7 gives it medical supply to read.

### Endpoints

| Route | Purpose |
|---|---|
| `PATCH /units/{unitInstanceId}/name` | `UnitInstance.Name` is documented as the player's own name and is null everywhere because nothing writes it. The selection toolbar already prefers it where present. |
| `GET /force-orders` | Expose `GetForceOrdersHandler`. Filters: `unitInstanceId`, `commandId`, `status`. Returns a DTO, not the entity — the handler currently returns `ForceOrder` itself, which would put EF navigations on the wire. |
| `POST /force-orders/{forceOrderId}/cancel` | A standing order the player cannot revoke is a trap. Writes `CancellationReason`, which the entity already carries. |

### Rulings needed

* Does `AuthorisedStrength` come from the organisation structure's tier size at spawn, or is it chosen by the
  player within bounds? The design implies chosen-within-bounds; spawn today takes a raw size.

---

## Phase 2 — Command lifecycle

**Purpose.** `units.Commands` was landed as schema only, deliberately: nothing forms one, so the map, vision and
toolbar projections were never given Command arms because those arms would have been unreachable code. This
phase makes a Command real and lights up those arms in the same pass.

### Column changes

`units.Commands`:

| Column | Type | Why |
|---|---|---|
| `CommanderOfficerId` | `int?` | Added **empty** here and populated in Phase 4. Nullable because a Command may operate with a vacant top slot — the design explicitly allows assembling a higher formation before a qualifying commander exists. |
| `HomeSettlementId` | `int?` | Only if a Command *has* a home — see the ruling below. |

A Command does not obviously have a home settlement the way a Unit does. A Unit is **raised** somewhere; a
Command is **formed in the field** out of Units that may have been raised in four different places. Three
readings, and they want choosing between rather than defaulting: the Command has no home at all (the concept is
Unit-only), it inherits the home of the Unit that formed it, or it is a genuine player choice — a headquarters
the Command is attributed to for upkeep and reinforcement. The third is the only one that makes the column mean
something on a Command rather than merely exist on it.

No movement columns on `Commands`. A Command moves at the speed of its **slowest** Unit, so the value
is `MIN(members)`, derived on read. A stored column would be a second source of truth that split, casualties and
partial movement all have to keep true by hand — the same argument the terminology doc used to put the order on
the Command rather than fanning it out to members.

### Endpoints

| Route | Purpose |
|---|---|
| `POST /commands` | Form a Command from `unitInstanceIds[]` and an optional name. Validates: same tile, same player, same game, none already in a Command. |
| `GET /commands/{commandId}` | Read one, with its members. |
| `POST /commands/{commandId}/units` | Join one or more Units standing on the same tile. |
| `DELETE /commands/{commandId}/units/{unitInstanceId}` | Detach one Unit; it becomes a lone Force on the tile. |
| `DELETE /commands/{commandId}` | Disband — every member becomes a lone Force. The row is deleted, not soft-deleted, until Phase 10 gives it a history worth preserving. |

### Projection work in the same phase

* `MapObserver` / `DTForceStub` / `ObservedForce` gain their `RecordType.Command` arm and stop assuming Unit.
* `ForceActionContextBuilder` builds a `ForceActionForce` for a Command (strength summed, movement minimised).
* Vision: Units in a Command **share detection and sight**. Today every observer is independent; a Command
  becomes one observer whose sight is the best of its members'.
* `ForceOrderParticipant` gains the `CommandId` nullable pair deferred from TM-6 stage 6 — see Phase 3.

### Rulings needed

* **May an unled Unit join a Command?** `military-organisation.md` lists "unled units cannot join a Command" as
  an uncommitted candidate lever against the armed-settler hole. The form/join endpoints cannot be written
  without an answer, and it is one boolean either way.
* **Is a Command of one allowed?** The design says yes, and says it is rarely worth the officer. Confirm the
  endpoint does not reject it.
* **What bounds a Command with no commander?** Max Units, max total strength and max specialists all derive from
  the commander's rank. Until Phase 4 there is no commander — so either the limits go unenforced (simplest, and
  honest about the state of the model) or an interim cap is invented. Recommend unenforced, documented on the
  handler, and enforced in Phase 4 in one place.

---

## Phase 3 — Split and merge

**Purpose.** `SplitForceStrategy` decides *whether* a Force can split. Nothing performs the split. The design
gives two distinct mechanics, and they resolve to one player-facing action.

### Column changes

`units.ForceOrderParticipants`:

| Column | Type | Why |
|---|---|---|
| `UnitInstanceId` | `int?` (was `int`) | Becomes nullable, paired with `CommandId` under a `CK_ForceOrderParticipants_UnitOrCommandXor` check constraint. Same shape as `ForceOrders`, same reasoning: a participant is a **Force**, and combining or splitting Commands is exactly the case that needs it. |
| `CommandId` | `int?` | |

Converting an existing non-null column to a nullable pair with a XOR constraint is safe here for the same reason
it was safe on `ForceOrders`: every existing row has a Unit and no Command, so the constraint holds the instant
it is added.

### Endpoints

| Route | Purpose |
|---|---|
| `POST /force-orders/split` | Body branches on leadership model. **Unled**: a list of sizes, each a multiple of the tag's `UnledBaseSize` except one remainder, every group ≥ one quantum. **Commissioned**: a tier index and a count of sub-formations to detach. |
| `POST /force-orders/combine` | Merge Forces on one tile. **Unled**: same tag only, result ≤ the tag's `UnledMaxSize`. **Commissioned**: governed by the reorganisation rules, and blocked until Phase 5 gives it formations to reorganise. |

Both cost the whole turn and both resolve **immediately** rather than becoming standing orders — matching the
`ForceActionType` doc's distinction, and matching how `SplitForceStrategy` already gates on `HasFullMovement`.
A `ForceOrder` row is still written, with `Result` participants naming the Forces created, because that row is
what the event log in Phase 10 reads.

### Rulings needed

* **What does a split Force inherit?** Veterancy and experience (Phase 6) divide how — copied to both halves, or
  split proportionally? Copying is the generous, exploitable option (split-and-merge to launder XP).
* **Initial Unit Strength on split.** Commissioned Units are capped by the strength they were created at. When
  one splits, does each half carry a proportional share of that ceiling? Unled Units are explicitly exempt.

---

## Phase 4 — Officers

**Purpose.** The largest single hole. `PlayerOfficerRank` is *configuration*, not people; there is no `Officer`
entity, which is precisely why `Command` carries no commander and none of its limits are enforced anywhere.

### New table — `military.Officers`

| Column | Type | Notes |
|---|---|---|
| `Id` | `int` | |
| `GameId` | `int` | |
| `PlayerId` | `int` | Matches the `PlayerId`-only keying of the other `military` tables; game-scoped queries join through `Player`. |
| `Name` | `nvarchar(100)?` | Player-renamable, like rank titles. |
| `MovementDomain` | `DomainType` | **Permanent, set at creation.** The roster partitions by it, and an officer may only lead a formation of the matching domain. |
| `RankLevel` | `int` | 1–6, positionally matching `PlayerOfficerRank.Level`. |
| `CommandExperience` | `int` | Accrues only under the manning gate — see Phase 5. |
| `BankedExperience` | `int` | The stall-at-cap rule: an experience-ready officer at a full rank banks and auto-promotes when a slot frees. |
| `CommandId` | `int?` | The post held, if a Command. |
| `UnitFormationId` | `int?` | The post held, if a formation inside a Unit (Phase 5). |
| `CreatedTurnNumber`, `KilledTurnNumber` | `int`, `int?` | |
| `RowVersion` | `rowversion` | |

**The post lives on the officer, not in a join table**, because an officer holds at most one post at a time — a
join table would model a many-to-many the design forbids, and would then need its own uniqueness constraint to
say so. The two nullable columns take a check constraint allowing at most one non-null, and *both* null (an
unassigned officer sitting in the roster) — the one place in this plan where the pair is not a strict XOR.

### Column changes

`military.PlayerOfficerRank`: `PromotionDivisor int?`, `PromotionPerkBonus int` — the two terms of
`maxOfficers(rank) = floor(totalSoldiers / divisor[rank]) + perkBonus[rank]`. Rank 1 takes a null divisor
meaning uncapped, mirroring the existing "null `MaxSoldiers` means no ceiling" convention on the same table.

### Endpoints

| Route | Purpose |
|---|---|
| `GET /military/officers` | The roster. Filters: domain, rank, assigned/unassigned. |
| `PATCH /military/officers/{officerId}` | Rename. |
| `POST /military/officers/{officerId}/assign` | Assign to a Command or a formation. Validates domain match, rank match and vacancy. |
| `POST /military/officers/{officerId}/unassign` | Return to the roster. |
| `GET /military/officer-capacity` | Per rank: held, cap, and how many are stalled on banked experience. The screen that makes the promotion ceiling legible instead of mysterious. |
| `POST /military/academy/convert` | Send a Unit or a formation "to the academy": consume it, emit officers at rolled ranks weighted by its Combat Experience. Blocked until Phase 6 supplies that number. |

### Rulings needed — one of these gates the whole table

* **Are free tier-1 officers rows?** A maxed 5-tier Infantry Unit has 265 officers, **200 of them free at tier
  1**. Materialising those is 200 rows per division that never do anything except exist, and the promotion
  ceiling explicitly does not count them. Recommend **only earned officers (Rank ≥ 2) get rows**; the tier-1
  officer is implied by the formation and minted arithmetically. That keeps the roster the *scarce* list the
  design says it is, and the ascension rule ("mints a fresh free Rank-1 officer, and that man leaves the soldier
  count") becomes a decrement rather than an insert. Decide before the table is created: reversing it later
  rewrites the ascension logic.
* **Do unled soldiers count toward `totalSoldiers` in the cap formula?** Flagged in the design's Future review as
  an open exploit shape. The formula cannot be written without it.

---

## Phase 5 — Runtime formations

**Purpose.** A commissioned Unit's internal shape is currently *implied* by the template's organisation tag and
the player's `PlayerOrganisationStructure`. Nothing runtime references those tiers, which is why a commissioned
split has no legal split points to detach along and why command experience has no manning gate to test.

### New table — `units.UnitFormations`

| Column | Type | Notes |
|---|---|---|
| `Id` | `int` | |
| `UnitInstanceId` | `int` | |
| `TierIndex` | `int` | 1–5, matching `PlayerOrganisationStructureTier.TierIndex` and therefore the officer rank. |
| `ParentUnitFormationId` | `int?` | Null at the top tier. Self-referencing hierarchy. |
| `HealthyCount`, `InjuredCount` | `int` | Casualties land on the formation; the Unit's counts become the roll-up. |
| `AuthorisedStrength` | `int` | Snapshotted from the structure's tier at creation, so editing the structure later does not silently resize fielded Units. |

**Do not materialise tier 1.** The same 200-rows-per-division argument as the officer roster. Model tiers **2 and
above** as rows, and hold the bottom tier as a count plus a headcount on its tier-2 parent. The design's manning
gate ("the appropriate number of sibling formations present, each at ≥ 30% of authorised size") is answerable
from a count and a headcount without a row per company.

Unled Units get **no rows at all** — they have no tiers, exactly as they have no per-player structure.

### Endpoints

| Route | Purpose |
|---|---|
| `GET /units/{unitInstanceId}/formations` | The Unit's internal tree, for the Unit detail panel and as the picker the commissioned split reads. |

Phase 3's commissioned split branch becomes implementable here; so does the Phase 6 command-experience gate.

### Rulings needed

* Are casualties applied to the Unit and distributed down, or to formations and rolled up? Rolling up is correct
  if formations are to persist through casualties, merge and disband as the design says — but it makes every
  combat write touch the tree.

---

## Phase 6 — Veterancy and experience

**Purpose.** `combat.md` builds the attack value on a veterancy multiplier. **No veterancy exists anywhere in
the codebase.** The academy conversion in Phase 4 also has nothing to read.

### Column changes

`units.UnitInstances`:

| Column | Type | Notes |
|---|---|---|
| `VeterancyLevel` | `byte` | Untrained → Legendary. Its `ExperienceMultiplier` is code-defined, following the `MilitaryOrganisationDefaults` precedent rather than a seeded reference table — six fixed rows no player edits are not data. |
| `VeterancyExperiencePoints` | `int` | Progress toward the next level. |
| `CombatExperiencePoints` | `int` | The separate currency the academy converts. |

`military.Officers.CommandExperience` (Phase 4) accrues here, gated on Phase 5's manning test.

### Endpoints

Reads only — the values ride on the existing Unit display and detail projections. The one write is Phase 4's
`POST /military/academy/convert`.

### Rulings needed

* **Do unled Units earn Combat Experience?** The design says Command Experience is settled (they earn none) and
  leaves Combat Experience explicitly undecided, because the academy then turns a veteran scout corps into an
  indirect officer farm.
* Does the veterancy multiplier also scale the *rate* of experience gain, or only combat output?

---

## Phase 7 — Supply and support

**Purpose.** `CheckUnitSupply` is a one-line `TODO` in the start-of-turn pipeline, and the design has four
support types with concrete penalties (out of fuel: no campaign movement, 30% on the battle map; out of ammo:
30% attack and defence; out of food: 34% wounded per turn). None of it can run without state.

### New table — `units.UnitSupplyStatus`

One row per Unit, 1:1 on `UnitInstanceId` as the primary key — **not** a row per support type. The four types
are fixed and every read wants all four (the map marker, the combat calculation, the roster), so four columns
beat four rows on the hottest path.

| Column | Type | Notes |
|---|---|---|
| `UnitInstanceId` | `int` | PK and FK. |
| `FuelSupplied`, `AmmunitionSupplied`, `FoodSupplied`, `MedicalSupplied` | `bit` | Recomputed each start of turn. |
| `TurnsWithoutFood` | `int` | The food penalty compounds; the others do not. |
| `LastEvaluatedTurnNumber` | `int` | So a stale row reads as stale rather than as supplied. |

### Column changes — `unitdesigns.CompiledUnitTemplate`

Supply cannot be reckoned without the template saying what the Unit *needs*, and the snapshot carries none of it
today: `FuelAssetId int?`, `AmmunitionAssetId int?`, `FoodAssetId int?`, `SupplyRadius int`,
`LandCarryingCapacity int`, `AirCarryingCapacity int`. That last pair also closes the design's open TODO about
Land and Sea Units crossing Water. This is compile-time work in the Unit Designer, so it lands under the Unit
Design component (`UD`), not `TM`.

**Supply chains are computed, not stored.** An intact chain is a contiguous run of `SUPPLY`-carrying Units whose
radii touch, back to friendly territory — a graph walk over data already in memory during the turn pipeline.
Persist it only if the map overlay proves too slow to recompute on read, and then as a per-turn cache keyed
`(GameId, PlayerId, TurnNumber)` that is truncated rather than updated.

### Endpoints

| Route | Purpose |
|---|---|
| `GET /military/supply` | Player-wide: which Forces are unsupplied, in what, and for how long. The "why is my army dying" screen. |

The per-tile supply marker rides on the existing map projection as a flag, not as its own call.

### Rulings needed

* Which Unit in a Command misses out when supply is short? The design flags this as needing "an intelligent,
  simple way" and suggests letting the player choose. A stored per-Unit supply priority would be a further
  column; a deterministic rule (weakest first, or largest first) needs none.

---

## Phase 8 — Stance, facing and entrenchment

**Purpose.** `ForceOrderType` already names `Fortify`, `Ambush` and `Sentry`, and `unit-mechanics.md` makes
facing a real defensive modifier. All three are today an order with no resulting *state* on the Force.

### Column changes — `units.UnitInstances`

| Column | Type | Notes |
|---|---|---|
| `Stance` | `int` | Normal / Fortified / Ambushing / Sentry. Distinct from the order that established it: the order completes, the stance persists. `DefenderProfile.IsAmbushing` in `combat.md` reads this. |
| `Facing` | `int?` | One of the eight directions, null where the domain has no facing. Changing it costs no movement points but must happen before end of round. |
| `EntrenchmentProgress` | `int` | Fortifying is multi-turn; the `Fortifying` pipeline process has work to write somewhere. |

### Endpoints

| Route | Purpose |
|---|---|
| `POST /force-orders/stance` | Set stance for a Force. Free where the design says free, turn-costing where it does not. |
| `POST /units/facing` | Free, and takes a **list** of Unit ids: `unit-mechanics.md` requires the UI to re-face a whole selection as a group, so a per-id route would be the wrong shape from the start. |

---

## Phase 9 — The player's military screen

**Purpose.** Everything above produces state the player currently has no way to see except by clicking tiles.

### Endpoints

| Route | Purpose |
|---|---|
| `GET /military/forces` | The roster: every Unit and Command, paged, with template, strength, tile, stance, supply, veterancy and current order. Filterable by domain, template, supply state and "has orders". Each row carries its tile so the client can jump the map to it. |
| `GET /military/summary` | Totals: Forces by domain, total soldiers, officers held vs capped per rank, Forces unsupplied, Forces idle. Feeds the Military Advisor landing page. |
| `GET /military/upkeep` | What the standing army costs per turn, by asset. |

No new tables. It does need covering indexes — `units.UnitInstances (GameId, PlayerId)` and the equivalent on
`units.Commands` — since every query here is player-scoped and the existing indexes are order-shaped.

---

## Phase 10 — End of life and the event log

**Purpose.** Units are created and never destroyed. Nothing disbands, nothing settles, and when something does
happen to a Force between turns the player has no way to find out what.

### Column changes

`units.UnitInstances`: `DisbandedTurnNumber int?`, with a filtered index for `WHERE DisbandedTurnNumber IS NULL`.
**Soft, not hard, deletion** — `ForceOrders`, `ForceOrderParticipants` and eventually battle history all
reference the row, and the terminology work already demonstrated how expensive it is to discover after the fact
that a `DropTable` was the wrong call. Every live-Force query filters on the index.

### New table — `units.ForceEvents`

| Column | Type | Notes |
|---|---|---|
| `Id`, `GameId`, `PlayerId`, `TurnNumber` | | |
| `EventType` | `int` | Spawned, split, merged, joined a Command, disbanded, starved, ambushed, destroyed, officer ascended. |
| `UnitInstanceId`, `CommandId` | `int?` | The nullable Force pair again — but here **both may be null** (a purely officer-level event), so no XOR. |
| `CampaignMapTileId` | `int?` | Where. |
| `Detail` | `nvarchar(max)?` | JSON payload, deliberately unstructured — this is a narrative feed, not a queryable fact table. |

### Endpoints

| Route | Purpose |
|---|---|
| `POST /units/{unitInstanceId}/disband` | Return the soldiers, or lose them, per the ruling below. |
| `POST /force-orders/settle` | The settler payoff: consume an unled Force carrying the settler capability and found a settlement sized by its strength. The single most-requested thing the current build cannot do. **Its toolbar affordance already exists** — UD-56 landed Create Village and Establish Camp as context actions with their full rule sets, gated on the `found-settlement` and `found-camp` capability tags; both currently raise a "not built yet" toast because there is nothing for them to create. This endpoint (or two of them, one per tier) is what they are waiting for. |
| `GET /military/events` | The between-turns feed, filterable by turn and by Force. |

### Rulings needed

* Does disbanding return soldiers to a population pool, or simply delete them? Nothing in the design says, and
  the answer decides whether disband is a strategic tool or a last resort.

---

## Cross-cutting concerns

**Concurrency.** Every phase that adds a write path adds one to a Force two clients can touch at once. The
existing military save handlers are keyed upserts with **no row version** and are explicitly flagged as
last-writer-wins. Phase 1 adds `RowVersion` to the two Force tables so later phases inherit it rather than
retrofit it; `Battle.RowVersion` is the precedent to copy.

**The `RecordType` switch.** Every "acts on either a Unit or a Command" path added from Phase 2 on should
`switch` on `RecordType` with **no `default` arm**, so a future enum member lights up the incomplete ones. Note
that warning currently goes nowhere — no analyzers, `TreatWarningsAsErrors` off across all six csprojs
(`AE-S15-002`) — so it is a convention held by review, not by the build.

**Compiled snapshot versioning.** A fielded Unit references its template's compiled snapshot, which is
**overwritten on recompile**, so fielded Units silently track template edits. That is already wrong; every phase
that reads more from the snapshot (Phase 6 reads combat values, Phase 7 reads supply requirements) makes it
wronger. It blocks nothing here, but Phase 7 is the natural moment to raise it as its own ticket.

**Carried debts that limit what these phases can show.** From the implementation notes: movement modifiers are
lost for template-built Units (`ModifiersJson` has no compiled-template equivalent), so a Force on the map moves
approximately rather than correctly. Sight is no longer among these — `UD-55` gave components a real `Sight`
value and compiles it into `CompiledUnitTemplate.TotalSight`, which the Unit read paths divide by 1,000 for the
map reveal radius.

**Fixed point.** Every new movement, strength or combat value follows the Scale 1,000 convention already used by
`CampaignMovementRemaining` and `CampaignMovementPoints`, so the two compare without conversion at the call site.

**Migrations.** Scaffold with `--project AscendantEmpire.Application --startup-project AscendantEmpire.Api`,
never `--no-build`, never `database update`. Generate the SQL for any migration writing raw T-SQL. Phase 3's
nullable-pair conversion and Phase 10's filtered index are the two most worth reviewing by hand.

**Column renames deserve the same scrutiny.** EF has no rename detection for properties in the general case, and a
scaffolded `DropColumn`/`AddColumn` pair silently destroys the data in the column. F-14 renamed
`MovementPointsRemaining` and got a clean `sp_rename`, but that was checked by reading the generated SQL before
committing, not assumed — as `TM-6_RenameTroopInstanceToUnitInstance` had to be corrected by hand after EF
scaffolded a destructive `DropTable`/`CreateTable`.

**Tests.** Follow the tiering in [testing.md](../testing.md). Note that Phase 3's XOR constraint and Phase 10's
filtered index are **invisible to the in-memory provider**, which ignores constraints entirely — those need tier
3, exactly as `CommandForceOrderTargetTests` did for the `ForceOrders` XOR.

---

## Open decisions, collected

These gate the phases they sit in. None can be resolved by reading the code.

| # | Question | Gates |
|---|---|---|
| 0 | Does `HomeSettlementId` FK to `StructureInstance`, or wait for a `Settlement` entity — and does a Command have one at all? | 1, 2 |
| 1 | May an unled Unit join a Command? | 2 |
| 2 | What bounds a Command with no commander — nothing, or an interim cap? | 2 |
| 3 | How do veterancy and Initial Unit Strength divide across a split? | 3 |
| 4 | Are free tier-1 officers rows in `military.Officers`, or implied? | 4 |
| 5 | Do unled soldiers count toward `totalSoldiers` in the promotion ceiling? | 4 |
| 6 | Are casualties applied top-down to the Unit, or bottom-up to formations? | 5 |
| 7 | Do unled Units earn Combat Experience? | 6 |
| 8 | Which Unit in a Command misses out when supply is short? | 7 |
| 9 | Does disbanding return soldiers to a population pool? | 10 |

Questions 4 and 5 are the two worth answering first: both change the shape of a table rather than the behaviour
of a handler, and both are expensive to reverse.

---

## Deliberately out of scope

* **Battle-map behaviour.** `BattleMapTileId` exists on both Force tables, and everything above is campaign-map.
  Battle-map Forces are their own plan.
* **Combat resolution.** Phase 6 supplies veterancy because `combat.md` needs it; actually resolving an attack
  is `COMM` work, and is sketched rather than settled in the design.
* **Specialists as entities.** They attach to Forces *and* settlements, progress along class paths, and can be
  assassinated — a feature in their own right, not a Force feature. Phase 4 leaves `MaxSpecialists` unenforced
  and says so.
* **AI order-giving.** Nothing here assumes a human on the other end, but nothing here serves an AI either.
* **Retiring `UnitDesign`.** Still referenced by `PlayerUnitDesignState` and `CompiledDesignStats`; a separate
  cleanup, unaffected by any of this.
