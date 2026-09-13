# Unit, Command and Force — terminology alignment

The words for "one fielded body of soldiers" and "several of them acting together" have drifted apart across the
design docs, the domain model, the wire contracts and the frontend. Four vocabularies are live at once and none
of them agrees with another. This document settles the vocabulary, records why, and sequences the change.

Tracked as **TM-6**.

## Status

| Stage | Scope | Ticket | State |
|---|---|---|---|
| 1 | `CommanderId` → `OwnerPlayerId` (no schema change) | TM-6 | **Done** — `ef7d102` |
| 2 | `RecordType.Force` → `RecordType.Command`, and populate it | TM-6 | **Done** — `6247543` |
| 3 | `TroopInstance` → `UnitInstance` — entity, table, FK columns, contracts, frontend | TM-6 | **Done** — `a7898e4` |
| 4 | The umbrella projections — `ObservedTroop` / `ITroopStub` / `DTTroopStub` → `*Force*` | TM-6 | **Done** — `76fc52a` |
| 5a | `TroopOrder*` → `ForceOrder*`, `/force-orders`, schema collapse | TM-6 | **Done** — `d55c541` |
| 5b | The remaining `Troop`-named types (`PlayerForce`, `UnitDisplayInfo`, …) | TM-6 | **Done** — `2c7a7f5` |
| 6 | `Command` entity + the nullable-pair order target (**feature work, not a rename**) | TM-6 | **Done** — schema only |
| 7 | Docs — retire Formation, rewrite `Terminology.md`, reconcile the design set | TM-6 | **Done** |

