# CM-63: Geography Water / Traversable Flags Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make "is this Geography water?" and "can it be entered at all?" explicit columns on `map.CampaignGeography`, then use them so a MapResourceType declares itself water-based and the Habitats page offers only Biomes and Geographies of matching water-ness.

**Architecture:** Two bools join `CampaignGeography` and thread through the six hand-written contract layers that mirror it. `CampaignTileWater` and `CampaignTileImpassability` — the existing single-rule services — stop inferring from the `CampaignMapGeographyType` enum and the `MovementCost >= 999` sentinel and read the columns instead, which also pulls the two handlers that compare the sentinel inline onto the shared service. `MapResourceType` then gains `IsWaterResource`, and the Type Manager's Habitats page filters its selects by it.

**Tech Stack:** .NET 9 / EF Core (SQL Server), FluentValidation, NUnit; React + TypeScript + TanStack Query, Vitest.

**Spec:** This plan is the spec of record. Background and the current state of the rules are in [docs/design/campaign-map/map-resources.md](../../design/campaign-map/map-resources.md) and [docs/design/assets-data-model.md](../../design/assets-data-model.md); both are updated by Task 9.

**Planner task:** CM-63.

## Global Constraints

- Braces on `if`/`for` always, even single-line bodies, and on their own line.
- C#: private fields `_camelCase`, constants `UPPER_SNAKE_CASE`, private constants both (`_MAX_X`). Locals and parameters stay `camelCase`. Test projects included.
- LINQ in method syntax, not query syntax.
- Every test carries `[Test, Description("...")]` — one line, stating what it pins and why. Test names read `Should<Outcome>When<Condition>`.
- Lightest sufficient test tier: no database → EF InMemory → containerised SQL Server. See [docs/testing.md](../../testing.md).
- The tier-3 container pins port 14330, so runs collide with a Visual Studio run. Always use a targeted `--filter`; never run the full suite unprompted.
- Never `dotnet test -v q`, and never pipe a test run through `tail` — grep for failure markers instead.
- Scaffold migrations with `--project AscendantEmpire.Application --startup-project AscendantEmpire.Api`. Never pass `--no-build`. Never run `database update`.
- React keeps its own idiom: `camelCase` throughout, no underscore prefixes, `SCREAMING_SNAKE_CASE` for module-level constants.
- Mountainous keeps `MovementCost = 999`. `IsTraversable` becomes authoritative; the sentinel survives as seeded data and is locked to the flag by a validator rule.
- **Each task ends in its own commit**, subject prefixed `CM-63:`. Claude never runs git itself and never runs migration `database update`: the message is handed to Ben inline as a fenced block and he commits.

## Decisions Taken

Recorded here because the implementer cannot derive them:

1. **Both flags are `required`** on the entity and on both DTOs. This is deliberate churn: it turns every geography construction site into a compile error rather than silently defaulting `DeepWater.IsWater` to false.
2. **`IMPASSABLE_MOVEMENT_COST` stays**, demoted from "the rule" to "a validation bound". `GeographyIsImpassable` reads `IsTraversable`; the validator forbids the two disagreeing.
3. **Pre-bump map archives are refused** on import rather than backfilled.
4. **Both the Biome and the Geography select are filtered** on the Habitats page. `CampaignTileWater.IsWater` is `biomeIsWater || geographyIsWater`, so a land resource needs *both* axes non-water; filtering both lists is what delivers that.
5. **The "Any" Geography option stays** for both water and land resources. See Review Focus item 5 for what that leaves open.

## Review Focus

Failure modes the design implies that no task's happy path exercises. Each has its test assigned to the task that owns the code:

1. `GeographyManagerDialog` round-trips GET → PUT with the whole draft, so a flag missing from `Geography.ts` would blank water on every Save. → Task 6.
2. `UpsertCampaignMapGeographiesHandler` assigns the fields in **two** places (the missing-rows projection and the update loop). Missing the second blanks flags on update while inserts keep them. → Task 1.
3. A pre-bump archive imported into an **empty** database would otherwise create DeepWater as land. → Task 5.
4. Mountainous with `IsTraversable = true` and `MovementCost = 999`, or the reverse, is incoherent and must be rejected. → Task 2.
5. A habitat with a null (Any) Geography under a land Biome still covers a water Geography painted inside that Biome, because either axis being water makes the tile water. Not closed here — it belongs at instance placement. → Task 8 pins the filter's actual guarantee so the gap is documented rather than assumed shut.

