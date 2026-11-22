# Technology Sustenance Modifier Grants — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make discovering a Technology grant its Player sustenance modifiers, proved by `BiomeSkillArid` giving +20% Hunt and +20% Forage on Arid tiles.

**Architecture:** A new catalogue table `technology.TechnologySustenanceModifierGrant` records which modifiers a Technology grants, authored in `TechnologySeedData` and written through the existing technology upsert. A new convergent service derives a Player's Technology-sourced `PlayerSustenanceModifier` rows from their Discovered set crossed with that catalogue and writes the difference, called immediately after the existing availability reconcile at both discovery points. Nothing stores an effective value; `IPlayerSustenanceValueReader` already derives it.

**Tech Stack:** .NET 9 / EF Core (SQL Server), MediatR, NUnit + NSubstitute. The React frontend is untouched — nothing renders a technology's grants beyond what already exists, so no TypeScript type changes and no vitest run.

**Spec:** [docs/superpowers/specs/2026-11-22-technology-sustenance-modifier-grants-design.md](../specs/2026-11-22-technology-sustenance-modifier-grants-design.md)

**Planner tasks:** Task 1 is `T-10`, Task 2 is `T-11`, Task 3 is `T-12`. One commit per task, subject prefixed with that code.

## Global Constraints

- Braces on every `if`/`for`, even single-line bodies, and on their own line.
- C#: private fields `_camelCase`, constants `UPPER_SNAKE_CASE`, private constants both (`_MAX_X`). Locals and parameters stay `camelCase`. Test projects included.
- LINQ in method syntax, never query syntax. Project through mapped navigations rather than writing `.Join(…)`.
- 1–2 line `<summary>` with tags on their own line, rationale in `<remarks>`/`<para>`; `<param>` only where the parameter is not obvious. Comments sparing and short, wrapped at about 200 characters. No counts or enumerations in comments.
- Markdown: never wrap to a column — one paragraph, bullet or table row is one unbroken line.
- Every test carries `[Test, Description("…")]` — one line, stating what it pins and why. Test names read `Should<Outcome>When<Condition>`. Group tests and helpers with `#region <Name>` / `#endregion`, never banner comments.
- Lightest sufficient test tier: no database → EF InMemory → containerised SQL Server. See [docs/testing.md](../../testing.md). Only the constraint tests need the container.
- The tier-3 container pins port 14330, so runs collide with a Visual Studio run. Use a targeted `--filter`; never run the full suite unprompted.
- Never `dotnet test -v q`, and never pipe a test run through `tail` — grep for failure markers instead.
- Scaffold migrations with `--project AscendantEmpire.Application --startup-project AscendantEmpire.Api`. Never pass `--no-build`. **Never run `database update`.**
- `ScaledPercent.Scale` and `ScaledAmount.Scale` are both `1_000`, and they mean different things: 1,000 is 100% for a percentage and 1 unit for an amount. **+20% is raw `200`.** Convert with `ScaledPercent.FromWholePercent(…)` and `ScaledAmount.FromWhole(…)`, never a literal.
- **Claude never runs git and never applies a migration.** Each task's commit message is handed to Ben inline as a fenced block and he commits. No `Co-Authored-By` trailer.

## Review Focus

Five conditions the spec implies that no task's own happy path would catch. Each has a test added to the task that owns the code.

- **A Technology that is `Unlocked` but not `Discovered` must grant nothing.** `TechnologyStatus` is deliberately ordered so "at least unlocked" is one comparison, which invites `>= TechnologyStatus.Unlocked` where only `== TechnologyStatus.Discovered` is correct. Pinned in Task 2.
- **Two discovered technologies granting the same resource and biome must both land**, as two rows with different `SourceId`, summing to +40% rather than one row overwriting the other. Pinned in Task 2.
- **A second all-biomes grant for the same resource on one Technology must be rejected by the unique index.** Without `.HasFilter(null)` EF filters the index to `IS NOT NULL` and both rows insert. Pinned in Task 1.
- **A negative grant must survive the check constraint and reconcile through as a penalty.** `CK_…_HasValue` tests `<> 0`, so −20% is valid data and must not be floored on the way in. Pinned in Tasks 1 and 2.
- **An all-biomes grant must be silently inert in a biome with no base for that resource** — no seed-time error, no read-time throw, and no modifier row that pretends to do something. Pinned in Task 2.