Stages 1–5 and 7 are terminology only: no behaviour changes, and the proof is an unchanged test count. Stage 6
is the first stage that adds a game concept, is excluded from the TM-6 pass, and gets its own ticket — see
[Why Command comes last](#why-command-comes-last).

### Outcome

**The suite never moved: 403 passed, 1 skipped at every stage**, against a baseline captured before stage 1,
plus 174 frontend tests across 14 files. That unchanging number is what says this was a rename. Build warnings
held at 19, all pre-existing.

**Two migrations are scaffolded and reviewed but NOT applied** — `TM-6_RenameTroopInstanceToUnitInstance` and
`TM-6_RenameTroopOrdersToForceOrders`. Applying them is Ben's to run.

What deviated from the plan, and why:

- **Stage 1 widened.** The plan renamed the troop stub's `CommanderId` and the structure stub's `OwnerId`.
  But `OwnerId` also sat on `PlayerStructure`, `IPlayerStructure`, `DTPlayerStructure` and two handlers, all
  meaning the owning player, so renaming only the map stub would have left two structure contracts disagreeing
  instead of one troop and one structure. All of them moved. `Battle.OriginalTileOwnerId` was left alone: it
  identifies the owner of a tile, not of a Force.
- **Both migrations had to be hand-written.** EF cannot infer a rename when the CLR type name changes, and
  cannot infer a schema move at all, so it scaffolded `DropTable` + `CreateTable` both times — which would have
  destroyed every fielded unit, standing order, movement step and participant row. That is what its "may result
  in the loss of data" warning meant. Both are now `RenameTable` plus the key, constraint and index renames EF
  omits on that path, with the reasoning recorded on each migration class so neither is "simplified" back.
  **Neither migration is exercised by any test** — tier-3 fixtures build their schema from the model via
  `EnsureCreatedAsync`, so these were reviewed, not proven (`AE-S5-002` in [full-review.md](full-review.md)).
- **Stage 5 ran as two commits, and absorbed the schema collapse.** The identifier rename rewrote the
  `ToTable("TroopOrders", …)` string literals as a side effect, so the table names had already moved; leaving
  the schema behind would have been a half-done state carried across a commit boundary. It went in the same
  migration.
- **Two route literals were caught by the tests, not by grep.** `ExceptionHandlingPipelineTests` and
  `GlobalExceptionHandlerTests` both hard-code `/player/troops` as a representative authenticated route, and
  the suite went red at stage 5b. Strictly the contract said to stop on any pass-count change; this was an
  incomplete rename inside the stage being executed rather than unexplained breakage, so it was finished. It is
  the exact failure mode the endpoint tests exist for — a renamed route still compiles and is only a 404 at
  runtime.
- **"Formation" was not retired from `military-organisation.md`.** Everywhere else it is gone. There it names a
  *grouping of soldiers at one tier inside a Unit* — a company, a platoon — which is genuinely none of Unit,
  Command or Force, and the plan's instruction to replace it with "tier" was wrong: a tier is the level, not
  the thing standing at it. The word is kept lower-case, defined narrowly at the top of that document, and
  [Terminology.md](../Terminology.md) records the exception rather than claiming a clean sweep.
- **`HealTroops` keeps its name.** It restores wounded soldiers, and soldiers are exactly what "troops" still
  means.

### Verified against the applied database

The first two migrations have since been applied, and the result was checked read-only against
`sys.tables`, `sys.indexes`, `sys.columns`, `sys.objects` and `sys.default_constraints`:

- The four tables are `units.UnitInstances`, `units.ForceOrders`, `units.ForceOrderMovementSteps` and
  `units.ForceOrderParticipants`. The `troops` schema holds no tables.
- **The data survived**: `units.UnitInstances` still holds its rows, which a `DropTable` would have zeroed. This
  is the proof the hand-edited migrations were worth the trouble. The three order tables were empty beforehand,
  so they prove nothing either way.
- No stale index, no stale column, and all four primary keys renamed. `TroopName` → `Name` and
  `TroopOrderId1` → `ForceOrderId1` both confirmed present under their new names.

**It also found something the scaffolder and every grep had missed.** Two default constraints still carried the
old spelling — `DF__TroopOrde__IsCom__…` and `DF__TroopOrde__WorkC__…`, created unnamed by `HasDefaultValue` and
named by SQL Server after the tables of the day. `sp_rename` on a table does not cascade into them. Cosmetic
only, since EF looks default constraints up by column rather than by name, but the database saying "Troop" after
this work is exactly the residue that makes a rename untrustworthy. Fixed by
`TM-6_RenameStaleDefaultConstraints`, which looks the names up rather than hard-coding them, because the
trailing hash differs per database.

That third migration had a bug of its own, caught by generating its SQL rather than by the compiler: both blocks
land in one batch and each declared `@currentName`, which T-SQL rejects because `DECLARE` is batch-scoped.
The variables are now suffixed per constraint. **Generate the script for any migration that writes raw
T-SQL** — `dotnet ef migrations script <from> <to>` — because nothing else checks it.

### Stage 6 — done as schema only

The suite moved to **409 passed, 1 skipped** here, and that is correct: stage 6 adds behaviour rather than
renaming it, so the unchanged-count invariant that governed stages 1–5 no longer applies. The six new tests are
the increase.

Two things had to be settled before any of it could be written, and both changed the shape:

- **There is no `Officer` entity, and no `Specialist` entity either** — only `PlayerOfficerRank`, which is
  per-player rank *configuration* rather than a person. The design's defining property of a Command ("one
  Command Commander, whose rank sets max total strength, max number of Units and max attached Specialists")
  therefore has nothing to reference. **`Command` carries no commander field**, and its XML says why: adding a
  bare rank column now would invent a shape the real relationship may not want, and none of those limits are
  enforced anywhere regardless. It gains an officer reference when officers are modelled.
- **The roadmap and `Terminology.md` said "two or more Units"; `military-organisation.md` says "one or more".**
  The design doc wins. The "not wrapped in a Command of one" ruling was about *automatic* wrapping — a player
  may still deliberately form a Command around a single Unit, it is just rarely worth the officer. Corrected in
  both places.

What landed: the `Command` entity (`units.Commands`), `UnitInstance.CommandId` nullable with a filtered index,
and on `ForceOrder` a nullable `UnitInstanceId` beside a new nullable `CommandId`, with
`CK_ForceOrders_UnitOrCommandXor` and two filtered indexes. Migration `TM-6_AddCommandAndForceOrderTarget` is
purely additive — no drop, no data loss — and its `AlterColumn` to nullable is safe because every existing row
has a Unit and no Command, so the XOR holds the moment the constraint is added.

`CommandForceOrderTargetTests` is tier 3 and the fixture exists for its two rejection cases: both target columns
are nullable, so nothing in the type system can express "exactly one", and the in-memory provider ignores
constraints entirely and would pass while proving nothing. They confirmed the constraint is really created and
really enforced, not merely scaffolded.

**Deliberately not built**, because each needs a ruling the design has not made: forming, disbanding and joining
a Command; whether unled Units may join one at all (`military-organisation.md` lists that as an uncommitted
candidate lever); and any enforcement of the officer-derived limits. Nothing therefore creates a Command yet, so
the map, vision and toolbar projections were **not** given Command arms — with nothing able to exist, those
arms would be unreachable code rather than working code. They belong with the operation that first creates one.

`ForceOrderParticipant.UnitInstanceId` is likewise still Unit-only. It wants the same nullable pair when
Commands can combine and split, and that is the same decision deferred rather than a different one.

Outstanding and Ben's to run: applying `TM-6_AddCommandAndForceOrderTarget`, and the click-through.

---

## The decision

| Term | Means | Status in code |
|---|---|---|
| **UnitInstance** | One fielded body of soldiers, built from one `UnitTemplate`, standing on one tile. | First-class entity (today misnamed `TroopInstance`). |
| **Command** | Two or more Units under a shared command structure on one campaign tile, led by a commanding officer whose rank bounds its size and attached specialists. | Does not exist yet — stage 6. |
| **Force** | Umbrella word for *"a Unit or a Command"*. **No entity, no base class, no polymorphic FK.** | A category over `RecordType`, plus flat projections that may carry the name. |
| **troops** | Individual soldiers. Lower-case, mass noun, never a group. | Already correct in `HealthyTroops` / `InjuredTroops`. |

**Retired outright:** `Formation`, `Contingent`, `Troop`-as-a-group, `Force` as a `RecordType` member,
`TroopInstance`.

**Kept as prose only:** "unit stack", meaning Units sharing a tile that are *not* under one Command. Not a type.

### Force is a category, not an enum member

Worth stating precisely, because the two are easy to conflate and an earlier draft of this document did:

- **`RecordType`** says which kind of record this is: `Unit`, `Command` or `Structure`.
- **Force** is the *category* covering `Unit` and `Command`. It has no enum member and no entity.
- So "is this a Force?" is `RecordType is Unit or Command` — a test **over** the enum, never a member **of** it.
  `Structure` is a `RecordType` and is not a Force, which is exactly why `RecordType` is the broader concept.

The "no type" rule is about the **domain**: no `Force` entity, no base class, no polymorphic foreign key. A
*projection* that genuinely describes either kind may carry the umbrella name, because that is precisely what it
describes — hence `ObservedForce` and `DTForceStub` at stage 4.

### Rulings taken

1. **A lone Unit is not wrapped in a Command of one.** A Command consumes the *(max tiers)+1* top officer rank
   (`PlayerOfficerRank.MAX_LEVEL`), so a Command per lone scout would need a Rank-6 officer each and would
   destroy the officer scarcity the whole model rests on. Command stays optional — which is precisely why an
   umbrella is needed at all.

2. **The umbrella is a word, not a type**, in the sense set out above. Where code must handle either, it takes a
   **flat projection** plus the `RecordType` discriminator. See [Why a word is enough](#why-a-word-is-enough).

3. **A Force order targets exactly one of a Unit or a Command** — nullable `UnitInstanceId` *and* nullable
   `CommandId` on the order row, with a check constraint enforcing exactly one non-null. Chosen over fanning a
   Command's order out to its member Units: "a Command moves as one" is a rule, and the nullable pair makes the
   model *state* it rather than making it something that has to be kept true across split, casualties and
   partial movement.

4. **`CommanderId` never means "owner".** It is populated today as `CommanderId = ot.PlayerId`
   (`GetPlayerCampaignMapHandler.cs:235`), so the name already lies. Once a Command has a real commanding
   officer it becomes an active trap. It becomes `OwnerPlayerId`, and `DTStructureStub.OwnerId` is aligned to
   the same name.

5. **`UnitInstance`, not bare `Unit`.** The suffix earns its keep by disambiguating from `UnitTemplate`, which
   the whole design catalogue is built around. **`Command` stays unsuffixed** — it is not built from a template,
   so there is nothing for `CommandInstance` to distinguish it from and the suffix would be noise. The
   asymmetry is deliberate, not an oversight.

6. **`troops.*` collapses into the `units` schema** at stage 5. Two schemas for one family is incoherent, and
   `troops` is retired vocabulary besides.

7. **`SplitForce`, not `SplitUnit`.** See [the note at stage 5](#stage-5--trooporder--forceorder).

---

## Why a word is enough

The pattern is already in the codebase, in the hottest read path in the game.
`BackendContracts/CampaignMaps/GetPlayerCampaignMap/MapObserver.cs` is a flat projection that anything able to
see collapses into — a structure or a troop alike:

```csharp
public record class MapObserver
{
    public required int Id { get; init; }
    public required RecordType RecordType { get; init; }
    public required int PlayerId { get; init; }
    public required Coord Location { get; init; }
    public required int SightRange { get; init; }
    public DetectionDirection SightDirection { get; init; } = DetectionDirection.FullRadius;
}
```

No base class, no inheritance, no polymorphic foreign key. `GetCampaignMapTileRadiusHandler` and
`GetPlayerCampaignMapHandler` then key their dictionaries on the discriminated pair:

```csharp
var returnData = new Dictionary<(int id, RecordType RecordType), MapObserverOriginLocation>();
```

That *is* the umbrella working as a word, already running — and note it spans `Structure` too, which is a
`RecordType` but not a Force. The three places the umbrella is genuinely load-bearing are all projections today,
so extending each to describe a Command adds fields rather than types:

| Site | Today | After |
|---|---|---|
| Vision / detection | `MapObserver` + `(id, RecordType)` | unchanged — `RecordType` finally gets populated |
| Map tile read | `DTTroopStub`, carrying an unused `RecordType` | `DTForceStub`, discriminator populated |
| Context action toolbar | `TroopActionTroop`, hand-flattened by `TroopActionContextBuilder` so the strategies never touch EF | `ForceActionForce`, same shape |

### What the word costs

Nothing enforces it. There is no compiler error when a new "acts on either" path handles only Units. The cheap
mitigation is to `switch` on `RecordType` with **no `default` arm**, so adding an enum member lights up every
incomplete switch. Note that with no analyzers and `TreatWarningsAsErrors` off across all six csprojs
(`AE-S15-002` in [full-review.md](full-review.md)), that warning currently goes nowhere.

---

## Where things stand

The four live vocabularies, for the record:

| Layer | Single thing | Group |
|---|---|---|
| Domain entity | `TroopInstance` | — does not exist |
| `Domain/Enums/RecordType.cs` | `Unit = 1` | `Force = 2` |
| Database | `units.TroopInstances`, but `troops.TroopOrders` | — |
| Frontend | `troop` | `Force` — `useForceSelection`, `selectedForce`, `"Select Force at (x, y)"` |
| `docs/Terminology.md` | `Unit` | **`Troop`** |
| `docs/design/README.md` | `Unit` | `Formation` |
| `docs/design/battlefield-maps.md` | *"Unit is used synonymously with Formation"* | — |
| `docs/design/units/military-organisation.md` | `Unit` | **`Command`** (and "formation" for tiers *inside* a Unit) |

`Formation`, `Command`, `Contingent` and `UnitStack` exist in **no** code at all — they are doc-only.
`Contingent` appears exactly once in the whole repository.

`Troop` is currently doing three jobs in code, which is the root of it:

1. the fielded single thing — `TroopInstance`
2. an **individual soldier** — `ObservedTroop.HealthyTroops` / `InjuredTroops`, and `TroopInstance`'s own XML:
   *"The number of troops currently injured in this troop"*
3. the family prefix — `TroopOrder`, `TroopActionType`, `/troop-orders`

Job 2 is the one the word is actually good at, and it survives untouched. Jobs 1 and 3 are what this document
reassigns.

### Scale

| | Occurrences | Files |
|---|---|---|
| `TroopInstance` | 164 | 39 |
| `TroopOrder` | 153 | — |
| `Troop` / `troop`, all senses | 1,841 | 105 |

Excludes `Migrations/`, `bin`, `obj`, `node_modules`.

---

## Execution protocol

- Every stage leaves the solution building and the app working end to end.
- **One commit per stage**, subject prefixed `TM-6:`, multi-line with a descriptive body covering code changes
  only.
- Record the outcome under an `#### outcome` heading, in the pattern [UD-32.md](UD-32.md) set: what deviated
  from plan, what was verified and how, and anything carried across that is wrong but deliberately left alone.
- Migrations are **scaffolded and reviewed, never applied**. Applying them is Ben's to run.
- Stop and report rather than working around, if the build hits a lock (MSB3027 — the Api dev server or VS
  holding `bin`), or if the full suite's pass count changes.

---

## Stage 1 — `CommanderId` → `OwnerPlayerId`

The smallest change with a real defect behind it, and no schema involvement — the field is projection-only.

| File | Edit |
|---|---|
| `BackendContracts/CampaignMaps/GetPlayerCampaignMap/ObservedTroop.cs` | `CommanderId` → `OwnerPlayerId` |
| `FrontendContracts/CampaignMaps/ITroopStub.cs` | same |
| `FrontendContracts/CampaignMaps/IStructureStub.cs` | `OwnerId` → `OwnerPlayerId` |
| `Application/Features/Maps/CampaignMaps/Classes/DTTroopStub.cs` | `CommanderId` → `OwnerPlayerId` |
| `Application/Features/Maps/CampaignMaps/Classes/DTStructureStub.cs` | `OwnerId` → `OwnerPlayerId` |
| `Application/Features/Maps/CampaignMaps/Handlers/GetPlayerCampaignMapHandler.cs:235` | the assignment |
| `Api/Controllers/CampaignMapController.cs:73,83` | both projections |
| `Tests/Features/Maps/CampaignMaps/PlayerCampaignMapTroopVisibilityTests.cs:131,224` | assertions |
| Frontend `features/map/components/TileTypes.ts` and its consumers | wire field rename |

**No migration.** Neither field is a persisted column.

## Stage 2 — `RecordType.Force` → `RecordType.Command`

```csharp
public enum RecordType { None = 0, Unit = 1, Command = 2, Structure = 3 }
```

**Numeric values are preserved deliberately**, because `RecordType` *is* persisted in exactly one place —
`structure.Structures.RecordTypeId` — and that column only ever holds `Structure = 3`. So the rename is a
source-level change with **no migration and no data touched**. Confirm that before the commit rather than
after.

The second half of the stage is populating the discriminator. It sits on the stubs today but is never set
meaningfully, because everything on the map is a Unit. Every projection that emits a stub should set it
explicitly rather than relying on `default`.

## Stage 3 — `TroopInstance` → `UnitInstance`

The big one, and the only stage that cannot be sliced further: a C# type rename is atomic across the solution.
39 files, compiler-verified.

**Domain and EF**

| Was | Becomes |
|---|---|
| `Domain/Entities/Units/TroopInstance.cs` | `Domain/Entities/Units/UnitInstance.cs` |
| `DbSet<TroopInstance> TroopInstances` | `DbSet<UnitInstance> UnitInstances` |
| table `units.TroopInstances` | table `units.UnitInstances` |
| `TroopOrder.TroopInstanceId` | `UnitInstanceId` *(single FK now; nullable at stage 6)* |
| `TroopOrderParticipant.TroopInstanceId` | `UnitInstanceId` |
| `TroopOrder.DefendTargetTroopInstanceId` | `DefendTargetUnitInstanceId` |
| `TroopInstance.TroopName` | `UnitInstance.Name` |
| `TroopInstance.Orders` | `UnitInstance.Orders` |

Also in scope: the four composite indexes over `TroopInstanceId` (`{GameId, TroopInstanceId, OrderStatus}`,
`{TroopInstanceId, OrderStatus}`, and the two singles), the FK constraint names
(`FK_TroopInstances_GameIndex_GameId`, `FK_TroopInstances_Player_PlayerId`), and the stale comments in
`ConfigureUnitSchema` that still name a long-dead `PlayerTroopSummary`.

**Migration.** One migration renaming the table, six columns, the indexes and the constraints. Scaffold with:

```bash
dotnet ef migrations add TM-6_RenameTroopInstanceToUnitInstance --project AscendantEmpire.Application --startup-project AscendantEmpire.Api
```

Never with `--no-build`, and **do not run `database update`** — review the generated `Up`/`Down` and hand it
over.

**Contracts and wire.** `PlayerTroop.TroopInstanceId` → `UnitInstanceId`, `MoveTroopsDto.TroopInstanceId` →
`UnitInstanceId`, `MoveTroopResult.TroopInstanceId` → `UnitInstanceId`, `TroopOrder.TroopInstanceId` →
`UnitInstanceId`. All four break the frontend, so they land in the same commit as the frontend edits — the
UD-32 discipline.

**Route.** `GET /troop-orders/troops/{troopInstanceId}/actions` becomes `.../units/{unitInstanceId}/actions`:
the plain noun in the URL segment, the suffix only on the parameter. Note this puts a `units` segment near
`/unit-designer` and `/unit-design-catalogue`, which address templates and components rather than fielded
Units. Distinct prefixes, so they cannot collide — recorded so the proximity reads as considered rather than
accidental. Update its `EndpointAuthorizationTests` row and `troopContextActionsApi.ts:37`.

**Application folders.** `Features/Troops/` holds one file (`GetTroopsByLocationHandler`) and `Features/Units/`
holds five. Collapse `Troops` into `Units` here; the empty folder goes.

## Stage 4 — the umbrella projections

Rename the shapes that describe "whatever is standing on this tile" so they carry the umbrella word:

| Was | Becomes |
|---|---|
| `BackendContracts/.../ObservedTroop.cs` | `ObservedForce.cs` |
| `FrontendContracts/CampaignMaps/ITroopStub.cs` | `IForceStub.cs` |
| `Application/.../Classes/DTTroopStub.cs` | `DTForceStub.cs` |
| `TroopActionContext.SelectedTroop` / `TroopActionTroop` | `SelectedForce` / `ForceActionForce` |
| `TroopActionContext.OtherTroopsOnTile` | `OtherForcesOnTile` |

These are the projections the "no type" rule explicitly permits to carry the name: each genuinely describes
either kind, and each already carries `RecordType` to say which.

`HealthyTroops` / `InjuredTroops` **stay exactly as they are** and become correct rather than accidental — they
were always counting individual soldiers.

**The frontend `Force*` names are already right and stay put.** `ForceSelectionDialog`, `useForceSelection`,
`selectedForce` and `"Select Force at (x, y)"` all describe "the thing the player picked on this tile, whichever
kind it is". That is the umbrella, and the frontend got there first. What changes is their *types*
(`ObservedTroopTile` → `ObservedForceTile`), not their names.

> **Superseded in part by TM-5.** `ForceSelectionDialog` and its `"Select Force at (x, y)"` title no longer exist —
> the dialog was replaced by the always-visible `ForceSelectionToolbar` (see
> [force-selection-toolbar.md](../design/units/force-selection-toolbar.md)). The naming judgement above still
> holds; only those two examples of it are gone.

## Stage 5 — `TroopOrder*` → `ForceOrder*`

An order targets a Force, so the family takes the umbrella word:

| Was | Becomes |
|---|---|
| `TroopOrder`, `TroopOrderMovementStep`, `TroopOrderParticipant` | `ForceOrder`, `ForceOrderMovementStep`, `ForceOrderParticipant` |
| `TroopOrderType`, `TroopOrderStatus`, `TroopOrderParticipantRole` | `ForceOrderType`, `ForceOrderStatus`, `ForceOrderParticipantRole` |
| `TroopActionType`, `TroopActionAvailability`, `TroopActionContext*` | `ForceActionType`, `ForceActionAvailability`, `ForceActionContext*` |
| `TroopContextAction*`, `ITroopContextActionService` | `ForceContextAction*`, `IForceContextActionService` |
| `Features/TroopOrders/` | `Features/ForceOrders/` |
| `Domain/Enums/Troops/` | `Domain/Enums/Forces/` |
| `/troop-orders` (both actions) | `/force-orders` |

### `SplitTroopsStrategy` → `SplitForceStrategy`

`ForceActionType.SplitForce`, label `"Split Force"`. **Not `SplitUnit`**, which an earlier draft of this
document recommended and which was wrong: per
[military-organisation.md](../design/units/military-organisation.md#splitting-and-merging) *both* leadership
models split at Unit level — commissioned detaches whole formations along tier lines, unled peels off quanta —
so `SplitUnit` would not have distinguished the two cases it was supposed to. The player's intent is one thing,
"divide what I have selected", so it is one action type and one button; the strategy branches internally on
`RecordType` and leadership model, which is exactly what the toolbar's three-state design (hidden /
disabled-with-reasons / enabled) exists to express.

**One strategy per action type.** `ITroopActionStrategy` exposes `ActionType` and `TroopContextActionService`
aggregates one strategy per type, ordered by the enum value. So the commissioned-Unit split and the Command
detach extend `SplitForceStrategy` internally — they must not become sibling strategies sharing `SplitForce`,
which would break the ordering and the aggregation.

**Enum numeric values must not move**: `SplitForce` keeps `1000`, because that value doubles as the toolbar's
static display order, and `TroopOrderType`'s hundreds-spacing is load-bearing the same way.

### Schema collapse

`troops.TroopOrders`, `troops.TroopOrderMovementSteps` and `troops.TroopOrderParticipants` move to the `units`
schema as `ForceOrders`, `ForceOrderMovementSteps` and `ForceOrderParticipants`, with their indexes and
constraints renamed to match. The `troops` schema is dropped. One migration.

## Stage 6 — `Command`, and the order target

**This stage is feature work, not a rename**, is excluded from TM-6, and gets its own ticket. It is why the
earlier stages are shaped as they are.

- New `Command` entity: game, player, campaign tile, commanding officer, member Units.
- `UnitInstance.CommandId`, nullable — a Unit under no Command is the ordinary case.
- `ForceOrder` gains nullable `CommandId` beside the now-nullable `UnitInstanceId`, with a check constraint
  `(UnitInstanceId IS NOT NULL AND CommandId IS NULL) OR (UnitInstanceId IS NULL AND CommandId IS NOT NULL)` —
  the same XOR shape `CK_Resource_GroupOrCategoryXor` already uses in the resources schema, so it is a pattern
  the codebase has rather than a new idea. Filtered indexes on each half.
- `RecordType.Command` starts being emitted; the map, vision, movement and toolbar projections gain their
  Command arms.

### Why Command comes last

Stage 3 names the order's foreign key `UnitInstanceId` rather than `ForceId` precisely so stage 6 adds
`CommandId` *beside* it. Naming it `ForceId` now would produce a column that claims to point at either and in
fact points at one — the silent-wrong-meaning failure this whole exercise exists to remove.

## Stage 7 — docs

- **Retire `Formation` everywhere.** It is the worst offender: a group of Units in `README.md`,
  `unit-mechanics.md`, `campaign-map.md`, `unit-support-and-supply.md` and `combat.md`; a *tier inside a Unit* in
  `military-organisation.md`; and explicitly synonymous with `Unit` in `battlefield-maps.md` (*"In this
  document, Unit is used synonymously with Formation"*). 12 files.
- **`Terminology.md` is rewritten.** Its `Troop` entry currently defines the new `Command`, and its `Contingent`
  entry has no referent anywhere. Both go; `UnitInstance`, `Command`, `Force` and `troops` replace them.
- `README.md`'s "Vocabulary at a glance" loses `Formation` and gains the four terms.
- `military-organisation.md` keeps `Command` as written — it is the source of the ruling — but its uses of
  "formation" for tiers become "tier".
- `force-context-action-toolbar.md` (renamed from `troop-context-action-toolbar.md`) and `military-organisation-implementation-notes.md` follow the code.

---

## Verification

Per stage, in order:

```bash
dotnet build AscendantEmpire.Api/AscendantEmpire.Api.csproj
```

```bash
dotnet test AscendantEmpire.Tests --filter "FullyQualifiedName~EndpointAuthorizationTests|FullyQualifiedName~EndpointIdentityBindingTests"
```

That second command is the cheap gate and needs no database. `EveryEndpoint_IsClassifiedExactlyOnce` fails if a
route was dropped or duplicated, and `EveryProtectedEndpoint_ReturnsUnauthorized_WithoutAToken` issues real
requests, so it proves a renamed route actually resolves rather than merely compiles.

```bash
npm run build --prefix AscendantEmpire.AscendantEmpireUI
```

Full suite at the end of each stage. **The unchanged pass count is the proof this was a rename** — the same
argument UD-32 closed on.

Manual click-through at the end of stages 3, 4 and 5, because no test here catches a missed `http.get` string —
that is a runtime 404 the suite cannot see. Load the campaign map, select a troop on a tile holding more than
one, and open the context toolbar.

---

## Known gaps — flag, do not fix here

1. **`RecordType` has no `default`-less switch anywhere yet.** The mitigation named above is a convention, not a
   mechanism, until analyzers exist (`AE-S15-002`).
2. **`Features/Troops/` vs `Features/Units/` vs `Features/TroopOrders/`** is three folders for one family, and
   `Features/Troops/` holds a single handler. Collapsed at stage 3; noted here so it is not "fixed" earlier.
3. **`UnitDesign`, `CompiledDesignStats` and `PlayerUnitDesignState` are legacy** and unrelated to this rename
   despite carrying `Unit`. Retiring them is its own task.
4. **`full-review.md` is a dated snapshot** and is deliberately left naming the old terms — the same call UD-32
   made about it.