---

### Task 1: The two columns, through every layer

**Files:**
- Modify: `AscendantEmpire.Domain/Entities/Maps/CampaignMaps/CampaignGeography.cs`
- Create: `AscendantEmpire.Application/Migrations/<timestamp>_CM-63_AddGeographyWaterTraversable.cs` (scaffolded)
- Modify: `AscendantEmpire.FrontendContracts/Geographies/GetCampaignMapGeographies/ICampaignMapGeography.cs`
- Modify: `AscendantEmpire.FrontendContracts/Geographies/IAddOrUpdateCampaignMapGeographyDto.cs`
- Modify: `AscendantEmpire.BackendContracts/Geographies/CampaignMapGeography.cs`
- Modify: `AscendantEmpire.BackendContracts/Geographies/AddCampaignMapGeography/AddOrUpdateCampaignMapGeographyDto.cs`
- Modify: `AscendantEmpire.Application/Features/Maps/Geographies/Classes/DTCampaignMapGeography.cs`
- Modify: `AscendantEmpire.Application/Features/Maps/Geographies/Handlers/GetCampaignMapGeographiesHandler.cs`
- Modify: `AscendantEmpire.Application/Features/Maps/Geographies/Handlers/AddCampaignMapGeographyHandler.cs`
- Modify: `AscendantEmpire.Application/Features/Maps/Geographies/Handlers/UpsertCampaignMapGeographiesHandler.cs`
- Modify: `AscendantEmpire.Application/Features/DatabaseSeeding/SeedData/GeographySeedData.cs`
- Modify: `AscendantEmpire.AscendantEmpireUI/src/types/map/Geography.ts`
- Modify: ~36 construction sites across 23 files in `AscendantEmpire.Tests` (the compiler lists them)
- Test: `AscendantEmpire.Tests/Handlers/Geographies/GetCampaignMapGeographiesTests.cs`, `AscendantEmpire.Tests/Handlers/Geographies/AddCampaignMapGeographyTests.cs`

**Interfaces:**
- Produces: `CampaignGeography.IsWater` and `CampaignGeography.IsTraversable`, both `public required bool`. The same two names, same casing, on `ICampaignMapGeography`, `IAddOrUpdateCampaignMapGeographyDto`, `CampaignMapGeography`, `AddOrUpdateCampaignMapGeographyDto` and `DTCampaignMapGeography`; `isWater` / `isTraversable` on the TypeScript `Geography`.
- Consumes: nothing.

**Seeded values** (`GeographySeedData`, all six rows):

| Geography | IsWater | IsTraversable |
| --- | --- | --- |
| Unknown | false | false |
| DeepWater | true | true |
| ShallowWater | true | true |
| Flat | false | true |
| Hilly | false | true |
| Mountainous | false | **false** |

`Unknown` is not paintable and stands for "no data", so it is not traversable. Water is traversable — a ship enters it; what water refuses is a *land* Force, which the domain strategies decide.

- [ ] **Step 1: Write the failing round-trip test**

In `GetCampaignMapGeographiesTests`, following the fixture's existing tier:

```csharp
[Test, Description("Pins that both terrain flags reach the client, since the Geography Manager round-trips this exact shape straight back into the PUT.")]
public async Task ShouldReturnTheWaterAndTraversableFlagsAsync()
```

Seed DeepWater (water, traversable) and Mountainous (not water, not traversable); assert both flags on both returned rows.

In `AddCampaignMapGeographyTests`, a second test covering Review Focus item 2 — the upsert's **update** path, not its insert path:

```csharp
[Test, Description("Pins that an upsert over an existing row carries the terrain flags, because the handler assigns them in a separate place from the insert and the two can drift.")]
public async Task ShouldKeepTheTerrainFlagsWhenUpdatingAnExistingGeographyAsync()
```

Seed a row with `IsWater = false, IsTraversable = true`, upsert the same geography type with both flipped, re-read, assert both changed.

- [ ] **Step 2: Run the tests to verify they fail**

```bash
dotnet test AscendantEmpire.Tests --filter "FullyQualifiedName~Geographies"
```

Expected: compile failure — the properties do not exist.

- [ ] **Step 3: Add the two properties to `CampaignGeography`**