---

## File Structure

**Created**

| File | Responsibility |
| --- | --- |
| `AscendantEmpire.Domain/Entities/Technologies/TechnologySustenanceModifierGrant.cs` | The catalogue row. |
| `AscendantEmpire.BackendContracts/Technologies/UpsertTechnologySustenanceModifierGrantDto.cs` | One grant as authored, in whole numbers. |
| `AscendantEmpire.BackendContracts/Technologies/GetTechnologies/CatalogueSustenanceModifierGrant.cs` | One grant as read. |
| `AscendantEmpire.FrontendContracts/Technologies/GetTechnologies/ICatalogueSustenanceModifierGrant.cs` | The read contract's interface. |
| `AscendantEmpire.Application/Features/Technologies/Classes/DTCatalogueSustenanceModifierGrant.cs` | The interface's implementation. |
| `AscendantEmpire.Application/Features/Technologies/Services/IPlayerTechnologySustenanceModifierService.cs` | The reconciler's contract and the reasoning for converging rather than firing on discovery. |
| `AscendantEmpire.Application/Features/Technologies/Services/PlayerTechnologySustenanceModifierService.cs` | The reconciler. |
| `AscendantEmpire.Tests/Features/Technologies/TechnologySustenanceModifierGrantConstraintTests.cs` | Tier 3: the check constraint and the unique index. |
| `AscendantEmpire.Tests/Features/Technologies/PlayerTechnologySustenanceModifierServiceTests.cs` | Tier 2: the reconciler. |
| `AscendantEmpire.Tests/Features/Technologies/BiomeSkillAridGrantTests.cs` | Tier 2: discovery through to the reader. |

**Modified**

| File | Change |
| --- | --- |
| `AscendantEmpire.Application/Data/AscendantEmpireContext.cs` | A `DbSet` and a model configuration block. |
| `AscendantEmpire.BackendContracts/Technologies/UpsertTechnologyDto.cs` | A `SustenanceModifierGrants` collection; the "four child collections" remark becomes five. |
| `AscendantEmpire.BackendContracts/Technologies/GetTechnologies/CatalogueTechnology.cs` · `AscendantEmpire.FrontendContracts/Technologies/GetTechnologies/ICatalogueTechnology.cs` · `AscendantEmpire.Application/Features/Technologies/Classes/DTCatalogueTechnology.cs` | A `SustenanceModifierGrants` collection. |
| `AscendantEmpire.Application/Features/Technologies/Handlers/UpsertTechnologiesHandler.cs` | `ValidateShape`, `ValidateReferences`, `LoadReferenceLookupsAsync`, `ReferenceLookups`, a new `SyncSustenanceModifierGrantsAsync`, and the summary log. |
| `AscendantEmpire.Application/Features/Technologies/Handlers/GetTechnologiesHandler.cs` | Project the new collection. |
| `AscendantEmpire.Application/Features/DatabaseSeeding/Steps/TechnologySeedStep.cs` | `DependsOn` gains `SeedTarget.Biomes`. |
| `AscendantEmpire.Application/Features/DatabaseSeeding/SeedData/TechnologySeedData.cs` | The two Arid grants. |
| `AscendantEmpire.Application/ApplicationStartup.cs` | Register the reconciler. |
| `AscendantEmpire.Application/Features/Turns/.../EndTurn/ApplyResearch.cs` · `AscendantEmpire.Application/Features/Game/Services/GameSetupService.cs` | Call the reconciler; narrow `ApplyResearch`'s TODO. |
| `AscendantEmpire.Domain/Entities/Technologies/TechnologyGrant.cs` · `docs/design/campaign-map/tiles.md` · `docs/design/technology.md` · `docs/design/campaign-map/food-yield.md` | Correct what this work makes false. |

