# AscendantEmpire — Database Seeding

The database seed fills a migrated-but-empty database with the reference and catalogue values the
game cannot start without: the terrain catalogues, the tag catalogue, the unit components, and the
starter unit templates every new game spawns.

It runs from the **Entity Setup Tool → Database → Seed** toolbar button, as a background job. The
dialog that opens lets you choose which parts to seed; anything the selection depends on is ticked
for you and cannot be unticked on its own.

The guiding principles:

- **Nothing is written directly.** Every step goes through the same idempotent upsert handler the
  rest of the application uses, so the seed cannot drift from hand-editing and re-running it
  converges rather than duplicating.
- **One source of truth for the graph.** The steps the executor runs and the checkboxes the dialog
  draws come from the same registered collection, so they cannot disagree.
- **The server is where correctness lives.** The dialog's auto-ticking is a courtesy. The server
  expands a selection over its prerequisites again before it runs.

---

## 1. What can be seeded

Each part is a `SeedTarget` (`AscendantEmpire.Domain/Enums/System/SeedTarget.cs`), listed under a
`SeedTargetGroup` for display.

| Target | Group | Depends on | Writes |
|---|---|---|---|
| `Geographies` | Map | — | `map.CampaignGeographies` |
| `Biomes` | Map | — | `map.CampaignBiomes` |
| `MapResourceCategories` | Map | — | `map.MapResourceCategory` |
| `TagTypes` | Unit Design | — | `unitdesigns.TagType` |
| `Tags` | Unit Design | `TagTypes` | `unitdesigns.Tag` |
| `UnitComponents` | Unit Design | `Tags` | `unitdesigns.UnitComponent` and its graph |
| `StarterTemplates` | Unit Design | `UnitComponents` | global `unitdesigns.UnitTemplate` |

### Why those edges exist

They are real constraints, not conventions:

- **`Tags` → `TagTypes`.** `Tag.TagType` doubles as a foreign key into the tag-type table
  (`FK_Tag_TagType_TagType`, `DeleteBehavior.Restrict`, configured in `AscendantEmpireContext`).
  Tag types are no longer seeded by an EF `HasData` block, so a migrated-but-unseeded database has
  none of them and a tag written against one fails at the database.
- **`UnitComponents` → `Tags`.** Components name the tags they carry by code (`TagCodes`), and the
  bulk upsert rejects a component whose tag code resolves to nothing in the payload or the database.
- **`StarterTemplates` → `UnitComponents`.** Templates name their base body and fitted components by
  code (`BaseCode`, `SlotComponentCodes`), and `UnitTemplateAssembly` resolves those against rows
  already committed — not against anything in the template payload.

The three map targets are genuinely independent of each other and of everything else.

`MapResourceTypes` are deliberately **not** seeded. Their yields, scenarios and layers name
`assets.Asset` rows the seed does not create, so a seeded type could not be guaranteed
something to point at. Types are authored in the MapResource Type Manager once the Assets exist.
If they were ever added as a target they would depend on both `MapResourceCategories` and `Biomes`.

---

## 2. The pieces

```
AscendantEmpire.Application/Features/DatabaseSeeding/
├── SeedData/      the values — one static Build() per catalogue
├── Steps/         one ISeedStep per SeedTarget, wrapping a SeedData class + its handler
├── Classes/       SeedTargetResolver, DatabaseSeedMetadata, DTSeedTarget
├── Handlers/      InitiateDatabaseSeedHandler, GetSeedTargetsHandler
└── Executors/     DatabaseSeedExecutor
```

### Seed data

`SeedData/*.cs` — `internal static` classes with a single static `Build()` returning the request DTO
its handler expects. They hold values and nothing else: no ordering, no dependencies, no dispatch.

Two conventions that are easy to get wrong, and are opposite to each other:

- **`BiomeSeedData` values are unscaled.** The handler multiplies them by 1,000 via
  `ScaledResourceValue.ToRaw` on the way in, so the figures here are the plain whole numbers a user
  types into the biome editor.
- **`UnitComponentSeedData` values are raw.** The component bulk upsert does no scaling of its own,
  so every stat is written pre-scaled — `1_000` means 1.0.