`public required bool IsWater { get; set; }` and `public required bool IsTraversable { get; set; }`, each with a `<summary>` of one or two lines plus a `<remarks>` saying what reads it. Mirror the wording style of `CampaignBiome.IsWater` / `IsTraversable`, which mean the same thing on the other axis. `IsTraversable`'s remarks must say that it is authoritative and that `MovementCost` keeps its sentinel as data only.

- [ ] **Step 4: Thread the properties through the five contract/DTO layers and the three handlers**

The two names are fixed by the Interfaces block above. In `UpsertCampaignMapGeographiesHandler` there are **two** assignment sites — the `missingGeographies` projection and the `foreach` update loop. Both get both flags.

- [ ] **Step 5: Scaffold the migration**

```bash
dotnet ef migrations add CM-63_AddGeographyWaterTraversable --project AscendantEmpire.Application --startup-project AscendantEmpire.Api
```

Then hand-add to the generated `Up`, after the two `AddColumn` calls, an `UPDATE` backfilling existing rows to the table above (matched on `CampaignMapGeographyTypeId`), so a database that already holds geographies is correct without re-running the seed. Do not run `database update`.

- [ ] **Step 6: Update `GeographySeedData` with the table above, and the TypeScript `Geography` type**

`isWater: boolean;` and `isTraversable: boolean;` with the same trailing-comment style as the fields around them.

- [ ] **Step 7: Fix the test construction sites the compiler names**

Mechanical. Each site gets the values from the table above for the geography it is seeding. Where a fixture seeds a geography it never reasons about, `IsWater = false, IsTraversable = true` is the right default.

- [ ] **Step 8: Run the tests to verify they pass**

```bash
dotnet test AscendantEmpire.Tests --filter "FullyQualifiedName~Geographies"
```

Expected: PASS. Then confirm the solution builds: `dotnet build AscendantEmpire.sln`.

- [ ] **Step 9: Commit**

Targeted tests green, so this task is done. Hand Ben the message inline
(subject prefixed `CM-63:`, tight body, no `Co-Authored-By` trailer) and let him run it.

---

### Task 2: Validator rules

**Files:**
- Modify: `AscendantEmpire.Application/Features/Maps/Geographies/Validators/CampaignMapGeographyValidator.cs`
- Test: `AscendantEmpire.Tests/Handlers/Geographies/CampaignMapGeographyValidatorTests.cs` (create if absent)

**Interfaces:**
- Consumes: `AddOrUpdateCampaignMapGeographyDto.IsWater` / `.IsTraversable` from Task 1.
- Produces: nothing other tasks call.

Tier 1 — the validator touches no `DbContext`.

- [ ] **Step 1: Write the failing tests**

```csharp
[Test, Description("Pins that water geography cannot be elevated, the same rule tile painting already enforces, so the catalogue cannot hold a combination the map editor would refuse.")]
public void ShouldFailWhenAWaterGeographyIsElevated()

[TestCase(true, 999, Description = "Pins that a traversable Geography cannot carry the impassable cost, since the flag is authoritative and the sentinel survives only as data.")]
[TestCase(false, 1, Description = "Pins that an untraversable Geography cannot carry an ordinary cost, so the two cannot drift apart.")]
public void ShouldFailWhenTraversabilityContradictsTheMovementCost(bool isTraversable, decimal movementCost)

[Test, Description("Pins that a water geography at sea level passes, so the elevation rule rejects only what it is aimed at.")]
public void ShouldSucceedWhenAWaterGeographyIsAtSeaLevel()
```

- [ ] **Step 2: Run to verify they fail**

```bash
dotnet test AscendantEmpire.Tests --filter "FullyQualifiedName~CampaignMapGeographyValidator"
```

- [ ] **Step 3: Add the two rules, and fix the elevation message**

Two `RuleFor(g => g)` entries, phrased like the existing ones (`$"{nameof(...)} ..."`). The water rule mirrors the wording already in `UpdateCampaignMapTilesHandler.IsValidBiomeGeographyCombinationForCampaignMap`.

While in the file: the existing `RuleFor(g => g.RelativeElevation).GreaterThanOrEqualTo(-2)` carries the message `"Relative Elevation must be 0 or greater."`, which contradicts its own bound. Correct the message to state -2.

- [ ] **Step 4: Run to verify they pass**

- [ ] **Step 5: Commit**