---

## Task 1 (T-10): The grant catalogue and its authoring path

**Files:**
- Create: `AscendantEmpire.Domain/Entities/Technologies/TechnologySustenanceModifierGrant.cs`
- Create: `AscendantEmpire.BackendContracts/Technologies/UpsertTechnologySustenanceModifierGrantDto.cs`
- Create: `AscendantEmpire.BackendContracts/Technologies/GetTechnologies/CatalogueSustenanceModifierGrant.cs`
- Create: `AscendantEmpire.FrontendContracts/Technologies/GetTechnologies/ICatalogueSustenanceModifierGrant.cs`
- Create: `AscendantEmpire.Application/Features/Technologies/Classes/DTCatalogueSustenanceModifierGrant.cs`
- Modify: `AscendantEmpire.Application/Data/AscendantEmpireContext.cs` (`DbSet` beside `TechnologyGrants` at ~329; configuration after the `TechnologyGrant` block at ~4200)
- Modify: `AscendantEmpire.BackendContracts/Technologies/UpsertTechnologyDto.cs`
- Modify: `AscendantEmpire.BackendContracts/Technologies/GetTechnologies/CatalogueTechnology.cs`, `AscendantEmpire.FrontendContracts/Technologies/GetTechnologies/ICatalogueTechnology.cs`, `AscendantEmpire.Application/Features/Technologies/Classes/DTCatalogueTechnology.cs`
- Modify: `AscendantEmpire.Application/Features/Technologies/Handlers/UpsertTechnologiesHandler.cs`
- Modify: `AscendantEmpire.Application/Features/Technologies/Handlers/GetTechnologiesHandler.cs`
- Modify: `AscendantEmpire.Application/Features/DatabaseSeeding/Steps/TechnologySeedStep.cs`
- Test: `AscendantEmpire.Tests/Features/Technologies/TechnologySustenanceModifierGrantConstraintTests.cs` (new), `AscendantEmpire.Tests/Features/Technologies/UpsertTechnologiesHandlerTests.cs` (extend), `AscendantEmpire.Tests/Features/Technologies/GetTechnologiesHandlerTests.cs` (extend)

**Interfaces:**
- Consumes: nothing from other tasks.
- Produces: `TechnologySustenanceModifierGrant` with `int Id`, `int TechnologyId`, `SustenanceResource Resource`, `BiomeType? BiomeTypeId`, `long FlatValue`, `long PercentValue`, `Technology Technology`; `Technology.SustenanceModifierGrants` as `ICollection<TechnologySustenanceModifierGrant>`; `AscendantEmpireContext.TechnologySustenanceModifierGrants`; `UpsertTechnologySustenanceModifierGrantDto` with `SustenanceResource Resource` (required), `BiomeType? BiomeTypeId`, `int FlatValue`, `int PercentValue`; `UpsertTechnologyDto.SustenanceModifierGrants` as `IReadOnlyCollection<UpsertTechnologySustenanceModifierGrantDto>`.

- [ ] **Step 1: Write the failing constraint tests**

New file, `[TestFixture] internal class TechnologySustenanceModifierGrantConstraintTests : SqlServerTestBase`, following `PlayerSustenanceModifierConstraintTests` — a `[SetUp]` that seeds one `Technology` and ensures `BiomeType.Arid` and `BiomeType.Grassland` exist in `CampaignBiomes`, since biomes are seeded by the Entity Setup Tool rather than by a migration. Store values raw, as the database holds them.

