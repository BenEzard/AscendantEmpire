# Repairing the Technology Tree

How to add, rewire or correct technologies in the catalogue. Follow this when the job is editing the
tree's **data** rather than the code around it.

The catalogue is
[`TechnologySeedData.cs`](../AscendantEmpire.Application/Features/DatabaseSeeding/SeedData/TechnologySeedData.cs).
It is hand-authored and is the source of truth — nothing generates it, so an edit goes in there
directly. The design behind the model is in [design/technology.md](design/technology.md).

---

## Before editing: read the shape off a neighbour

Every entry is an `UpsertTechnologyDto`. Copy the exact shape from the technology nearest to the one
being added rather than writing it from memory — the single-line prerequisite form and the multi-line
form with a `PrerequisiteKind` are both in use, and matching the surrounding style keeps the diff
readable.

```csharp
new UpsertTechnologyDto
{
    Code = "ForestFamiliarity",
    DisplayName = "Forest Familiarity",
    Era = TechnologyEra.Stone,
    DirectedRequired = 400,
    Prerequisites =
    [
        new UpsertTechnologyPrerequisiteDto
        {
            PrerequisiteTechnologyCode = "ClimateSkillModerate",
            PrerequisiteKind = TechnologyPrerequisiteKind.Optional
        },
        new UpsertTechnologyPrerequisiteDto
        {
            PrerequisiteTechnologyCode = "ClimateSkillSubtropical",
            PrerequisiteKind = TechnologyPrerequisiteKind.Optional
        }
    ]
},
```

A mandatory prerequisite is the default and is written on one line, with no `PrerequisiteKind`:

```csharp
new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "ForestFamiliarity" }
```

## The conventions a new entry has to match

| | |
|---|---|
| **`Code`** | Derived from the display name: ProperCase, non-alphanumerics stripped, **no era prefix**. `"Climate Skill - Moderate"` → `ClimateSkillModerate`. It is the match key for the upsert and for every prerequisite, so **renaming a code silently orphans whatever depended on it**. |
| **`DirectedRequired`** | `100 + depth × 100`, where depth is the longest path from a root. Tiers in use are 100 … 1100. Round numbers, not balance — but staying on the curve is what keeps the file legible. |
| **`Era`** | `TechnologyEra.Stone` throughout. Era has one consumer, the unimplemented `RandomisedEqual` generator, so there is nothing to work out here yet. |
| **`SortOrder`** | Left unset, derived from list position. Position therefore decides in-game ordering. |
| **`IconPath`** | Left unset until artwork exists. |
| **List position** | Ordered by cost tier, then alphabetically **by code** within the tier. Put a new entry where its code sorts in its tier. |
| **`ExperienceRequired` / `ExperienceTypeCodes`** | Only where a technology genuinely draws on a seeded `ExperienceType`. A requirement with no source can never fill, and the tree viewer flags it. |
| **Acquisition costs and grants** | Leave empty. No Asset spending is modelled, and almost no grant target exists yet; the intent is carried as `Description` text instead. |

## Prerequisite semantics

A technology becomes available once **all** its `Mandatory` prerequisites are discovered **and**, if it
has any `Optional` ones at all, at least one of those is too. The optional edges of one technology form
a **single** implicit "any one of these" group — there is no way to express two separate any-of groups,
so "either A or B, and either C or D" cannot be authored.

Six technologies have no prerequisites at all and are therefore available from the first Round:
Primitive Crafting 1, Hunting, Fire-Making, Intensive Gathering, Language and Natural Exploration.
Every other technology in the catalogue hangs off one of them.
`TechnologySeedDataTests.ShouldLeaveOnlyTheStartingTechnologiesWithoutPrerequisites` pins that set, so
giving a technology its first prerequisite — or taking its last one away — is a decision the suite
makes you make on purpose.

### Repricing a technology you have just moved

Whether to reprice a technology whose prerequisites you changed is a judgement call worth raising: it
moves deeper in the tree, so the depth curve says its cost should rise, but that is a balance decision
rather than a mechanical one. A technology deliberately left at its original cost keeps a comment
saying so, and that is the one case where the file's "prerequisite written before its dependent"
ordering does not hold.

## Validate the graph before building