Targeted tests green, so this task is done. Hand Ben the message inline
(subject prefixed `CM-63:`, tight body, no `Co-Authored-By` trailer) and let him run it.

---

### Task 3: `CampaignTileWater` reads the flag

**Files:**
- Modify: `AscendantEmpire.Application/Features/Maps/CampaignMaps/Services/CampaignTileWater.cs`
- Modify: `AscendantEmpire.Application/Features/ForceOrders/Movement/Strategies/MovementTargetTile.cs`
- Modify: `AscendantEmpire.Application/Features/ForceOrders/Handlers/StepForceHandler.cs` (the `MovementTargetTile` build at ~line 188, the origin-tile projection at ~line 621, and the `originIsWater` call at ~line 634)
- Modify: `AscendantEmpire.Application/Features/Maps/CampaignMaps/Classes/SightTerrain.cs`, `.../Services/SightDataReader.cs`
- Modify: `AscendantEmpire.Application/Features/Maps/Biomes/Handlers/GetTileSustenanceHandler.cs`
- Modify: `AscendantEmpire.Application/Features/Maps/Depletion/Services/TileSupportEvaluator.cs`
- Modify: `AscendantEmpire.Application/Features/Turns/Services/TurnPipeline/Campaign/EndRound/RegenerateTileSustenance.cs`
- Modify: `AscendantEmpire.Application/Features/Turns/Services/TurnPipeline/Campaign/EndTurn/Gathering.cs`
- Test: `AscendantEmpire.Tests/Features/Maps/CampaignMaps/CampaignTileWaterTests.cs`

**Interfaces:**
- Consumes: `CampaignGeography.IsWater` from Task 1.
- Produces: `CampaignTileWater.IsWater(bool biomeIsWater, bool geographyIsWater)` — replaces the `CampaignMapGeographyType` parameter. `MovementTargetTile` gains `public required bool GeographyIsWater { get; init; }`; `SightTerrain` gains the same property name.

Each EF projection that currently selects `t.CampaignMapGeographyTypeId` for this purpose adds `GeographyIsWater = t.Geography.IsWater` beside its existing `BiomeIsWater = t.Biome.IsWater`. `MovementTargetTile.GeographyType` is **kept** — it is used for more than water.

- [ ] **Step 1: Rewrite `CampaignTileWaterTests` for the new signature**

Replace the enum-driven `[TestCase]`s with the four combinations of the two bools, keeping the existing description style and the rule it pins ("either being water is enough").

- [ ] **Step 2: Run to verify it fails**

```bash
dotnet test AscendantEmpire.Tests --filter "FullyQualifiedName~CampaignTileWater"
```

- [ ] **Step 3: Change the signature and update the six call sites**

`CampaignTileWater.IsWater`'s `<remarks>` must stop describing the geography side as an enum match and say both axes are now flags the author sets.

- [ ] **Step 4: Run the movement, sustenance and gathering suites**

```bash
dotnet test AscendantEmpire.Tests --filter "FullyQualifiedName~Movement|FullyQualifiedName~Sustenance|FullyQualifiedName~Gathering|FullyQualifiedName~StepForce"
```

Expected: PASS. Fixtures that seed water tiles will need `IsWater = true` on their geography rows — the compiler catches the construction, this run catches the value.

- [ ] **Step 5: Commit**

Targeted tests green, so this task is done. Hand Ben the message inline
(subject prefixed `CM-63:`, tight body, no `Co-Authored-By` trailer) and let him run it.

---

### Task 4: `CampaignTileImpassability` reads `IsTraversable`

**Files:**
- Modify: `AscendantEmpire.Application/Features/Maps/CampaignMaps/Services/CampaignTileImpassability.cs`
- Modify: `AscendantEmpire.Application/Features/ForceOrders/Movement/Strategies/MovementTargetTile.cs`
- Modify: `AscendantEmpire.Application/Features/Maps/CampaignMaps/Classes/TerritoryTile.cs`, `.../Classes/TerritoryTileColumns.cs`, `.../Services/CampaignTerritoryReader.cs`
- Modify: `AscendantEmpire.Application/Features/Maps/CampaignMaps/Services/SightDataReader.cs:210`
- Modify: `AscendantEmpire.Application/Features/Maps/MapGallery/Handlers/PutCampaignMapStartPositionsHandler.cs:156`
- Modify: `AscendantEmpire.Application/Features/Maps/MapGallery/Handlers/UpdateCampaignMapTilesHandler.cs:536`
- Modify: `AscendantEmpire.Application/Features/ForceOrders/ContextActions/Strategies/SettlementFoundingRules.cs:44` (comment only)
- Test: `AscendantEmpire.Tests/Features/Maps/CampaignMaps/CampaignTileImpassabilityTests.cs`