```csharp
#region Unique index
ShouldRejectWhenSameTechnologyResourceAndBiomeInsertedTwice      // two rows, Hunt + Arid -> DbUpdateException
ShouldRejectWhenSameTechnologyAndResourceGrantedForAllBiomesTwice // two rows, Hunt + null biome -> DbUpdateException; pins HasFilter(null)
ShouldAllowWhenSameResourceGrantedInDifferentBiomes              // Hunt + Arid and Hunt + Grassland both insert
ShouldAllowWhenSameBiomeGrantedForDifferentResources             // Hunt + Arid and Forage + Arid both insert
#endregion

#region Check constraint
ShouldRejectWhenNeitherFlatNorPercentIsSet                       // FlatValue 0, PercentValue 0 -> DbUpdateException
ShouldAllowWhenOnlyFlatIsSet                                     // FlatValue 5000, PercentValue 0
ShouldAllowWhenOnlyPercentIsSet                                  // FlatValue 0, PercentValue 200
ShouldAllowWhenValuesAreNegative                                 // PercentValue -200 inserts; a penalty is valid data
#endregion
```

- [ ] **Step 2: Run the constraint tests to verify they fail**

Run: `dotnet test AscendantEmpire.Tests --filter FullyQualifiedName~TechnologySustenanceModifierGrantConstraintTests`
Expected: FAIL — `TechnologySustenanceModifierGrants` does not exist on the context.

- [ ] **Step 3: Add the entity**

`TechnologySustenanceModifierGrant` in `AscendantEmpire.Domain/Entities/Technologies/`, with the properties in the Interfaces block. Its `<remarks>` carry the two things the type cannot show: that `Resource` rightwards mirrors `PlayerSustenanceModifier`'s target and value columns so the reconciler is a projection rather than a transformation, and that a null `BiomeTypeId` means every biome. Add `SustenanceModifierGrants` to `Technology` beside `Grants`.

- [ ] **Step 4: Add the `DbSet` and the model configuration**

`DbSet` beside `TechnologyGrants`. Configuration in a `// --- TechnologySustenanceModifierGrant ---` block after the `TechnologyGrant` one, following `PlayerSustenanceModifier`'s block at ~1397 for the check constraint and the index, and `TechnologyGrant`'s at ~4179 for the relationship:

- `entity.ToTable("TechnologySustenanceModifierGrant", schema, table => table.HasCheckConstraint("CK_TechnologySustenanceModifierGrant_HasValue", "[FlatValue] <> 0 OR [PercentValue] <> 0"))`.
- `entity.HasKey(e => e.Id)` — a surrogate key, because the natural key includes the nullable `BiomeTypeId`.
- `Resource` converted to `int` and required.
- `HasOne(e => e.Technology).WithMany(e => e.SustenanceModifierGrants).HasForeignKey(e => e.TechnologyId).OnDelete(DeleteBehavior.NoAction)`, named `FK_TechnologySustenanceModifierGrant_Technology_TechnologyId`. `NoAction` matches every other technology child table.
- An optional `HasOne<CampaignBiome>().WithMany().HasForeignKey(e => e.BiomeTypeId).IsRequired(false).OnDelete(DeleteBehavior.NoAction)`, named `FK_TechnologySustenanceModifierGrant_CampaignBiome_BiomeTypeId`.
- `entity.HasIndex(e => new { e.TechnologyId, e.Resource, e.BiomeTypeId }).IsUnique().HasFilter(null).HasDatabaseName("UX_TechnologySustenanceModifierGrant_Technology_Resource_Biome")`. **`.HasFilter(null)` is required** — carry the comment explaining why, as `PlayerSustenanceModifier` does.

- [ ] **Step 5: Scaffold the migration**

Run: `dotnet ef migrations add T-10_AddTechnologySustenanceModifierGrant --project AscendantEmpire.Application --startup-project AscendantEmpire.Api`
Expected: a migration creating the table, the check constraint and the unique index. **Do not apply it.** Read the generated `Up` and confirm the index carries no `filter:` argument.

- [ ] **Step 6: Run the constraint tests to verify they pass**

Run: `dotnet test AscendantEmpire.Tests --filter FullyQualifiedName~TechnologySustenanceModifierGrantConstraintTests`
Expected: PASS. The per-test database is restored from the migrated template, so this also proves the migration created both rules.

- [ ] **Step 7: Write the failing authoring tests**

Extend `UpsertTechnologiesHandlerTests` in a `#region Modifier grants` region:

```csharp
ShouldRejectWhenTechnologyGrantsSameResourceAndBiomeTwice   // error mentions the technology code
ShouldRejectWhenModifierGrantHasNeitherFlatNorPercent
ShouldRejectWhenModifierGrantNamesBiomeWithNoBaseForResource // Fish on Arid, whose BaseFishValue is null
ShouldAllowWhenModifierGrantAppliesToEveryBiome              // null BiomeTypeId skips the base check
ShouldScaleModifierGrantValuesWhenWritten                    // PercentValue 20 -> stored 200; FlatValue 5 -> stored 5000
ShouldReplaceModifierGrantsWholesaleWhenUpserted             // a second upsert with one grant leaves exactly one row
ShouldClearModifierGrantsWhenEmptyCollectionSupplied
```

- [ ] **Step 8: Run the authoring tests to verify they fail**

Run: `dotnet test AscendantEmpire.Tests --filter FullyQualifiedName~UpsertTechnologiesHandlerTests`
Expected: FAIL — `UpsertTechnologyDto` has no `SustenanceModifierGrants`.

- [ ] **Step 9: Add the DTO and the collection**

`UpsertTechnologySustenanceModifierGrantDto` with the properties in the Interfaces block. `FlatValue` and `PercentValue` are **whole numbers as a designer writes them**, scaled by the handler; say so in the remarks, because the names invite a raw value. Add `SustenanceModifierGrants` to `UpsertTechnologyDto`, defaulting to `[]`, and change its "four child collections" remark to five.

- [ ] **Step 10: Add the validations**

In `ValidateShape`, two checks following the `duplicateGrants` block at ~301: a duplicate `(Resource, BiomeTypeId)` within one Technology, and a grant with `FlatValue == 0 && PercentValue == 0`.

In `LoadReferenceLookupsAsync`, read the biomes named by modifier grants — distinct non-null `BiomeTypeId`s — projecting the four `Base*Value` columns, and build a `HashSet<(BiomeType Biome, SustenanceResource Resource)>` of the pairs that **have** a base. Skip the query when no grant names a biome, as the other lookups skip theirs. Add it to `ReferenceLookups` with a `HasBase(BiomeType biome, SustenanceResource resource)` method beside `ResolveGrantTarget`.

In `ValidateReferences`, reject a grant whose non-null biome has no base for its resource. Word the error like the existing unresolvable-target one: name the pairs and say the Biomes catalogue must be seeded first.

- [ ] **Step 11: Implement `SyncSustenanceModifierGrantsAsync`**

Mirror `SyncGrantsAsync` at ~585 exactly — same parameters minus `lookups`, same `stored`/`current`/`desired` shape, keyed on `(Resource, BiomeTypeId)`. Scale on the way in with `ScaledAmount.FromWhole` and `ScaledPercent.FromWholePercent`. Call it beside `SyncGrantsAsync` and add the count to the summary log.

- [ ] **Step 12: Add `SeedTarget.Biomes` to the seed step**

`TechnologySeedStep.DependsOn` becomes `[SeedTarget.ExperienceTypes, SeedTarget.Biomes]`, and the remark explaining why gains the biome reason.

- [ ] **Step 13: Run the authoring tests to verify they pass**

Run: `dotnet test AscendantEmpire.Tests --filter FullyQualifiedName~UpsertTechnologiesHandlerTests`
Expected: PASS.

- [ ] **Step 14: Add the catalogue read**

`ICatalogueSustenanceModifierGrant`, `CatalogueSustenanceModifierGrant` and `DTCatalogueSustenanceModifierGrant`, each carrying `Resource`, `BiomeTypeId`, `FlatValue` and `PercentValue` — **raw stored values**, consistent with how the biome read returns its bases. A `SustenanceModifierGrants` collection on all three technology contracts, and the projection in `GetTechnologiesHandler` beside `RawGrants`. No target resolution is needed: `Resource` and `BiomeTypeId` are enums, not foreign keys into five catalogues.