`UnitComponentSeedData` names its tags by code only. Their definitions live in `TagSeedData`, which
the `Tags` step owns, which is what lets the tag catalogue be re-seeded without the components.

### Steps

`Steps/ISeedStep.cs` is the whole abstraction. One implementation per `SeedTarget`:

```csharp
internal interface ISeedStep
{
    SeedTarget Target { get; }
    SeedTargetGroup Group { get; }
    string DisplayName { get; }                    // the dialog's label
    string Description { get; }                    // the line under it
    IReadOnlyCollection<SeedTarget> DependsOn { get; }   // direct prerequisites only
    int SortOrder { get; }                         // position within the group
    int ItemCount { get; }                         // the row count beside the label

    Task<string> ExecuteAsync(IMediator mediator, CancellationToken cancellationToken);
}
```

A step is a handful of lines: its metadata, plus one dispatch. `GeographySeedStep` in full:

```csharp
public int ItemCount => GeographySeedData.Build().Count;

public async Task<string> ExecuteAsync(IMediator mediator, CancellationToken cancellationToken)
{
    var request = new UpsertCampaignMapGeographiesRequest(GeographySeedData.Build());

    await SeedStepDispatch.SendAsync(mediator, "geographies", request, cancellationToken);

    return $"{ItemCount} geography(ies)";
}
```

`ExecuteAsync` returns its fragment of the completion summary, which the executor joins into
`ResultReference` — `"Seeded 12 biome(s), 7 map resource category(ies)."`

#### Why a step runs itself

A step sends its own request rather than handing back an `IRequest` for the executor to dispatch.
That is forced by how handlers are resolved: the handler is looked up against the response type the
*caller* supplies, not the request's runtime type. A request declaring
`ServiceResult<BulkUpsertUnitComponentsResult>` dispatched through an `IRequest<ServiceResultBase>`
parameter would not find its handler. Four of the seven steps do declare the base response type, but
the two unit-design ones return closed generics — so a uniform request-shaped interface would break
on exactly those two.

`SeedStepDispatch.SendAsync` is the shared helper that keeps each call site to one line and turns a
failed `ServiceResultBase` into an `InvalidOperationException` naming the step. Every step therefore
fails the same way, and the executor has one thing to catch.

### Registration

All seven are registered in `ApplicationStartup.AddApplicationServices`, next to the
`IProcessExecutor` block:

```csharp
services.AddScoped<ISeedStep, GeographySeedStep>();
services.AddScoped<ISeedStep, BiomeSeedStep>();
// … one line per step
```

Both `DatabaseSeedExecutor` and `GetSeedTargetsHandler` take `IEnumerable<ISeedStep>`, so the
dependency graph the dialog draws and the order the seed runs in come from the same collection.

Registration order is **not** run order — `SeedTargetResolver` sorts by group and sort order, then by
dependency, so the container is free to hand the steps over however it likes.

### The resolver

`Classes/SeedTargetResolver.cs`. Pure — no database, no mediator, just the registered steps and the
requested targets.

- `ExpandTargets(steps, requested)` → the transitively closed set of targets.
- `Resolve(steps, requested)` → the same, as `ISeedStep`s in an order safe to run.

It is a depth-first walk with a visited set (so a diamond in the graph emits its shared node once)
and an in-progress set (so a cycle is reported as a cycle rather than overflowing the stack).
Independent steps keep a stable group-then-sort-order sequence, so a full seed always runs the same
way round.

It throws — rather than skipping — on a requested target with no registered step, a dependency that
resolves to nothing, or two steps claiming the same target. The first is the only one a caller can
cause; the rest are registration mistakes that `SeedStepCatalogueTests` catches first.

---

## 3. How a seed runs

```
Dialog  ──POST /system/seed { targets:[…] }──►  InitiateDatabaseSeedHandler
                                                      │ expand over prerequisites
                                                      │ persist Processing (Pending)
                                                      │ write MetadataJson
                                                      ▼
                                               BackgroundProcessingChannel
                                                      │
                                                      ▼
                                               DatabaseSeedExecutor
                                                      │ read MetadataJson
                                                      │ resolve → ordered steps
                                                      │ run each, updating progress
                                                      ▼
Dialog  ◄──GET /system/status?processId=N───    Processing (Complete / Failed)
```

