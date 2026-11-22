# Technology Sustenance Modifier Grants — Design

**Status:** agreed, not yet planned. The implementation plan is a separate document.

**Planner tasks:** `T-10` the catalogue table and its authoring path · `T-11` the discovery reconciler and its call sites · `T-12` the Arid grant, the end-to-end test and the document corrections. Each ends in its own commit, prefixed with its own code.

## Intent

Discovering a Technology should raise the discovering Player's sustenance yields. The worked example, and the only grant this work authors, is **Biome Skill - Arid** (`BiomeSkillArid`) granting **+20% Hunt and +20% Forage on Arid tiles**.

`PlayerSustenanceModifier` already exists and `IPlayerSustenanceValueReader` already applies it to a biome's base. What is missing is everything upstream of that table: nothing records which Technology grants which modifier, and nothing writes a row when a Technology is discovered. [ApplyResearch.cs](../../../AscendantEmpire.Application/Features/Turns/Services/TurnPipeline/Campaign/EndTurn/ApplyResearch.cs) carries the TODO. This work builds that path for sustenance modifiers, and builds the discovery-time seam in a shape the other effect kinds can join.

### Why now

Technology is the first modelled `ModifierSourceType`, so it is the only source that can write a modifier at all; Specialists and Departments are not modelled. Until something grants a modifier, every Player sees identical yields on identical tiles, and the whole per-Player half of the yield formula is untested in play.

## Goals

- Discovering `BiomeSkillArid` gives that Player two `PlayerSustenanceModifier` rows: +20% Hunt and +20% Forage, both scoped to `BiomeType.Arid`, sourced `(Technology, <BiomeSkillArid id>)`.
- Which modifiers a Technology grants is **catalogue data**, authored in `TechnologySeedData` and visible through the technology read, on the same footing as its prerequisites, acquisition costs and unlock grants.
- Writing those rows is **idempotent and self-correcting**: re-running it changes nothing, and re-pricing a grant followed by a re-seed corrects games already in flight without a backfill script.
- The seam is reusable by every other discovery method — trade, espionage, battlefield capture, conquest — without each one re-implementing it.

## Non-goals

These are deliberately out of scope and are separate pieces of work.

- **Applying `TechnologyGrant` unlocks.** Its TODO stands. Most of its targets — buildings, game options — have no catalogue yet, and the unlock half needs no new storage, only enum members and applier branches.
- **The other two modifier families.** `PlayerResearchModifier` and `PlayerMapResourceModifier` get their own catalogue tables on the same pattern when something needs them.
- **The other biome-skill technologies and tiers.** Forest, Grassland, Jungle, Lake, Mangrove, Woodland and Desert, and the Expertise and Mastery tiers above each, are authoring work once the mechanism is proven.
- **Settlement-scoped modifiers.** `PlayerSustenanceModifier` has no `SettlementId` and this work does not add one.
- **Making modifiers affect a Settlement's harvest.** See "Known gaps" below — this is real and this work does not fix it.
- **Entity Setup Tool editing UI.** The seed path is enough to get the mechanism working. A grid for the grants is a follow-up.

## Anti-goals

Things that would make this a failure even if the Arid bonus works.

- Two places recording what a Technology does, free to disagree — a C# map beside the catalogue.
- Modifier rows that need a hand-written backfill after a figure is re-priced.
- A reconciler that can touch a modifier row it did not create: a Specialist's or a Department's.
- A grant that silently does nothing because the named biome has no base value for the resource, discovered only by nobody noticing the bonus in play.

## Constraints