Four things break a catalogue, and the upsert rejects the whole batch for any of them: a **duplicate
code**, a **self-prerequisite**, a **prerequisite naming a code that is not there**, and a **cycle**.
`TechnologySeedDataTests` checks all four over the authored payload with no database, so a broken edit
fails there naming the codes at fault rather than surfacing as a rejected batch's error list. It also
pins a **duplicate display name** and the **same prerequisite named twice** on one technology — both
rejected by the upsert as well — alongside the opening research choice and the experience type codes.

```bash
dotnet test AscendantEmpire.Tests/AscendantEmpire.Tests.csproj --filter "FullyQualifiedName~TechnologySeedDataTests" --nologo -v q
```

No test pins the catalogue's size, so adding or removing a technology needs no test updated to match.
`ShouldSeedEveryRowTheCatalogueAuthors` in `UpsertTechnologiesHandlerTests` counts the authored rows and
compares them with the stored ones, which catches an edge dropped on the way into the database without
caring how many there are.

What nothing checks mechanically is the **cost curve**. `DirectedRequired` is a balance number, so a
technology sitting off `100 + depth × 100` is a judgement call rather than a failure — see
[Repricing a technology you have just moved](#repricing-a-technology-you-have-just-moved), since an
added prerequisite is what usually moves one off it.

## Then build, and look at it

```bash
dotnet build AscendantEmpire.Application/AscendantEmpire.Application.csproj -v q --nologo
```

If the build fails with `MSB3027` / `MSB3021`, the output DLLs are locked by a running
`AscendantEmpire.Api` or by Visual Studio. **Stop and say which process holds the lock** — never build
to a scratch `-o` directory to get around it, and never kill the process.

Then, **look at the tree**: the Entity Setup Tool's Technology group opens the tree viewer, which
draws the catalogue with mandatory prerequisites solid and optional dotted, and reports unconnected
technologies, cycles, unresolvable prerequisites and dangling grants in its legend. It reads the
catalogue through `GET /technology`, so the database has to have been seeded with the edit first — the
seed is an idempotent upsert matched on `Code`, so re-running it renames or rewires a row rather than
duplicating it, and never removes one added by hand.

One consequence of that worth knowing: a technology's four child collections — prerequisites, costs,
grants and experience sources — are **replaced wholesale** by the upsert, not merged. Omitting a
collection clears it.

## Clearing the catalogue for a full reseed

Because the seed never deletes, a technology **removed** from `TechnologySeedData.cs` stays in the
database, still drawn by the tree viewer, until it is deleted by hand. Re-running the seed will not take
it out. The same goes for a code that was renamed: the old row remains alongside the new one.

Every foreign key into `technology.Technology` is `NoAction`, so the catalogue cannot be deleted while
anything references it and the order below matters. Run it against the database you are reseeding:

```sql
BEGIN TRANSACTION;

-- Per-player references into the catalogue. These are what block the delete, and clearing them throws
-- away every Player's discovered technologies and research progress - see the warning below.
UPDATE technology.PlayerResearchState SET CurrentTechnologyId = NULL, NextTechnologyId = NULL;
DELETE FROM technology.PlayerTechnology;

-- The catalogue's own child tables, innermost first.
DELETE FROM technology.TechnologyExperienceType;
DELETE FROM technology.TechnologyGrant;
DELETE FROM technology.TechnologyPrerequisite;
DELETE FROM technology.TechnologyAcquisitionCost;

-- The catalogue itself, last: TechnologyPrerequisite points at it twice, once per end of each edge.
DELETE FROM technology.Technology;

COMMIT;
```

Then reseed through the Entity Setup Tool's Database → Seed, which upserts the catalogue from scratch.
Identity values are not worth reseeding: `Code` is the match key for every row and every prerequisite,
and nothing authored refers to an id.

> **This is a development reset, not a maintenance step.** It destroys the research state of every
> Player in the database, in every game, not just the one you are looking at. On a database holding a
> game you care about, delete the individual rows you actually want gone instead.

A `TRUNCATE` will not work on any of these tables — they are all referenced by a foreign key — so the
deletes stand as written.

The `ExperienceType` catalogue in the same schema is untouched by the above and does not normally need
clearing, since it is upserted on `Code` just like the technologies are. If it does have to go, its own
references come first:

```sql
BEGIN TRANSACTION;

DELETE FROM technology.PendingExperienceAccrual;
DELETE FROM technology.PlayerUnusedExperience;
DELETE FROM technology.TechnologyExperienceType;
DELETE FROM technology.ExperienceType;

COMMIT;
```