### Initiating

`InitiateDatabaseSeedHandler` rejects an empty selection, expands the rest through the resolver, then
persists a `Processing` row with `ProcessType.SeedDatabase` and `ProcessingStatus.Pending` and writes
the **expanded** list into `MetadataJson`. The row therefore records what the job will really do, not
just what was ticked. The new `ProcessingId` is returned so the caller can poll, and queued so the
dispatcher picks it up.

A `Processing` row has no foreign keys, so the selection travels as loose JSON:

```json
{ "targets": ["TagTypes", "Tags", "UnitComponents", "StarterTemplates"] }
```

`Classes/DatabaseSeedMetadata.cs` owns both ends of that — `Write` and `TryRead` — because the
handler and the executor have to agree on the property name exactly, and nothing else would catch a
mismatch until the job failed at run time. Targets are written as enum **names**, so a row stays
readable and survives a renumbering.

No migration was needed for any of this: `Processing.MetadataJson` already existed, and
`ProcessType.ExportCatalogue` already used it for its own selection flags.

### Executing

`DatabaseSeedExecutor` marks the row `InProgress` at 5%, reads the target list back, resolves it to
ordered steps, and runs them. After each step it records
`5 + (completed / total) × 90`, reaching 100% on completion.

The steps divide the bar evenly. They are not equal in cost — the components step does far more than
the geographies one — but the seed is short enough that a weighting would be precision without
purpose, and an even split cannot stall or run backwards as the selection changes.

**Metadata that cannot be read means seed everything.** That is what a job queued before the
selection existed meant, and it is the only reading that cannot silently under-seed a database
someone believed they had just filled.

### Failing

`SeedStepDispatch` throws on a failed result, the executor's outer `catch` writes
`ProcessingStatus.Failed` and the exception message into `ErrorMessage`, and the remaining steps do
not run. The save uses `CancellationToken.None` so the failure is persisted even during shutdown.

**There is no outer transaction.** Only the template step is transactional internally; the other six
each commit their own `SaveChanges`. A seed that fails part-way therefore leaves the steps before it
applied. Since every step is idempotent, the fix is to run it again.

---

## 4. The API

Both endpoints are on `SystemController`, under
`[Authorize(Policy = nameof(PermissionType.EntitySetupTool))]`.

### `GET /system/seed/targets`

Returns `DTSeedTarget[]`, ordered by group then sort order — the catalogue the dialog renders:

```json
[
  { "target": "Geographies", "group": "Map", "displayName": "Geographies",
    "description": "The elevation and movement half of a tile's terrain …",
    "dependsOn": [], "sortOrder": 10, "itemCount": 6 },
  { "target": "Tags", "group": "UnitDesign", "displayName": "Tags",
    "description": "The tag catalogue, including the settler, scout and infantry …",
    "dependsOn": ["TagTypes"], "sortOrder": 20, "itemCount": 5 }
]
```

Backed by `GetSeedTargetsHandler`, which projects the injected `IEnumerable<ISeedStep>` and reads
nothing else.

### `POST /system/seed`

```json
{ "targets": ["StarterTemplates"] }
```

Returns the bare `processId` as `201`. Prerequisites need not be listed. `400` for an empty selection
or an unknown target.

Enums are serialised as their **names** throughout, because the API registers
`JsonStringEnumConverter`. The frontend's string unions carry those names verbatim.

---

## 5. The frontend

Everything lives under `src/features/entitysetuptool/`.

| File | Role |
|---|---|
| `api/seedApi.ts` | `getSeedTargets()`, `postInitiateDatabaseSeed(targets)`, and the `SeedTarget` / `SeedTargetInfo` types mirroring the backend |
| `hooks/useSeedTargets.ts` | Loads the catalogue once (`staleTime: Infinity` — it cannot change while the page is open) |
| `hooks/useSeedSelection.ts` | Holds the tick state and derives the dependency closure |
| `components/SeedSelectionDialog.tsx` | The dialog itself |
| `hooks/useDatabaseSeed.ts` | `startSeed(targets)` → poll → done/error |

### How the auto-ticking works

`useSeedSelection` stores **only** what the user ticked. The implied set is recomputed from that on
every render by walking `dependsOn`:

- `explicitTargets` — ticked by the user. Editable.
- `impliedTargets` — reachable from an explicit tick, minus the explicit ones. Ticked and **disabled**.
- `submitTargets` — the union, which is what `onCommit` receives.

Because `impliedTargets` is derived rather than stored, unticking a parent releases its prerequisites
for free: there is no second piece of state that could disagree about why a row is ticked. A target
the user ticked *and* which something else needs stays explicit, so it stays editable.

Each locked row says what put it there — *"Required by Starter Unit Templates"* — so the lock is
explained rather than just imposed.

### Progress

Unchanged from before: `useDatabaseSeed` hands the `processId` to the shared
`useProcessStatus` (`GET /system/status`, 2 s interval, stops at a terminal status), and
`EntitySetupTool` maps the phase onto `ProgressOperationDialog` for progress and success, and the
copyable `ExportDialog` for the full server error text.

> The per-step summary the executor writes into `ResultReference` does **not** reach the UI —
> `DTProcessingStatus` deliberately omits that field. Surfacing it is tracked as **G-147**.

---

## 6. Adding a new seed target

1. Add a `SeedData` class with a static `Build()` returning the request DTO its handler expects.
2. Add a member to `SeedTarget`, with an explicit number. Do not renumber the existing ones —
   queued jobs carry the names, but the numbers are the stored values.
3. Add an `ISeedStep`: its metadata, its `DependsOn`, and one `SeedStepDispatch.SendAsync` call.
4. Register it in `ApplicationStartup`.

That is all. The dialog picks it up from `GET /system/seed/targets` with no frontend change.

If you need an idempotent path that does not exist yet, add the handler rather than writing to the
context from the step — that is what `BulkUpsertTagsHandler` is. It exists because the component
bulk upsert refuses an empty component list (rightly: it is the catalogue-transfer path), which left
no way to refresh the tag catalogue alone. Both it and `BulkUpsertUnitComponentsHandler` share
`TagCatalogueUpserter`, so they create, re-classify and validate tags identically.

---

## 7. Two behaviours worth knowing

**Seeding components is not inert.** `BulkUpsertUnitComponentsHandler` collects the templates built
on the components it is about to touch and recompiles them afterwards through
`AffectedTemplateRecompiler`. Ticking `UnitComponents` alone therefore refreshes the compiled
snapshot of every template standing on those components, including ones you authored by hand. That
is correct — a changed component *should* propagate — but it means the selection is narrower in what
it writes than in what it affects. The dialog says so.

**Renaming a slot code is destructive.** Slots are matched by `Code` (their `Id` is left null in the
seed data), so re-running the seed updates existing slot rows in place and the template components
fitted into them survive. Renaming a slot's code instead drops the old slot and everything fitted to
it.

---

## 8. Tests

In `AscendantEmpire.Tests/Features/DatabaseSeeding/`, following the tiers in
[testing.md](testing.md).

**Tier 1 — no database:**

- `SeedTargetResolverTests` — expansion, ordering, diamonds, and the three throwing cases. Uses fake
  steps so the shapes under test can be built deliberately.
- `SeedStepCatalogueTests` — resolves the steps out of a container built by the *real* registration
  method, then pins that every `SeedTarget` has exactly one step, every `DependsOn` resolves, the
  graph is acyclic, and every step carries the metadata the dialog renders. **This is the guard
  against adding a seed-data class and its enum member but forgetting the step** — without it the
  target would simply never appear and never be seeded, with nothing to say so.
- `DatabaseSeedMetadataTests` — round-tripping, and the cases `TryRead` must treat as unreadable.
- `UnitDesignSeedDataTests` — that the seeded catalogue is coherent: the starter codes game setup
  demands, the slots and components the templates name, and the single unled organisation tag each
  starter must resolve to.

**Tier 2 — in-memory:**

- `InitiateDatabaseSeedHandlerTests` — that the **expanded** list reaches `MetadataJson`, that the
  returned id is the one queued, and that empty and unresolvable selections are refused without
  queueing anything.

**Frontend:** `components/__tests__/SeedSelectionDialog.test.tsx` — the closure behaviour (ticking,
locking, releasing, explicit-beats-implied), what `onCommit` receives, and the loading/error states.
