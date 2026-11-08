# Buildings

> **Nothing here is built.** There is no `Building` table, no catalogue, no construction process and no
> effect machinery. This document is the design as it currently stands: the parts that are settled, the
> parts that are a recommendation awaiting a decision, and the questions still open. Each section says
> which it is.
>
> The one piece of Buildings content that exists anywhere is the single Meeting Place row in the
> `## Buildings` section of [settlements.md](settlements.md#buildings), which this document expands on.

## What a Building is

A **Building** is constructed inside a Settlement and grants it capabilities — an output each turn, a
storage class it can keep, a unit design or component unlocked, a specialist employable, technology or
culture points.

**Building, structure and infrastructure are three different things, and the words are not
interchangeable.** A **structure** is on a campaign map tile: the object with artwork, a tile slot, a
sight range and build points, which is also what stands on a settlement's own tile to draw it.
**Infrastructure** is the structures built on the tiles surrounding a settlement, linked back to the
settlement that raised it. A building has no tile, no slot and no artwork on the map; it exists only in
relation to its settlement.

That distinction is load-bearing for the model: a Building is **not** a row in the `Structure`
catalogue and a building instance is **not** a `StructureInstance`. It borrows the construction
mechanic from structures (below) without borrowing the table.

## The catalogue

A Building comes from a global, authored catalogue, on the same terms as `Asset`, `Structure`,
`Technology` and `UnitComponent`: no `GameId`, no `PlayerId`, `Code` unique across the whole catalogue,
maintained through the Entity Setup Tool and the database seed.

| Field | Meaning |
| --- | --- |
| `Code` | Stable key, unique catalogue-wide. |
| `DisplayName` | What the player sees. |
| `Footprint` | How much of a settlement's Construction Capacity this building consumes. |
| `MinSettlementTier` | The lowest `SettlementTier` that may hold it. |
| `CoExistInSettlement` | How many may stand in one settlement. **Default 1**, `null` means limitless. |
| `CoExistInEmpire` | How many may stand across one empire. `null` means limitless, and is the default. |
| `BuildPointsRequired` | The construction work the building takes, on the same Scale-1,000 raw figure as `Structure.BuildPointsRequired`. |
| `MaxHitPoints` | The hit points a finished building has. |

A Building instance is a separate row per settlement, carrying its own construction progress and its
own hit points.

## Unlocking

**Settled.** A Building is locked by default and unlocked by technology discovery.

This costs almost nothing, because the technology catalogue is already generic about what it grants.
`TechnologyGrant` carries `(TargetType, TargetId, GrantEffect)` with `TargetType` a
`TechnologyGrantTargetType` and `GrantEffect` either `Unlock` or `Obsolete`. Buildings need:

- a `Building` member on `TechnologyGrantTargetType`, and
- a `PlayerUnlockedBuilding` table keyed `(PlayerId, BuildingId)` with an `ObsoleteOnRound`, matching
  `PlayerUnlockedAsset` and `PlayerUnlockedComponent`.

Because `TechnologyGrant.TargetId` carries no foreign key by design, nothing else changes. The
`Obsolete` verb comes along for free: a technology can retire a building kind, and what that means for
instances already standing is an open question below.

## Where a Building can go

### Minimum tier

**Settled.** `MinSettlementTier` is a **minimum**, not an exact match. A building authored at tier 3 can
be built at tiers 3, 4, 5 and 6; one authored at tier 1 can be built anywhere.

### Footprint and Construction Capacity

A Settlement has a **Construction Capacity** per tier. The summed `Footprint` of its buildings must not
exceed it, and a building is refused if constructing it would.

**Recommended placement.** `SettlementTierRules` already holds `_TERRITORY_RADIUS_BY_TIER`, a private
dictionary of tier → int, with a `<remarks>` explaining why the radius is a property of the tier rather
than of the Structure catalogue. Construction Capacity is the same shape of fact and belongs beside it
as `_CONSTRUCTION_CAPACITY_BY_TIER`. That puts the number in code rather than in the database, which is
consistent with the territory radius but is a decision worth taking deliberately.

### Co-existence limits

**Settled.** `CoExistInSettlement` defaults to 1; `CoExistInEmpire` has no default limit. `null` means
limitless in both cases.

Both are checked when construction is ordered. [manufacturing-chains.md](manufacturing-chains.md)
already anticipates a limit that a technology *raises* — `MAX_BLACKSMITH_PER_SETTLEMENT` goes from 1 to
5 once the Blacksmith's Guild is discovered, with a further rule requiring one Guild per ten Shops. The
catalogue column alone cannot express that, so a per-player override is a likely later addition rather
than a change to the column.

## Acquisition cost

A Building has zero or more asset costs, paid before construction begins.

The existing precedent is `TechnologyAcquisitionCost`: a child table keyed `(TechnologyId, AssetId)`
with a `Quantity`, a **real** foreign key to `Asset`, and **no rows meaning free**. A
`BuildingAcquisitionCost` follows it directly.

The cost is met from the asset bank of the settlement the building is going up in, draining that bank
first and taking the remainder across its network, levelled — the settlement-scoped expense rule in
[asset-banks.md](asset-banks.md). An asset whose `StorageScope` is not `SettlementBank` cannot be a
cost, because there is no balance to debit.

### Costs expressed as a group — **open**

A cost should be expressible as either a **specific asset** (Marble) or a **group** (stone). Which level
of the catalogue "group" means is not settled, and the candidates are not equivalent:

- `AssetCategory` — the mid-level grouping, whose examples in
  [assets-data-model.md](assets-data-model.md) are literally "Stone" and "Wood". The closest fit to the
  Marble/stone example.
- `AssetGroup` — a sub-grouping *within* a grouped category, e.g. "Logs" within "Wood". Narrower than
  the example needs.
- A **Material Class** as described in [production-and-assets.md](production-and-assets.md) —
  `CONSTRUCTION_STONE`, with a player-reorderable preference list deciding which concrete asset is
  spent. **No such entity exists**, and neither does the preference table.

The design doc's worked example is a building cost (`House: 10 CONSTRUCTION_STONE`), so Material Class
is what was originally intended. Deciding this means deciding whether Buildings brings the Material
Class and Material Preference model into existence or binds to a level of the hierarchy that is already
there.

## Construction

**Settled: build points, not hit points.** A building under construction accrues **build points**
towards `BuildPointsRequired`, reusing the structure mechanic rather than a second one:
`StructureInstance.BuildPointsProgress` is the pattern, stored as raw work on the Scale-1,000 figure so
an advance is a plain addition and completion is an exact comparison.

**What differs from structures is where the rate comes from.** A structure's `BuildPointsPerTurn` is
derived from the founding force — `Power × HealthyCount × MIN(1, movement remaining / movement points)`
— and **fixed once at founding**, because the settlers leave the map and there is nobody left to ask. A
building has no founding force, and its settlement is still there every turn.

So:

- The rate is driven by the settlement's `Population`. The existing calibration already anchors this:
  200 healthy humans on a full turn make 1,400 points, and the human body is Power 7.0, so
  `Population × 7` reproduces the structure rate exactly for a settlement the size of its founding
  party.
- The rate is **recomputed each turn**, not stored at founding. A settlement that has doubled in size
  should build faster, and a stored rate would keep it at its old speed.
- The calculation lives behind a seam from the outset, mirroring
  `Features/Structures/Services/ConstructionRateCalculator`, because further factors are intended later
  and the formula should have one home.

**Open:** whether a settlement can construct several buildings at once. A shared population-derived pool
makes concurrency fall out naturally — two builds each advance at half rate — and needs no second rule.
A queue would need one.

## Hit points

**Settled.** `MaxHitPoints` is a catalogue figure; an instance carries its current hit points and
**retains them**.

Hit points are a **separate track** from build points, not the same number:

- **While building,** hit points scale with construction progress, reaching `MaxHitPoints` at
  completion. This is what the player sees as a building "coming up".
- **Once complete,** hit points move independently — damage and repair — and construction progress is
  finished and no longer consulted.

Keeping them separate is what stops a damaged building from reading as a partly-constructed one, and
stops a repair from being indistinguishable from a build. Nothing in the codebase has hit points today
except unit bodies (`UnitComponentBody.Hitpoints`, `CompiledUnitTemplate.Hitpoints`), both raw
Scale-1,000.

**Open:** what damages a building, and what a building at zero hit points is. Neither siege nor capture
is built for structures either.

## Effects

A Building's effects are what it is *for*. The examples on the table sort into three data-shaped kinds
plus one subsystem.

### Per-turn conversion — inputs to outputs

**Recommended as one model, not two.** A building consumes zero or more inputs per turn and produces one
or more outputs. The two cases that look different are the same row shape:

| Building | Inputs | Outputs |
| --- | --- | --- |
| Meeting Place | none | 10 craft materials |
| Opera House | none | culture |
| Sawmill | wood logs | timber |

What makes this one kind rather than two is `Asset.StorageScope`, which already routes the destination
with no special-casing: craft materials and timber are `SettlementBank` and land in the settlement's
bank, while **Culture is a seeded Asset with `StorageScope = Player`** and is held once per player for
the whole game with no balance row at all. The building says what it produces; the catalogue says where
it goes.

[manufacturing-chains.md](manufacturing-chains.md) has already specified the full semantics of this,
for infrastructure: **all mandatory inputs** must be met, **at least one option from each option group**
for optional inputs, the highest available **Desirability Index** chosen within a group, and **pro-rata
output** where only part of an input is held. A sawmill is the degenerate case — one mandatory input,
one output — of a model that is already designed.

> **Nothing credits an asset bank balance yet.** [settlements.md](settlements.md) is explicit that banks
> exist, are created on founding and can be drawn down, but that nothing fills them. Buildings would be
> the first writer of a credit, which makes this effect kind the one that unblocks the most of the rest
> of the game.

### Capacity grant

A flat increase to what a settlement can hold — a storage hut adding +2,500 to its generic capacity.

The pieces are partly there: `StorageClass` is the thing a capacity cap binds against, every `Asset`
names exactly one, and RS-29 already limits what a settlement can hold. The "generic pool a class
overflows into" is referred to in [assets-data-model.md](assets-data-model.md) as the tier-granted
**storehouse**. A capacity grant is a small table of its own: a target (a storage class, or the generic
pool) and an amount.

### Modifier

**Recommended: authored in C#, not as catalogue data.** The technology catalogue has already made this
exact choice, and documents it: a technology that gives +10% forage writes a `PlayerSustenanceModifier`
row with `SourceType = Technology` and `SourceId` set to its own id, and *"which modifier a Technology
grants is authored in C#, not as catalogue data."* Buildings should mirror it rather than invent a
generic effect-target enum that will never cover the interesting cases.

Two pieces are needed:

- a `Building` member on `ModifierSourceType`, which today has `Technology`, `Specialist` and
  `Department` and no foreign key behind `SourceId`.
- the **nullable `SettlementId` scope** that `PlayerSustenanceModifier` already carries a TODO for:
  *"A Settlement's modifiers will apply to its territory, and wait on Settlements being modelled —
  TODO: add a nullable `SettlementId` scope then."* Settlements are modelled, and a building's modifier
  is settlement-scoped by nature, so Buildings is what makes that TODO come due.

Removing a building is then a delete by `(SourceType, SourceId)`, with no running total to unwind,
exactly as losing a technology is.

### Production lines — a subsystem, deferred

Some buildings let a settlement **create units**: a warriors hut, an airfield, a tank factory, a
shipyard, each accepting a different type of unit and each running in parallel with the others.

This is not an effect on a number. It is unit production, which does not exist in any form, and it
brings its own questions — what is buildable on a line, how long a unit takes, where it appears, what it
costs to keep. **Recommendation: Buildings carries this as a named seam and builds none of it.**

> **The word "queue" is already taken.** `TurnQueue`, `TurnQueueParticipant` and
> `PlayerTurn.TurnQueueId` own it, and a settlement-side `Queue` beside them will read as a turn concept
> every time. **`ProductionLine`** is the proposed name, and "in parallel" then falls out of lines being
> separate rows rather than needing a parallelism rule of its own.

## Specialist slots — deferred

A Building may have zero or more **SpecialistSlots**, each accepting a number and type of specialist
valid for that slot. [manufacturing-chains.md](manufacturing-chains.md) has worked examples: a farm with
5 slots taking `FARMER` or `GRAZIER` by crop type, a blacksmith with 2 taking any of five classes along
a progression.

**There is nothing to point at.** No `Specialist` entity, no table, nothing — only a reserved
`ModifierSourceType.Specialist` member with no foreign key behind it. Slots are carried in this design
as a named future seam; designing them now would drag the whole specialist subsystem into Buildings.

The acceptance-rule shape to copy when the time comes is `UnitComponentSlot` + `UnitComponentSlotRule`:
a slot with one or more typed rules (`RuleType`, `Operator`, `Value`) that a candidate must satisfy.

## Upgrade path

**Open, and barely discussed.** There should be a path from one building to a better one. The shape is
undecided, and the three candidates behave quite differently:

- A **supersession table**, as the unit designs use — `ComponentSupersession` and
  `TemplateSupersession` already model "this design replaces that one" and what happens to things built
  from the old one.
- A **self-referencing `UpgradesFromBuildingId`** on the catalogue row, making the path a chain in the
  catalogue itself.
- **Tiers of one building**, where the instance carries a level rather than the catalogue carrying
  several rows.

Whichever it is, it has to answer: does upgrading preserve the instance's hit points and specialists, or
replace the building; does the upgrade cost the difference or the full price; and does the footprint
change.

## Open questions

- **Which catalogue level a group cost names** — `AssetCategory`, `AssetGroup`, or a Material Class that
  does not exist yet. See above.
- **Whether the input/output chain model is shared with Infrastructure.**
  [manufacturing-chains.md](manufacturing-chains.md) attributes chains to infrastructure and gives the
  farm and the blacksmith as its examples, while a sawmill is a building. If a farm and a sawmill really
  do run the same pro-rata and option-group machinery, one shared set of chain tables is right and a
  building-specific set will be duplicated when infrastructure arrives.
- **Desirability Index versus Material Preference.** Two mechanisms are specified for one job — deciding
  which concrete asset to consume. [manufacturing-chains.md](manufacturing-chains.md) picks by an
  authored **Desirability Index**; [production-and-assets.md](production-and-assets.md) picks by the
  player's re-orderable **Material Preference** list. A sawmill needs exactly this decision ("which
  logs?"). Neither is implemented, so nothing is broken yet.
- **Whether a School is an output or a modifier.** Research points are player-scoped assets, so a school
  could be a per-turn conversion producing them — or a `PlayerResearchModifier` row, which is what the
  research pipeline already reads and which carries bucket semantics.
- **Upkeep.** [settlements.md](settlements.md) gives the Meeting Place an Upkeep column and
  [manufacturing-chains.md](manufacturing-chains.md) gives the farm an "assets per turn to keep
  operational". Upkeep is not modelled anywhere in the codebase, for anything, and what happens to a
  building whose upkeep goes unpaid is undecided.
- **Construction concurrency** — several builds at once off a shared pool, or a queue.
- **Capture, razing and tier growth.** What happens to a settlement's buildings when it changes hands,
  when it is razed, and when it grows a tier — including whether growth raising Construction Capacity is
  the only way a building over the old cap becomes legal.
- **A building over capacity after the fact.** Whether Construction Capacity can ever shrink, and what
  happens to the buildings standing in it if it does.
- **Footprint during construction.** Whether an unfinished building holds its footprint against the cap
  from the moment it is ordered — assumed yes, so that two orders cannot together exceed capacity.
- **Demolition.** Whether a player can tear a building down to reclaim its footprint, and whether
  anything is refunded.
- **What an `Obsolete` technology grant does to standing instances** of a building kind it retires.
- **What damages a building**, and what a building at zero hit points is.