- **The percentage scale is 1,000 = 100%** (`ScaledPercent.Scale`), so +20% is raw `200`. Amounts use `ScaledAmount.Scale`, also 1,000, so a flat +5 is raw `5000`. The two scales are the same number and mean different things; conflating them is the easiest mistake here.
- **A null base can never be raised.** `SustenanceValueCalculator.CalculateEffectiveValue` returns null for a null base whatever the modifiers say, so a grant naming a biome with no base for that resource is a permanent no-op.
- **Percentages sum, they do not compound**, and apply to the base alone. Two grants of +20% on the same resource and biome give +40%, not +44%.
- A Player's effective value is **never stored**. It is derived on every read, which is what makes a grant's effect immediate and a revocation a plain delete.
- The seed and the Entity Setup Tool share one upsert path, so neither can diverge from the other in how a row is matched or scaled.
- Project conventions apply throughout: braces always and on their own line, `_camelCase` private fields, `UPPER_SNAKE_CASE` constants, LINQ in method syntax, `[Test, Description("…")]` on every test, the lightest sufficient test tier, structured log templates with named placeholders.

## Approaches considered

Two decisions were taken in conversation. Both alternatives are recorded because they are reasonable and the reasons for rejecting them are not derivable from the code.

### Where the grant is recorded

| | Approach | Verdict |
| --- | --- | --- |
| **A** | A catalogue table per modifier family, `technology.TechnologySustenanceModifierGrant`, mirroring `PlayerSustenanceModifier`'s target columns. | **Chosen.** |
| B | One generic table for all three families, with a discriminator and the union of every target column. | Rejected. |
| C | A static C# map from technology code to the rows it grants, read at discovery time. | Rejected. |

**Why A.** The figures still live in C#, in `TechnologySeedData`, under version control, changed only by a code edit and a re-seed — which is the whole of what "it never changes" asks for. The table is not a rival source of truth; it is the read model the game loop, the API and the tooling share, exactly as it already is for the technology's prerequisites, costs, unlock grants and experience sources. Two things follow from having it that C cannot give: the upsert path can **reject a grant against a null base at seed time** rather than letting it be a silent no-op in play, and the technology read can show what the Technology actually does — which for the biome-skill family is its only effect, so without it the details panel shows a Technology that appears to do nothing.

**Why not B.** The three player modifier tables already share six columns and are deliberately three tables rather than one, because their target columns differ. A generic catalogue table re-fights that and loses: the applier becomes a switch over mostly-null columns, and the editing grid is two-thirds inapplicable cells. Adding a family should be a new table, not a migration widening a live one.

**Why not C.** It saves a migration and one sync method, at the cost of no validation, an invisible effect, and an applier reading unlocks from the DbContext and modifiers from a static class.

### How the rows get written

| | Approach | Verdict |
| --- | --- | --- |
| **2** | A convergent reconciler: derive the rows the Player should have, diff against what they have, write the difference. | **Chosen.** |
| 1 | An event applier firing once at the moment of discovery. | Rejected. |

**Why 2.** It is the model this data is already designed for. `PlayerSustenanceModifier`'s remarks say losing a source "is just deleting its rows by (`SourceType`, `SourceId`) — there is no running total to unwind", and `PlayerTechnology.Status` makes the argument explicitly: a materialised cache, recomputed by one owner, idempotent, "do not optimise it away". Beyond consistency it buys two things. Re-pricing reaches live games: changing Arid from 20% to 15% and re-seeding corrects every Player on their next reconcile, where an event applier would never revisit a Player who had already discovered it. And it cannot be forgotten: every future discovery method must call the availability reconcile anyway to unlock dependents, so hanging the modifier reconcile beside it means new methods inherit it.

**Why not 1.** Nothing backfills. A grant added or re-priced after a Player discovered the Technology never reaches them.

**The cost of 2, accepted.** It converges only when something triggers a reconcile, and today that is game setup or a discovery — nothing reconciles at start of turn or after a seed, despite `PlayerTechnology`'s remark claiming all three. So a re-priced grant reaches a Player on their next discovery rather than immediately. This is a pre-existing gap that approach 1 has strictly worse, and closing it is a follow-up task, not part of this work.

## The design

### 1. The catalogue table

`technology.TechnologySustenanceModifierGrant` — one modifier one Technology grants, for one resource, in one biome or in all of them.

