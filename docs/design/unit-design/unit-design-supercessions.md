# Unit Design Supersessions (Upgrade Path)

## How it works (non-technical)

As players advance through technology they unlock newer, better components and unit designs. When a
better version of something arrives — say an **advanced sword** that replaces a **basic sword** — the
game should help the player move forward without forcing their hand.

Supersession is the link that says *"this new thing replaces that old thing."* Once it exists, the
game can do two helpful things:

1. **Offer to retire the old part.** When the player unlocks the advanced sword, the game can offer to
   mark the basic sword as obsolete so it won't be used in any *new* units. This is only an offer — the
   player can decline. The basic sword might be cheaper to build or maintain, so keeping it around can
   be the smart choice.

2. **Suggest upgrades to existing designs.** Any unit design (template) that still uses the basic sword
   can be flagged with a recommendation: *"You could swap this for the advanced sword."* Again, it is a
   suggestion, never automatic. Existing designs keep working exactly as they are until the player
   chooses to change them.

Crucially, upgrade paths are not a simple straight line. A basic sword might branch into either a
**longsword** or a **rapier**, and several older swords might all be replaced by a single **steel
sword**. The system is built to handle these branches and merges, and to mark one option as the
*recommended* default when there are several.

Components are also loosely grouped by what they *are* (e.g. all "sword"-type parts) using a
classification label. This keeps the catalogue tidy and lets a design slot say "any sword fits here,"
separately from the upgrade relationships.

---

## Technical summary (at a glance)

- The missing data primitive is a **directed "X supersedes Y" link**. Everything else needed to *act*
  on an upgrade already exists.
- Add **`ComponentSupersession`** — a many-to-many join (predecessor → successor) between
  `UnitComponent`s, with an `IsRecommended` flag for the default upgrade among branches.
- Add **`TemplateSupersession`** — the same shape between `UnitTemplate`s (schema added now; population
  deferred, see Open Item).
- Add a **`TagType`** (`Classification` / `Buff` / `Perk`) to the existing `Tag` entity, so a
  classification tag like `"sword"` is distinct from buff/perk bonus tags while reusing the existing
  tag join table and the existing `HasTag` slot rule. No separate "Class" table.
- **Per-player obsolescence already exists** via `PlayerUnlockedComponent.ObsoleteOnTurn` and
  `PlayerUnlockedTemplate.ObsoleteOnTurn`. "Offer to obsolete" just sets that field — no schema change.
- **Which design uses which part already exists** via `UnitTemplateComponent`. Upgrade recommendations
  read from there.
- Component supersessions and `TagType` are imported through the existing
  `BulkUpsertUnitComponents` catalogue endpoint, following its "payload is authoritative, one
  transaction" pattern.
- **In scope:** entities, EF configuration, migration, and catalogue-import wiring (components).
  **Out of scope (follow-ups):** the recommendation engine, the obsolete-offer handler, build-path
  enforcement, and surfacing supersessions in read endpoints.
- Rejected alternative: a "Component Class + linear tier." A linear tier conflates *grouping* with
  *upgrade direction* and cannot express branches, merges, or cross-family upgrades.

---

## Technical detail

### Design decisions
- **Explicit directed supersession links**, not a Class+tier. Tier-within-a-class conflates *grouping*
  with *upgrade direction* and cannot express branching (basic sword → longsword *or* rapier), merges
  (bronze + iron sword → steel sword), or cross-family upgrades.
- **Branching (many-to-many) via a join table**, mirroring the existing `UnitComponentTag` style, with
  an `IsRecommended` flag to mark the default upgrade among several successors.
- **Grouping via Tags + a new `TagType`**, keeping a classification tag (`"sword"`) distinct from
  buff/perk bonus tags while reusing the existing join table and the existing `HasTag` slot rule.
- **Supersession is authored global catalogue data** (not per-player). Per-player *obsolescence* remains
  in the existing `PlayerUnlockedComponent` / `PlayerUnlockedTemplate` `ObsoleteOnTurn` fields.

### 1. New enum — `TagType`
`AscendantEmpire.Domain/Enums/UnitDesigns/TagType.cs`

```
public enum TagType
{
    Classification, // structural family / role, e.g. "sword" — usable in slot rules and UI grouping
    Buff,           // a beneficial combat/stat bonus
    Perk,           // a perk-style bonus
}
```
(Proposed initial values — trivially extendable. Confirm the buff/perk split matches intent.)

### 2. `Tag` entity gets a `TagType`
`AscendantEmpire.Domain/Entities/UnitDesigns/Tag.cs`
- Add `public required TagType TagType { get; set; }`.
- Update the class summary so it no longer says only "bonus/buff".

### 3. New entity — `ComponentSupersession` (component → component, branching)
`AscendantEmpire.Domain/Entities/UnitDesigns/ComponentSupersession.cs`

```
public class ComponentSupersession
{
    public int PredecessorComponentId { get; set; }   // the older component
    public int SuccessorComponentId { get; set; }     // the component that supersedes it
    public bool IsRecommended { get; set; }           // default upgrade among multiple successors

    public UnitComponent Predecessor { get; set; } = null!;
    public UnitComponent Successor { get; set; } = null!;
}
```
- Composite PK `(PredecessorComponentId, SuccessorComponentId)` — mirrors `UnitComponentTag`.
- Authored global catalogue data (not per-player).

On `UnitComponent` add two nav collections (read as: *what supersedes me* / *what I supersede*):
- `ICollection<ComponentSupersession> SupersededBy` — rows where this component is the **predecessor**.
- `ICollection<ComponentSupersession> Supersedes` — rows where this component is the **successor**.