Extend `GetTechnologiesHandlerTests` with `ShouldReturnModifierGrantsWhenTechnologyHasThem`, asserting the raw values come back unscaled.

- [ ] **Step 15: Run the technology tests to verify they pass**

Run: `dotnet test AscendantEmpire.Tests --filter FullyQualifiedName~AscendantEmpire.Tests.Features.Technologies`
Expected: PASS.

- [ ] **Step 16: Hand Ben the commit message**

Run the full backend suite first — `dotnet test AscendantEmpire.Tests` — and report any failure rather than writing a message. The frontend is untouched, so no vitest run. If green, write the `T-10:` message inline as a fenced block covering the code changes only, not the doc or plan edits. Ben commits.

---

## Task 2 (T-11): The discovery reconciler

**Files:**
- Create: `AscendantEmpire.Application/Features/Technologies/Services/IPlayerTechnologySustenanceModifierService.cs`
- Create: `AscendantEmpire.Application/Features/Technologies/Services/PlayerTechnologySustenanceModifierService.cs`
- Modify: `AscendantEmpire.Application/ApplicationStartup.cs:228` (beside `IPlayerTechnologyAvailabilityService`)
- Modify: `AscendantEmpire.Application/Features/Turns/Services/TurnPipeline/Campaign/EndTurn/ApplyResearch.cs` (constructor, the TODO at ~49, and after the reconcile at ~208)
- Modify: `AscendantEmpire.Application/Features/Game/Services/GameSetupService.cs` (constructor and after `ReconcileGameAsync` at ~604)
- Test: `AscendantEmpire.Tests/Features/Technologies/PlayerTechnologySustenanceModifierServiceTests.cs` (new); `AscendantEmpire.Tests/Features/Turns/Services/TurnPipeline/ApplyResearchTests.cs` and `AscendantEmpire.Tests/Features/Game/Services/GameSetupServiceTests.cs` and `AscendantEmpire.Tests/Features/Turns/Services/TurnPipelineExecutionOrderTests.cs` (add the substitute)

**Interfaces:**
- Consumes: `TechnologySustenanceModifierGrant` and `AscendantEmpireContext.TechnologySustenanceModifierGrants` from Task 1.
- Produces: `IPlayerTechnologySustenanceModifierService` with `Task<int> ReconcileAsync(AscendantEmpireContext context, int gameId, int playerId, CancellationToken cancellationToken)` and `Task<int> ReconcileGameAsync(AscendantEmpireContext context, int gameId, CancellationToken cancellationToken)`. Both join the caller's unit of work and never save.

- [ ] **Step 1: Write the failing reconciler tests**

New file, `[TestFixture] internal class PlayerTechnologySustenanceModifierServiceTests`, EF InMemory, following `PlayerSustenanceValueReaderTests` for setup shape. Regions: `#region Granting`, `#region Convergence`, `#region Scope`, `#region Helpers`.

```csharp
#region Granting
ShouldAddRowsWhenTechnologyIsDiscovered                  // two grants -> two rows, SourceType Technology, SourceId the technology id
ShouldCopyNegativeValuesWhenGrantIsAPenalty              // PercentValue -200 arrives as -200, not floored
ShouldAddRowForEveryBiomeWhenGrantHasNullBiome           // one row with a null BiomeTypeId, not one row per biome
ShouldAddRowsFromBothWhenTwoTechnologiesGrantSameResourceAndBiome // two rows, different SourceId
#endregion

#region Convergence
ShouldChangeNothingWhenRunTwice                          // second call returns 0 and row count is unchanged
ShouldUpdateValuesWhenGrantIsRepriced
ShouldRemoveRowWhenGrantIsWithdrawnFromCatalogue
ShouldRemoveRowWhenTechnologyLeavesCatalogue
#endregion

#region Scope
ShouldAddNothingWhenTechnologyIsOnlyUnlocked             // Status Unlocked, not Discovered
ShouldAddNothingWhenTechnologyIsNotDiscovered            // Locked and Available
ShouldLeaveRowsAloneWhenSourceIsSpecialistOrDepartment   // pre-seeded rows survive a reconcile that wants none
ShouldReconcileEveryPlayerWhenGameIsReconciled           // two players, one discovered; only that one gains rows
#endregion
```