**Interfaces:**
- Consumes: `CampaignGeography.IsTraversable` from Task 1.
- Produces: `CampaignTileImpassability.IsImpassable(bool biomeIsTraversable, bool geographyIsTraversable)` and `CampaignTileImpassability.GeographyIsImpassable(bool geographyIsTraversable)`. `MovementTargetTile` and `TerritoryTile`/`TerritoryTileColumns` each gain `public required bool GeographyIsTraversable { get; init; }`; `GeographyMovementCost` is **kept**, since `MovementTargetTile.MovementCost` still needs it.

`PathFinder` is untouched: it already models impassability as an explicit `PathTile.IsImpassable` bool and skips impassable neighbours regardless of cost, and it has no production caller.

- [ ] **Step 1: Rewrite `CampaignTileImpassabilityTests` for the new signature**

Four bool combinations for `IsImpassable`, two for `GeographyIsImpassable`, keeping the existing fixture's descriptions. The property-agreement test at the bottom of the file (asserting `MovementTargetTile` delegates to the service) keeps its shape with the new parameters.

- [ ] **Step 2: Run to verify it fails**

```bash
dotnet test AscendantEmpire.Tests --filter "FullyQualifiedName~CampaignTileImpassability"
```

- [ ] **Step 3: Change both service methods to read the flag**

Delete the `>= IMPASSABLE_MOVEMENT_COST` comparison from `GeographyIsImpassable` and the `<remarks>` paragraph explaining the sentinel; replace it with one saying the flag is authoritative and the constant is now only the validator's bound.

- [ ] **Step 4: Move the two inline sentinel sites onto the service**

`PutCampaignMapStartPositionsHandler` and `UpdateCampaignMapTilesHandler` currently compare `MovementCost >= CampaignGeography.IMPASSABLE_MOVEMENT_COST` themselves. Both call `CampaignTileImpassability.GeographyIsImpassable` instead. `PutCampaignMapStartPositionsHandler`'s projection record gains `GeographyIsTraversable`; its failure message currently interpolates the movement cost — reword it to name the geography, since the cost is no longer the reason.

- [ ] **Step 5: Turn `SightDataReader:210` into a boolean predicate**

`.Where(geography => geography.IsTraversable)` replaces the decimal comparison. The surrounding comment ("Only Geographies a Force can enter count") already describes the flag and needs no change.

- [ ] **Step 6: Run the movement, territory, sight and start-position suites**

```bash
dotnet test AscendantEmpire.Tests --filter "FullyQualifiedName~Impassab|FullyQualifiedName~Territory|FullyQualifiedName~Sight|FullyQualifiedName~StartPosition|FullyQualifiedName~Movement"
```

- [ ] **Step 7: Commit**

Targeted tests green, so this task is done. Hand Ben the message inline
(subject prefixed `CM-63:`, tight body, no `Co-Authored-By` trailer) and let him run it.

---

### Task 5: Map archive version bump and refusal

**Files:**
- Modify: `AscendantEmpire.Application/Features/Maps/MapGallery/Classes/Export/MapExportGeography.cs`
- Modify: `AscendantEmpire.Application/Features/Maps/MapGallery/Classes/Export/MapExportDocument.cs` (`FormatVersion` `"1.1"` → `"1.2"`)
- Modify: `AscendantEmpire.Application/Features/Maps/MapGallery/Executors/MapExportExecutor.cs:170`
- Modify: `AscendantEmpire.Application/Features/Maps/MapGallery/Executors/MapImportExecutor.cs:226` and the document-level version check
- Test: `AscendantEmpire.Tests/Features/MapGallery/MapImportExecutorTests.cs`, `.../MapExportExecutorTests.cs`

**Interfaces:**
- Consumes: `CampaignGeography.IsWater` / `.IsTraversable` from Task 1.
- Produces: `MapExportGeography.IsWater` / `.IsTraversable`.