### 4. New entity — `TemplateSupersession` (template → template, branching)
`AscendantEmpire.Domain/Entities/UnitDesigns/TemplateSupersession.cs`
- Same shape against `UnitTemplate` (`PredecessorTemplateId`, `SuccessorTemplateId`, `IsRecommended`).
- Add `SupersededBy` / `Supersedes` nav collections on `UnitTemplate`.
- Realistically links **global** templates (`Code` set); validated when a writer exists. Schema only
  this ticket (see Open Item).

### 5. EF configuration
`AscendantEmpire.Application/Data/AscendantEmpireContext.cs` — extend `ConfigureUnitDesignSchema`
(add `ConfigureComponentSupersession` / `ConfigureTemplateSupersession` sub-methods to match the
existing per-area style), and add DbSets in the `#region Unit Designs` block.
- Both join tables: `ToTable(..., "unitdesigns")`, composite `HasKey`, two `HasOne…WithMany…HasForeignKey`
  configs with **explicit constraint names** and `OnDelete(DeleteBehavior.NoAction)` (matches the
  codebase's NoAction convention; the two self-referencing FKs *must* be NoAction to avoid multiple
  cascade paths).
- Secondary index on the successor FK (e.g. `IX_ComponentSupersession_Successor`) for reverse lookups.
- `Tag.TagType`: `.HasConversion<int>().IsRequired()`.

### 6. Migration
`AscendantEmpire.Application/Migrations/` — one migration, e.g. `AddComponentAndTemplateSupersession`:
- Create `unitdesigns.ComponentSupersession` and `unitdesigns.TemplateSupersession`.
- Add `TagType` (int, NOT NULL) to `unitdesigns.Tag` with a **default of `Buff`** so existing rows
  (currently all buff/bonus tags) get a sensible value.

### 7. Bulk-upsert wiring (components only)
Contracts under `AscendantEmpire.BackendContracts/UnitDesigns/BulkUpsertUnitComponents/`:
- `TagDto.cs`: add `TagType TagType`.
- `BulkUpsertUnitComponentsDto.cs`: add a top-level list `ComponentSupersessions`
  (a new `ComponentSupersessionDto { string PredecessorCode; string SuccessorCode; bool IsRecommended; }`).
  Top-level (like the existing `Tags` list) handles branching/merges cleanly and keeps it off the
  per-component DTO.

`BulkUpsertUnitComponentsHandler.cs`:
- **TagType on tags**: set `TagType` when creating a tag (the block at lines 84–97). Also update the
  `TagType` of *existing* tags found in the payload (the handler currently leaves existing tags
  untouched — recommend updating it so re-import is authoritative). Validate `TagType` is a defined enum.
- **Supersessions**: after components/tags are reconciled, process `ComponentSupersessions`:
  - **Validate** (collected with the other errors, fail-before-write): every `PredecessorCode` /
    `SuccessorCode` resolves to a component in the payload *or* already in the DB; predecessor ≠
    successor; no duplicate `(predecessor, successor)` pairs; at most one `IsRecommended` per predecessor.
  - **Reconcile authoritatively per predecessor**: for each predecessor code mentioned in the list,
    replace that predecessor's outgoing edges with exactly the listed ones (add missing, update
    `IsRecommended`, delete surplus — delete explicitly because FKs are NoAction). Predecessors **not**
    mentioned are left untouched. This matches the handler's existing "payload is authoritative for
    what it mentions, one `SaveChanges`" philosophy (cf. `MergeTagJoins` / `MergeDomainModifiers`).
- Load existing edges for the mentioned predecessor ids alongside the existing bulk-load to avoid N+1.

### 8. Tests
`AscendantEmpire.Tests/Handlers/UnitComponents/BulkUpsertUnitComponentsHandlerTests.cs`:
- Edges created from valid pairs; `IsRecommended` honoured.
- Re-import replaces a predecessor's edges (surplus removed, new added, flag updated).
- Rejections: unknown code, self-loop, duplicate pair, >1 `IsRecommended` per predecessor — all
  return `Fail` and write nothing.
- `TagType` set on a newly created tag and updated on an existing one.

### Open item to confirm
- **`TemplateSupersession` write path**: there is no global-template import/upsert handler in the
  codebase today, so this ticket would add the table + EF config + migration but leave it unpopulated
  until a template-authoring path exists. Options: (a) include the schema now (matches the stated
  intent to supersede templates, cheap, lets read endpoints surface it later), or (b) drop it from this
  ticket and add it together with the template writer. This document assumes **(a)**.

### Out of scope (explicit follow-ups)
- The recommendation engine (find templates whose `UnitTemplateComponent`s reference a superseded
  component, propose the successor if it passes the slot's acceptance rules and is unlocked).
- The obsolete-offer handler (set `ObsoleteOnTurn` on accept).
- Build-path enforcement blocking obsolete components on *new* units.
- Surfacing supersession in read endpoints (`GetFullCatalogue`, `GetPlayerTemplates`).

### Verification
1. `dotnet build` the solution.
2. `dotnet ef migrations add AddComponentAndTemplateSupersession --project AscendantEmpire.Application`
   — review the generated `Up`/`Down` (two tables + `Tag.TagType` default).
3. `dotnet test` — run the extended `BulkUpsertUnitComponentsHandlerTests` (uses the existing test DB
   setup).
4. Optional manual: `POST /unitdesign/components/bulk` with a payload containing two components, a
   `Classification` tag, and a `ComponentSupersessions` pair; confirm 200/201 and that the
   `ComponentSupersession` row + tag `TagType` land in the DB.