- [ ] **Step 2: Run the reconciler tests to verify they fail**

Run: `dotnet test AscendantEmpire.Tests --filter FullyQualifiedName~PlayerTechnologySustenanceModifierServiceTests`
Expected: FAIL — the service does not exist.

- [ ] **Step 3: Write the interface**

`IPlayerTechnologySustenanceModifierService`, `internal`, with the two signatures in the Interfaces block. Its `<remarks>` carry what the signatures cannot: that it converges rather than firing on discovery, so a re-priced grant corrects itself and a new discovery method inherits it by reconciling availability; that it joins the caller's unit of work; and that it reads only `SourceType == Technology`, because the other sources' rows are not its to manage. Model the prose on `IPlayerTechnologyAvailabilityService`.

- [ ] **Step 4: Implement the service**

`PlayerTechnologySustenanceModifierService`, `internal sealed`, taking `ILogger<PlayerTechnologySustenanceModifierService>`. `ReconcileGameAsync` loads the grant catalogue once and loops the game's Players, as `PlayerTechnologyAvailabilityService` does with its graph. Per Player:

1. Wanted — grants whose `TechnologyId` is in the Player's Discovered set, keyed `(TechnologyId, Resource, BiomeTypeId)`.
2. Current — tracked `PlayerSustenanceModifier` rows for that Player **where `SourceType == ModifierSourceType.Technology`**.
3. Add, update where `FlatValue` or `PercentValue` differs, remove the rest. Return the count of all three.

Log once per call with named placeholders: added, updated and removed counts, the Player and the Game.

- [ ] **Step 5: Run the reconciler tests to verify they pass**

Run: `dotnet test AscendantEmpire.Tests --filter FullyQualifiedName~PlayerTechnologySustenanceModifierServiceTests`
Expected: PASS.

- [ ] **Step 6: Register and wire both call sites**

Register in `ApplicationStartup` as scoped, beside the availability service. Inject into `ApplyResearch` and `GameSetupService`, and call immediately after the existing reconcile in each — `ReconcileAsync` in `ApplyResearch.SpendOnProjectAsync`, `ReconcileGameAsync` in `GameSetupService`. Fold the returned count into each existing log line rather than adding a second one. Narrow `ApplyResearch`'s TODO at ~49 to the still-unapplied `TechnologyGrant` unlocks. Add the substitute to the three test fixtures that construct these types.

- [ ] **Step 7: Run the affected suites to verify they pass**

Run: `dotnet test AscendantEmpire.Tests --filter "FullyQualifiedName~ApplyResearchTests|FullyQualifiedName~GameSetupServiceTests|FullyQualifiedName~TurnPipelineExecutionOrderTests"`
Expected: PASS.

- [ ] **Step 8: Hand Ben the commit message**

Full backend suite, report failures, then the `T-11:` message inline as a fenced block if green.

---

## Task 3 (T-12): The Arid grant and the document corrections

**Files:**
- Modify: `AscendantEmpire.Application/Features/DatabaseSeeding/SeedData/TechnologySeedData.cs` (the `BiomeSkillArid` entry at ~1034)
- Modify: `AscendantEmpire.Domain/Entities/Technologies/TechnologyGrant.cs`, `docs/design/campaign-map/tiles.md`, `docs/design/technology.md`, `docs/design/campaign-map/food-yield.md`
- Test: `AscendantEmpire.Tests/Features/Technologies/BiomeSkillAridGrantTests.cs` (new)

**Interfaces:**
- Consumes: `UpsertTechnologySustenanceModifierGrantDto` from Task 1 and `IPlayerTechnologySustenanceModifierService` from Task 2.
- Produces: nothing other tasks rely on.

- [ ] **Step 1: Write the failing end-to-end test**