| Column | Type | Purpose |
| --- | --- | --- |
| `Id` | `int`, PK | Surrogate key. The natural key includes the nullable `BiomeTypeId`, so it cannot be the primary key — the same reason `PlayerSustenanceModifier` carries one. |
| `TechnologyId` | `int`, FK → `technology.Technology`, `DeleteBehavior.NoAction` | A **real foreign key**, unlike `TechnologyGrant.TargetId`: this points at exactly one table, so there is no reason to give up referential integrity. `NoAction` matches every other technology child table — `TechnologyGrant`, `TechnologyAcquisitionCost` and `TechnologyPrerequisite` all use it, and nothing in the codebase deletes a Technology today. |
| `Resource` | `int` (`SustenanceResource`) | Forage, Hunt, Fish or Fodder. |
| `BiomeTypeId` | `int`, nullable (`BiomeType`) | The biome affected, or null for every biome. |
| `FlatValue` | `bigint` | A flat amount added, scaled by `ScaledAmount.Scale`. Negative for a penalty; `0` when the grant has no flat part. |
| `PercentValue` | `bigint` | A percentage of the base added, scaled by `ScaledPercent.Scale`. Negative for a penalty; `0` when the grant has no percentage part. |

- Check constraint `CK_TechnologySustenanceModifierGrant_HasValue` — a row must carry a flat part, a percentage part, or both. Mirrors `CK_PlayerSustenanceModifier_HasValue`.
- Unique index `UX_TechnologySustenanceModifierGrant_Technology_Resource_Biome` — one grant per Technology, resource and biome. It **must** be declared `.HasFilter(null)`: EF's SQL Server convention would otherwise filter a unique index over the nullable `BiomeTypeId` to `IS NOT NULL`, which would let one Technology hold any number of all-biome grants for the same resource. Unfiltered, SQL Server treats nulls as equal, which is the rule wanted. `UX_PlayerSustenanceModifier_Player_Source_Resource_Biome` carries the same comment for the same reason.

The columns from `Resource` rightwards are `PlayerSustenanceModifier`'s target and value columns exactly. That is the point: the player table's unique key is (`PlayerId`, `SourceType`, `SourceId`, `Resource`, `BiomeTypeId`), so with `SourceType = Technology` and `SourceId = TechnologyId` a catalogue row maps one-to-one onto a player row and the reconciler is a projection rather than a transformation.

Nothing deletes a Technology today, and `NoAction` means whatever eventually does must remove its grants first — exactly the obligation `TechnologyGrant` already carries. If a Technology does leave the catalogue, the player rows it granted are left pointing at a `SourceId` that no longer resolves, and the reconciler cleans them up on its next pass, because a Technology absent from the catalogue contributes nothing to the wanted set.

### 2. Authoring and seeding

**`UpsertTechnologyModifierGrantDto`** — `Resource` (required), `BiomeTypeId` (nullable, null meaning every biome), `FlatValue` and `PercentValue`, both defaulting to `0`. Both value fields are **whole numbers as a designer writes them** and are scaled by the handler, following the biome upsert: `PercentValue = 20` means twenty percent and is stored as `200`. The DTO's remarks must say so, because the field names alone invite a raw value.

**`UpsertTechnologyDto`** gains a `ModifierGrants` collection. Its existing remark that "the four child collections are replaced wholesale" becomes five, and the new collection follows the same rule.

**`UpsertTechnologiesHandler`** gains `SyncModifierGrantsAsync`, mirroring `SyncGrantsAsync`: load the stored rows for the touched technologies, remove those not in the desired set, add or update the rest, keyed on (`Resource`, `BiomeTypeId`).

Three validations, alongside the existing `duplicateGrants` and `unknownGrantTargets` checks:

1. **Duplicate (resource, biome) within one Technology** — rejected, mirroring the unique index and the existing duplicate-grant check.
2. **Neither a flat nor a percentage part** — rejected, mirroring the check constraint.
3. **A named biome with no base value for the resource** — rejected. This is the validation the table exists to make possible: `BaseHuntValue` is null on a biome with no game, and a Hunt grant there can never do anything. The check applies only when `BiomeTypeId` is non-null; an all-biomes grant is necessarily a no-op in some biomes and that is correct, not an error.

Validation 3 makes the technology seed depend on the biome catalogue, so **`TechnologySeedStep.DependsOn` gains `SeedTarget.Biomes`**. That step's remarks already warn that its dependencies have to grow as the seed data starts naming things outside itself.

### 3. The reconciler

`IPlayerTechnologySustenanceModifierService`, in `Features/Technologies/Services`, mirroring `IPlayerTechnologyAvailabilityService` in shape and contract:

- `ReconcileAsync(context, gameId, playerId, cancellationToken) -> int`
- `ReconcileGameAsync(context, gameId, cancellationToken) -> int`
- Takes the caller's `AscendantEmpireContext` and **joins the caller's unit of work** — it never calls `SaveChangesAsync`, so the status change and the modifier rows land together or not at all.
- Returns the number of rows changed, for the caller's log line.

Per Player:

1. Read the whole grant catalogue once, untracked: `(TechnologyId, Resource, BiomeTypeId, FlatValue, PercentValue)`. A few hundred rows at most, and `ReconcileGameAsync` reads it once for every Player in the game.
2. Read the Player's discovered Technology ids — `PlayerTechnology` where `Status == TechnologyStatus.Discovered`.
3. **Wanted** = every grant whose Technology is in that set, keyed (`TechnologyId`, `Resource`, `BiomeTypeId`).
4. **Current** = the Player's tracked `PlayerSustenanceModifier` rows **where `SourceType == ModifierSourceType.Technology`**. This filter is load-bearing: a Specialist's or Department's rows must be invisible to this service, or it will delete them as unwanted.
5. Write the difference — add what is missing, update where `FlatValue` or `PercentValue` differs, remove what is no longer wanted. `GameId` and `PlayerId` come from the arguments, `SourceType` is `Technology` and `SourceId` is the `TechnologyId`.

A second pass changes nothing, which is what makes a pipeline re-run harmless and is worth a test of its own.

### 4. Call sites

Two, both immediately after the existing availability reconcile:

- **[ApplyResearch.cs:208](../../../AscendantEmpire.Application/Features/Turns/Services/TurnPipeline/Campaign/EndTurn/ApplyResearch.cs:208)** — after `technologyAvailabilityService.ReconcileAsync`, inside `SpendOnProjectAsync`, so the Technology just set to `Discovered` is in the set. The TODO at line 49 narrows to the unapplied `TechnologyGrant` unlocks.
- **[GameSetupService.cs:604](../../../AscendantEmpire.Application/Features/Game/Services/GameSetupService.cs:604)** — after `ReconcileGameAsync`, covering starter technologies. `StarterTechnologyCodes` is currently empty, so this grants nothing today and is correct the moment it is populated.

Ordering is after the availability reconcile by intent rather than necessity: that service does not change the discovered set, but "statuses settled, then effects derived" is the order to read and the order to keep as more effects join.

### 5. The seed data

In `TechnologySeedData`, on the `BiomeSkillArid` entry:

```
ModifierGrants =
[
    new UpsertTechnologyModifierGrantDto { Resource = SustenanceResource.Hunt,   BiomeTypeId = BiomeType.Arid, PercentValue = 20 },
    new UpsertTechnologyModifierGrantDto { Resource = SustenanceResource.Forage, BiomeTypeId = BiomeType.Arid, PercentValue = 20 }
]
```

`BiomeType.Arid` has `BaseForageValue = 30` and `BaseHuntValue = 30`, so both grants land on real bases and pass validation 3.

