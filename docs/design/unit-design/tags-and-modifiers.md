# Tags and Modifiers

This document defines the concepts behind unit-design Tags and Modifiers. It resolves an earlier
"working model" discussion (kept below) into five named concepts, decisions on the original open
questions, and the work that remains.

## Current references in the code

## Tags — a global, reusable catalogue

**Backend (C#/.NET):**

- `Tag.cs` — entity: `Id`, `Code` (unique, ≤50 chars), `DisplayName` (≤100), `Description` (≤200), `TagType` enum. A global, reusable tag attached to components — either a structural classification or a bonus.
- `UnitComponentTag.cs` — many-to-many join between `Tag` and `UnitComponent`.
- `TagType.cs` — enum: `Classification`, `Combat`, `Movement`, `Ability`.
- EF Core: `DbSet<Tag> Tags`, `DbSet<UnitComponentTag> UnitComponentTags`.
- `UnitDesignTagsController.cs` — dev-only endpoints `GET/POST /unit-design-tags`, `PUT /unit-design-tags/{id}`, backed by `GetTagsHandler`, `CreateTagHandler`, `UpdateTagHandler`, and shape validation in `TagValidation.cs`. The tag-type catalogue sits under the same controller as a sub-resource: `GET/PUT /unit-design-tags/types`, backed by `GetTagTypesHandler` and `UpsertTagTypesHandler`. The `:int` constraint on `{id}` is what keeps it distinct from the literal `types` segment.
- Contracts: `UpsertTagDto`, `TagView`, `CatalogueTag`.

**Frontend**, all under `AscendantEmpire.AscendantEmpireUI/src/features/entitysetuptool/`:

- `types/Tag.ts` — `TagType` union, `Tag` interface, `createEmptyTag()`, `TAG_TYPES`.
- `types/UnitComponent.ts` — `UnitComponent.tagCodes: string[]` (components reference tags by code only); `SlotRuleType` includes `"HasTag"` so slot-acceptance rules can filter by tag.
- `TagSetupDialog.tsx` — authoring dialog for a single global tag.
- `components/sections/TagsSection.tsx` — tab in `ComponentSetupDialog` for attaching/removing tag codes as chips (references only, no definition editing).
- `EntitySetupTool.tsx` — top-level dev tool with an Add/Search toolbar group for Tags.
- `api/tagsApi.ts` — `getTags/createTag/updateTag` hitting `/unit-design-tags`.
- `api/componentsApi.ts` — sends `tagCodes: string[]` on bulk component upsert; backend resolves codes to `Tag` rows.

### Modifiers — narrower, not a unified type

There's no single `Modifier` entity — it splits into two unrelated mechanisms:

1. **Weapon domain damage modifiers** (most developed):
   - Backend: `UnitComponentWeaponDomainModifier.cs` — per-weapon, per-`DomainType` fixed-point `DamageMultiplier`; also gates which domains a weapon can target.
   - Frontend: `WeaponDomainModifiers = Partial<Record<DomainType, number>>` (in both `entitysetuptool` and `unitdesign` feature type files); sparse key/value table UI in Weapon properties, with `domainModifiersToRows()`/`rowsToDomainModifiers()` helpers in `componentsApi.ts`.
2. **Movement modifiers** — see [unit-movement.md](../units/unit-movement.md#movement-modifiers).

## Concepts

The original discussion (kept below) was treating classification, passive bonuses, capability unlocks, combat triggers, and force-level bonuses as one problem. They don't share a shape, so they're split into five concepts. Only the first two exist today.

### 1. Tags — classification only (implemented)

- Flat, global, string-keyed catalogue (`Tag`/`TagType`). No hierarchy — combine meaning via multiple tags on a component (e.g. a camel base carries both a "cavalry" tag and a "desert" tag) rather than a tag tree.
- Tags carry no data beyond their own catalogue fields (`Code`, `DisplayName`, `Description`, `TagType`) — they are keys, not behavior.
- Consumed today for classification and for slot-acceptance rules (`SlotRuleType.HasTag`).

### 2. Structured Modifiers — numeric, single-unit scope (implemented, partial)

- Weapon domain damage modifiers and movement modifiers (see above) are the two existing instances.
- Modifiers always carry data (a `Value`, optional `DomainType`/`Conditions`) — this is the dividing line from Tags, which never do.
- Values support both flat and percentage amounts via an explicit `ValueType` (Flat/Percent) enum next to `Value`, rather than a bare, ambiguous number.
- Movement modifiers are not yet applied to movement — see [unit-movement.md](../units/unit-movement.md#movement-modifiers). The existing `Effect`/`Conditions` JSON shape looks expressive enough for every `MoveModifier` case.

### 3. Capability Grants — tag unlocks an action (first use landed in UD-56)

Examples from the original discussion:

- ~~`camp-goods` (equipment) enables building a camp, if contextual conditions are met.~~ **Delivered** in
  UD-56, as two `TagType.Ability` tags rather than one: `found-settlement` and `found-camp`, both authored
  onto the `camping-materials` equipment component. See below.
- ~~`self-organised` (training) enables a unit to split without a Leader, if conditions are
  met.~~ **Superseded** by the Unled leadership model on MilitaryOrganisation tags — splitting
  without a leader is now intrinsic to unled structures, not a training grant. See
  [military-organisation.md](../units/military-organisation.md#leadership-model).
- `stealth-observation` (training, sniper) enables a toggleable special movement mode.

These aren't modifiers — they don't change a number, they unlock a player-facing action. Model as a lookup (`TagCode → CapabilityCode`) checked by game/UI logic when the action is attempted, rather than folding this into the modifier shape.

**How the first one actually works (UD-56).** No separate lookup table was needed: `CompileUnitTemplateHandler`
already unions every tag carried by a template's components into `CompiledUnitTemplateCapability` rows keyed by
`TagCode`, so the capability *is* the tag code. `ForceActionContextBuilder` flattens those codes onto the Force,
and each context-action strategy asks for the one it needs — `found-settlement` for Create Village,
`found-camp` for Establish Camp. A Force whose template grants nothing matching is not offered the action at all.
See [force-context-action-toolbar.md](../units/force-context-action-toolbar.md#the-settlement-strategies--create-village-and-establish-camp-ud-56).

Two consequences worth carrying into the next capability:

- **One ability per action, not one per component.** Splitting camping into `found-settlement` and `found-camp`
  is what lets a future component grant camping without granting settlement. Naming a tag after the component
  that happens to carry it today would have thrown that away.
- **These tags are catalogue data, not seed data.** They are authored through the Entity Setup Tool and are
  absent from `Features/DatabaseSeeding/SeedData/`, so on a database where nobody has authored them the gated
  action simply never appears. Correct, but the first place to look when a capability seems not to work.

### 4. Combat Trigger Effects — deferred

Examples: `hit-and-run` (move after attacking, if movement points remain), `porcupine-defence` (double defence damage vs. a melee cavalry attacker), `zone-of-control` (long bows attack an incoming attacker), `penetrate-and-explode` (chance of splash damage to nearby units on death).

These need attacker/defender-relative conditions and secondary/triggered effects (e.g. on-death), which is a combat-engine concern, not a tags-and-modifiers concern. They also depend on combat-resolution mechanics that are themselves still unresolved (see
[open-questions.md](../open-questions.md#combat-domains-and-unit-capabilities)). Hold off designing this until combat resolution is settled, otherwise this doc keeps growing open questions instead of resolving them.

### 5. Force/Specialist-Scoped Bonuses — separate scope, deferred

Examples: a blacksmith reducing weapon damage by 5%, a master forager increasing food foraging, an experienced scout reducing ambush chance, an accountant reducing upkeep by 5%, a medic with a range, a drill sergeant imputing a discipline bonus.

These apply to a Force, not a Unit Component — a different scope entity, and belong conceptually with [specialists.md](../specialists.md) rather than here. Not building now, but the
Structured Modifier shape (concept 2) should gain an optional `ApplyTo` field (self / attacker / allies / enemies) now, cheaply, so it can be reused here later without a rewrite.

## Decisions

1. Tags and Structured Modifiers alone aren't enough of a division — five concepts as above, though 3–5 stay unbuilt for now.
2. Tags are string keys with no attached data; Modifiers are structured records that always carry data.
3. Tags are flat — no hierarchy.
4. Simple constraints are allowed (e.g. a `TagType` restricts which `ComponentType`s may carry it), and should warn rather than block at design time — consistent with the existing obsolescence pattern ("obsolescence is always at the player's discretion").
5. Values support both flat and percentage, via an explicit `ValueType` enum next to `Value`.
6. Tags stay scoped to unit design for now. The `Tag`/`TagType` entities have no unit-design-specific fields, so relocating them later (own folder/namespace, own entities outside unit design) is cheap — do it when a second consumer needs tagging (e.g. adding a `StructureTag` join table beside `UnitComponentTag`), not before.
   - Note this is about moving the **entities** out of unit design, and is unaffected by `UnitDesignTagsController` existing. That controller was split out of `UnitDesignController` for size and cohesion (see [UD-32](../../work-in-progress/UD-32.md)) and keeps tags firmly *inside* unit design — it is not the relocation this point defers, and does not bring it forward.

## Remaining open work

- Wire the remaining `MoveModifier` cases (`ForestWalker`, `Amphibious`, `RoadAffinity`, `RailAffinity`, `MagTubeAffinity`) through the existing JSON `Effect`/`Conditions` shape.
- Build an authoring UI for movement modifiers (none exists today — they're compiled server-side only).
- ~~Design Capability Grants (concept 3) once a first concrete use case is prioritized.~~ **Done** in UD-56 —
  `found-settlement` and `found-camp` on the `camping-materials` component. The remaining gap is an authoring
  affordance: nothing warns a designer that a capability tag no code reads is inert, or that a strategy's code is
  missing from the catalogue.
- Combat Trigger Effects (concept 4) and Force/Specialist-Scoped Bonuses (concept 5) are parked pending combat-resolution design and `specialists.md` work, respectively.