- [ ] **Step 1: Write the failing tests**

```csharp
[Test, Description("Pins that an archive written before the terrain flags existed is refused, because importing it into an empty database would create DeepWater as land with no error.")]
public async Task ShouldFailTheImportWhenTheArchivePredatesTheTerrainFlagsAsync()

[Test, Description("Pins that the terrain flags survive an export and re-import, so a map moved between databases keeps the geography it was authored against.")]
public async Task ShouldRoundTripTheTerrainFlagsAsync()
```

- [ ] **Step 2: Run to verify they fail**

```bash
dotnet test AscendantEmpire.Tests --filter "FullyQualifiedName~MapImportExecutor|FullyQualifiedName~MapExportExecutor"
```

- [ ] **Step 3: Add the two fields to `MapExportGeography`, populate them in the export, and bump `FormatVersion` to `"1.2"`**

- [ ] **Step 4: Refuse pre-1.2 archives in `MapImportExecutor`**

Compare the document's `FormatVersion` before any row is touched and fail with a message naming the version found and telling the author to re-export. `ImportMissingGeographiesAsync` then sets both flags from the document unconditionally.

- [ ] **Step 5: Run to verify they pass**

- [ ] **Step 6: Commit**

Targeted tests green, so this task is done. Hand Ben the message inline
(subject prefixed `CM-63:`, tight body, no `Co-Authored-By` trailer) and let him run it.

---

### Task 6: Geography Manager and Geography List

**Files:**
- Modify: `AscendantEmpire.AscendantEmpireUI/src/features/entitysetuptool/components/GeographyManagerDialog.tsx`
- Modify: `AscendantEmpire.AscendantEmpireUI/src/features/mapgallery/components/GeographyListDialog.tsx`
- Test: `AscendantEmpire.AscendantEmpireUI/src/features/entitysetuptool/components/__tests__/GeographyManagerDialog.test.tsx` (create if absent)

**Interfaces:**
- Consumes: `Geography.isWater` / `.isTraversable` from Task 1.
- Produces: nothing other tasks call.

`BiomeManagerDialog` already carries this exact pair of checkboxes with downstream fields disabled off `isWater` — follow it rather than inventing a pattern.

- [ ] **Step 1: Write the failing tests**

```tsx
it("keeps both terrain flags in the payload when saving", ...)
```

Covers Review Focus item 1: render the dialog over a geography with `isWater: true, isTraversable: false`, change an unrelated field (the name), Save, and assert the mutation received both flags unchanged. This is the test that catches a field missing from `Geography.ts`.

```tsx
it("sends the flipped flag when a terrain checkbox is toggled", ...)
```

- [ ] **Step 2: Run to verify they fail**

```bash
npx vitest run src/features/entitysetuptool/components/__tests__/GeographyManagerDialog.test.tsx
```

- [ ] **Step 3: Add the two checkboxes to the General Properties section**

Beside the existing "Is Paintable on Campaign Map?" checkbox, same markup, labelled "Is Water?" and "Is Traversable?".

- [ ] **Step 4: Add the two columns to `GeographyListDialog`**

The table already shows `movementCost`; add water and traversable so the read-only list in the map gallery shows what now governs behaviour. Match the existing cell classes.

- [ ] **Step 5: Run to verify they pass, and typecheck**

```bash
npx vitest run src/features/entitysetuptool
```

```bash
npx tsc --noEmit
```

- [ ] **Step 6: Commit**

Targeted tests green, so this task is done. Hand Ben the message inline
(subject prefixed `CM-63:`, tight body, no `Co-Authored-By` trailer) and let him run it.

---

### Task 7: `MapResourceType.IsWaterResource`