**Arid is not Desert.** Both are `BiomeType` members and both have a Biome Skill technology. `BiomeSkillArid` grants on `BiomeType.Arid` alone.

No other Technology gains a grant in this work.

## Testing

| Tier | Covers |
| --- | --- |
| No database | Nothing — the arithmetic already has `SustenanceValueCalculatorTests`, and this work adds no formula. |
| EF InMemory | The reconciler: adds the two rows for a discovered Technology, changes nothing on a second pass, updates on a re-priced grant, deletes when a grant is withdrawn, ignores an undiscovered Technology, and leaves Specialist and Department rows untouched. The upsert handler: duplicate grant rejected, valueless grant rejected, null-base biome rejected, wholesale replace of the collection. |
| Containerised SQL Server | The check constraint and the unique index, mirroring `PlayerSustenanceModifierConstraintTests`. |

One test should close the loop end to end: discover `BiomeSkillArid` through `ApplyResearch`, then assert `IPlayerSustenanceValueReader` reports 36 for Hunt and Forage on Arid against a base of 30, and that no other biome moved.

## Documentation to update

Each of these states something this work makes false.

- **`TechnologyGrant.cs`** — the paragraph saying a Technology that gives +10% forage writes the modifier row directly and that "which modifier a Technology grants is authored in C#, not as catalogue data". It becomes a pointer to the new table.
- **`PlayerSustenanceModifier.cs`** — nothing about it is wrong, but `docs/design/campaign-map/tiles.md` says "Nothing writes modifiers yet".
- **`docs/design/technology.md`** — the catalogue table list gains a row, and the open question "which technologies grant which Assets, components and templates" needs the modifier half separated from it.
- **`docs/design/campaign-map/food-yield.md`** — in "What differs per Player", the Technology modifier lever moves from "nothing writes rows" to built, and the `SourceId` row and the "Not yet implemented" bullet both change.
- **`docs/design/campaign-map/replenishment.md`** — unaffected, but it is where the harvest consequence below is designed.

## Known gaps not addressed here

- **The Arid bonus will not raise a Settlement's harvest.** `SettlementHarvestCalculator.CalculateSustenanceTake` takes `MIN(available, regrowth, staffed share)`, and regrowth is measured against the **biome's** base, so a positive modifier raises only `available`, which is never the smallest term. The bonus helps a Force feeding on Arid ground and nothing else until regrowth on an owned tile uses the owner's effective value, which `replenishment.md` designs and nothing implements. This is worth knowing before anyone tests the feature by watching a Settlement's income.
- **Nothing reconciles after a seed or at start of turn**, so a re-priced grant reaches a Player on their next discovery rather than at once.
- **Fodder grants would be inert**, since nothing eats Fodder. Not a problem for this work, which grants Hunt and Forage.

## Settled on review

Three questions a builder would have hit, decided here rather than left open.

- **The service is registered in `ApplicationStartup` as scoped and internal**, beside `IPlayerTechnologyAvailabilityService`, and follows it in lifetime and visibility.
- **`GameDeleteExecutor` needs no entry.** The new table is catalogue data with no `GameId`, shared by every game. The `PlayerSustenanceModifier` rows it leads to are already purged there.
- **`GET /technology` reports the modifier grants**, so `CatalogueTechnology`, its frontend contract and the `GetTechnologiesHandler` projection all gain the collection. Rendering them in the Entity Setup Tool's details panel is **not** in scope — but the read is, because being able to see what a Technology does is half the reason the grants are catalogue data rather than code, and a contract added later is a second change to the same three files.

## Left to the builder

- Exact type and service names, and whether the reconciler's diff is expressed as a dictionary join or a pair of set operations.
- Whether `ReconcileGameAsync` reads one Player's modifier rows at a time or the whole game's in one query — both are small.
- The log templates, subject to named placeholders and no interpolation in the message.
- The migration's name, following the `<ticket>_<Description>` convention of its neighbours.
- Test file and region layout, following the `#region` convention for test files.