New file, EF InMemory, seeding the Arid biome with `BaseForageValue` and `BaseHuntValue` of 30 raw-scaled, the `BiomeSkillArid` Technology with its two grants, and one Player.

```csharp
ShouldRaiseHuntAndForageOnAridWhenBiomeSkillAridIsDiscovered  // reconcile, then IPlayerSustenanceValueReader gives 36 for Hunt and Forage on Arid
ShouldLeaveOtherBiomesAloneWhenBiomeSkillAridIsDiscovered     // Grassland and Desert unchanged
ShouldLeaveFishAndFodderAloneWhenBiomeSkillAridIsDiscovered   // the two ungranted resources on Arid
ShouldGiveNothingWhenBiomeSkillAridIsNotDiscovered            // the base, 30
ShouldBeInertWhereBiomeHasNoBase                              // an all-biomes Hunt grant leaves a Hunt-less biome null, not 0
ShouldSumBothWhenTwoTechnologiesGrantTheSameResourceAndBiome  // a second +20% Hunt on Arid gives 42, not 43.2 — percentages sum, they do not compound
```

- [ ] **Step 2: Run it to verify it fails**

Run: `dotnet test AscendantEmpire.Tests --filter FullyQualifiedName~BiomeSkillAridGrantTests`
Expected: FAIL — the reader returns 30, because no grant is authored.

- [ ] **Step 3: Author the Arid grants**

On the `BiomeSkillArid` entry, `SustenanceModifierGrants` with two items: `Resource = SustenanceResource.Hunt` and `Resource = SustenanceResource.Forage`, both `BiomeTypeId = BiomeType.Arid`, both `PercentValue = 20`. A short comment that `BiomeType.Arid` is not `BiomeType.Desert`, which has its own Biome Skill technology. No other Technology gains a grant.

- [ ] **Step 4: Run it to verify it passes**

Run: `dotnet test AscendantEmpire.Tests --filter FullyQualifiedName~BiomeSkillAridGrantTests`
Expected: PASS.

- [ ] **Step 5: Correct the documents**

Each states something this work makes false. Describe the current state only — no "was" or "since" notes, and no column wrapping.

- `TechnologyGrant.cs` — the paragraph claiming a modifier-granting Technology writes the row directly and that which modifier it grants "is authored in C#, not as catalogue data" becomes a pointer to `TechnologySustenanceModifierGrant`.
- `docs/design/campaign-map/tiles.md` — "Nothing writes modifiers yet".
- `docs/design/technology.md` — the catalogue table list gains a row; the open question about which technologies grant what loses its modifier half.
- `docs/design/campaign-map/food-yield.md` — in "What differs per Player", the Technology lever and the `PlayerSustenanceModifier` stage move to built; also the `SourceId` table row and the "Sources of modifiers" bullet under "Not yet implemented".

- [ ] **Step 6: Run the technology and biome suites to verify nothing regressed**

Run: `dotnet test AscendantEmpire.Tests --filter "FullyQualifiedName~AscendantEmpire.Tests.Features.Technologies|FullyQualifiedName~AscendantEmpire.Tests.Features.Maps.Biomes"`
Expected: PASS.

- [ ] **Step 7: Hand Ben the commit message**

Full backend suite, report failures, then the `T-12:` message inline as a fenced block if green. The body covers the seed-data change only — the document edits are not commit-message material.

---

## Notes for whoever verifies this in the running game

**The bonus will not raise a Settlement's harvest.** `SettlementHarvestCalculator.CalculateSustenanceTake` takes `MIN(available, regrowth, staffed share)` and regrowth is measured against the biome's base, so a positive modifier raises only `available`, which is never the smallest term. It shows up for a Force feeding on Arid ground and nowhere else, until regrowth on an owned tile uses the owner's effective value. That is designed in `replenishment.md` and implemented nowhere.

**The migration is not applied by this plan.** Ben applies it.

**A re-priced grant reaches a live game on that Player's next discovery**, since nothing reconciles at start of turn or after a seed. Closing that is its own task.