**Files:**
- Modify: `AscendantEmpire.Domain/Entities/Maps/MapResources/MapResourceType.cs`
- Create: `AscendantEmpire.Application/Migrations/<timestamp>_CM-63_AddMapResourceTypeIsWaterResource.cs` (scaffolded)
- Modify: `AscendantEmpire.BackendContracts/MapResources/AddOrUpdateMapResourceType/AddOrUpdateMapResourceTypeDto.cs`
- Modify: `AscendantEmpire.FrontendContracts/MapResources/AddOrUpdateMapResourceType/IAddOrUpdateMapResourceTypeDto.cs`
- Modify: `AscendantEmpire.BackendContracts/MapResources/GetMapResourceCatalogue/*` (the `MapResourceTypeDetail` record)
- Modify: `AscendantEmpire.Application/Features/Maps/MapResources/Services/MapResourceTypeMapper.cs` (both directions: `ToDetail` ~line 38, the write ~line 124)
- Modify: `AscendantEmpire.Application/Features/DatabaseSeeding/SeedData/TimberSeedData.cs`, `.../BerriesSeedData.cs`
- Modify: `AscendantEmpire.AscendantEmpireUI/src/types/map/MapResource.ts`
- Modify: `AscendantEmpire.AscendantEmpireUI/src/features/mapgallery/tools/mapResourceDraft.ts` (`createMapResourceType`)
- Modify: `AscendantEmpire.AscendantEmpireUI/src/features/mapgallery/components/mapResources/MapResourceGeneralSection.tsx`
- Test: `AscendantEmpire.Tests/Features/Maps/MapResources/PutMapResourceCatalogueHandlerTests.cs` (or the fixture that already covers the catalogue round-trip)

**Interfaces:**
- Consumes: nothing from earlier tasks.
- Produces: `MapResourceType.IsWaterResource` (`public bool`, **not** `required` — it joins `IsRenewable` / `IsStratified` / `IsDistribution`, which are plain bools) and `required bool IsWaterResource` on the add/update DTO, matching its siblings there. TypeScript: `isWaterResource: boolean` on `MapResourceType`.

Both seeded resources (Timber, Berries) set `IsWaterResource = false` explicitly, as they do their other kind flags.

- [ ] **Step 1: Write the failing round-trip test**

```csharp
[Test, Description("Pins that the water-resource flag survives a catalogue save and reload, since it is what the Habitats page filters its Biome and Geography lists by.")]
public async Task ShouldRoundTripTheWaterResourceFlagAsync()
```

- [ ] **Step 2: Run to verify it fails**

```bash
dotnet test AscendantEmpire.Tests --filter "FullyQualifiedName~MapResourceCatalogue|FullyQualifiedName~PutMapResourceCatalogue"
```

- [ ] **Step 3: Add the property, the DTO fields, and both mapper directions**

The entity property's `<summary>` says it decides where the type can live — water tiles or land — and its `<remarks>` points at `MapResourceHabitat`, since that is what it constrains. It is **not** one of the three kind flags and must not be described as one; put it in the `Extraction` region or its own, not `Kind`.

- [ ] **Step 4: Scaffold the migration**

```bash
dotnet ef migrations add CM-63_AddMapResourceTypeIsWaterResource --project AscendantEmpire.Application --startup-project AscendantEmpire.Api
```

A plain `AddColumn<bool>` defaulting to false is correct here: every existing type is land.

- [ ] **Step 5: Add the checkbox and the TypeScript field**

`MapResourceGeneralSection` already imports `CheckboxRow`. Add one labelled "Water Resource?" with a hint saying it restricts the Habitats page to water Biomes and Geographies. `createMapResourceType` defaults it to `false`.

- [ ] **Step 6: Run to verify it passes, and typecheck**

- [ ] **Step 7: Commit**

Targeted tests green, so this task is done. Hand Ben the message inline
(subject prefixed `CM-63:`, tight body, no `Co-Authored-By` trailer) and let him run it.

---

### Task 8: Filter the Habitats page by water-ness

**Files:**
- Modify: `AscendantEmpire.AscendantEmpireUI/src/features/mapgallery/components/mapResources/MapResourceHabitatsSection.tsx`
- Modify: `AscendantEmpire.Application/Features/Maps/MapResources/Handlers/PutMapResourceCatalogueHandler.cs`
- Test: `AscendantEmpire.AscendantEmpireUI/src/features/mapgallery/components/mapResources/__tests__/MapResourceHabitatsSection.test.tsx` (create if absent)
- Test: `AscendantEmpire.Tests/Features/Maps/MapResources/PutMapResourceCatalogueHandlerTests.cs`

**Interfaces:**
- Consumes: `Geography.isWater` (Task 1), `Biome.isWater` (already present), `MapResourceType.isWaterResource` (Task 7).
- Produces: nothing other tasks call.

The section already filters both lists by `isPaintableOnCampaignMap`; water-ness is a second predicate on the same two `filter` calls. `nextBiome` — the Biome a new row starts on — must be picked from the *filtered* list, or adding a habitat to a water resource would start it on a land Biome.

The server rule can only police habitats that **name** a Geography. A null (Any) Geography is still allowed for both water and land resources, which is Review Focus item 5.

- [ ] **Step 1: Write the failing frontend tests**

```tsx
it("offers only water biomes and water geographies for a water resource", ...)
it("offers only land biomes and land geographies for a land resource", ...)
it("still offers Any as a geography for a water resource", ...)
it("starts a new habitat row on a biome the filtered list offers", ...)
```

The last one pins the `nextBiome` trap.

- [ ] **Step 2: Write the failing server-side test**

Which Geographies are water is database state, so this rule belongs with the handler's existing asset-and-category existence checks in `PutMapResourceCatalogueHandler`, **not** in the DbContext-free `MapResourceTypeValidator`. The validator's class-level `<remarks>` already draws that line ("Whether the Assets and category named exist is the handler's check, since it needs the database") — extend it to name this rule too. The tests therefore go in `PutMapResourceCatalogueHandlerTests`:

```csharp
[TestCase(true, false, Description = "Pins that a water resource cannot name a land Geography, so the API enforces what the Habitats page offers rather than leaving the rule in the UI.")]
[TestCase(false, true, Description = "Pins that a land resource cannot name a water Geography, the same rule in the other direction.")]
public async Task ShouldFailWhenAHabitatNamesAGeographyOfTheWrongWaternessAsync(bool isWaterResource, bool geographyIsWater)

[Test, Description("Pins that a habitat with no Geography is accepted for either kind of resource, since Any is a legitimate habitat and the water-ness of the tile then comes from the Biome.")]
public async Task ShouldSucceedWhenAHabitatNamesNoGeographyAsync()
```

- [ ] **Step 3: Run both to verify they fail**

```bash
npx vitest run src/features/mapgallery/components/mapResources/__tests__/MapResourceHabitatsSection.test.tsx
```

```bash
dotnet test AscendantEmpire.Tests --filter "FullyQualifiedName~PutMapResourceCatalogue"
```

- [ ] **Step 4: Add the water-ness predicate to both selects and to `nextBiome`**

- [ ] **Step 5: Add the server-side check to `PutMapResourceCatalogueHandler`**

Beside the existing asset resolution, load the named Geographies' `IsWater` and add one error per habitat whose Geography disagrees with its type's `IsWaterResource`, phrased like the existing `$"There is no Asset {missing}."` errors.

- [ ] **Step 6: Run both to verify they pass**

- [ ] **Step 7: Commit**

Targeted tests green, so this task is done. Hand Ben the message inline
(subject prefixed `CM-63:`, tight body, no `Co-Authored-By` trailer) and let him run it.

---

### Task 9: Documentation

**Files:**
- Modify: `docs/design/campaign-map/map-resources.md`
- Modify: `docs/design/campaign-map/tiles.md` (if it describes impassability or water)

**Interfaces:**
- Consumes: everything above.
- Produces: nothing.

Docs state how things are **now** — no "was", no "since removed", no before/after tables.

- [ ] **Step 1: Document the water-resource rule in `map-resources.md`**

Under the MapResourceType flag list: a type declares `IsWaterResource`, and its habitats are restricted to Biomes and Geographies of matching water-ness. State plainly that a habitat with no Geography is still allowed, and that the water-ness of such a tile then comes from the Biome.

- [ ] **Step 2: Correct any passage that describes impassability as a movement-cost sentinel**

Grep the `docs/` tree for `999` and for "impassable" and bring each hit into line with `IsTraversable` being authoritative.

- [ ] **Step 3: Commit**

Documentation only, so there is nothing to run. Hand Ben the message inline
(subject prefixed `CM-63:`, tight body, no `Co-Authored-By` trailer) and let him run it.

---

## Execution Notes

- Tasks 1 → 2/3/4/5/6 and 7 → 8 are the only hard orderings. Task 7 does not depend on Tasks 1–6 and could run alongside them, but it touches `MapResource.ts`, which Task 8 also edits.
- Task 1 is the one with real blast radius (the ~36 test sites). Review it before anything builds on it.
- Each task ends in its own commit, so a task that has to be redone is a revert rather than an unpick. Ben makes every commit; the message is handed to him inline.
- Do not run either full suite until the work is done and a commit message is asked for.
