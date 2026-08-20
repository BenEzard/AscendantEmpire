# Tags and Modifiers

This document defines the concepts behind unit-design Tags and Modifiers. It resolves an earlier
"working model" discussion (kept below) into five named concepts, decisions on the original open
questions, and the work that remains.

## Current references in the code

## Tags — a global, reusable catalog

**Backend (C#/.NET):**

- `Tag.cs` — entity: `Id`, `Code` (unique, ≤50 chars), `DisplayName` (≤100), `Description` (≤200), `TagType` enum. A global, reusable tag attached to components — either a structural classification or a bonus.
- `UnitComponentTag.cs` — many-to-many join between `Tag` and `UnitComponent`.
- `TagType.cs` — enum: `Classification`, `Combat`, `Movement`, `Ability`.
- EF Core: `DbSet<Tag> Tags`, `DbSet<UnitComponentTag> UnitComponentTags`.
- `UnitDesignTagsController.cs` — dev-only endpoints, backed by `GetTagsHandler`, `CreateTagHandler`, `UpdateTagHandler`, and shape validation in `TagValidation.cs`. `GET /unit-design-tags` has moved onto it; `POST /tags` and `PUT /tags/{id}` are still on `UnitDesignController.cs` until the rest of the split lands — see [UD-32](../../work-in-progress/UD-32.md).
- Contracts: `UpsertTagDto`, `TagView`, `CatalogTag`.

**Frontend**, all under `AscendantEmpire.AscendantEmpireUI/src/features/entitysetuptool/`:

- `types/Tag.ts` — `TagType` union, `Tag` interface, `createEmptyTag()`, `TAG_TYPES`.
- `types/UnitComponent.ts` — `UnitComponent.tagCodes: string[]` (components reference tags by code only); `SlotRuleType` includes `"HasTag"` so slot-acceptance rules can filter by tag.
- `TagSetupDialog.tsx` — authoring dialog for a single global tag.
- `components/sections/TagsSection.tsx` — tab in `ComponentSetupDialog` for attaching/removing tag codes as chips (references only, no definition editing).
- `EntitySetupTool.tsx` — top-level dev tool with an Add/Search toolbar group for Tags.
- `api/tagsApi.ts` — `getTags/createTag/updateTag` hitting `/unit-design/tags`.
- `api/componentsApi.ts` — sends `tagCodes: string[]` on bulk component upsert; backend resolves codes to `Tag` rows.

### Modifiers — narrower, not a unified type

There's no single `Modifier` entity — it splits into two unrelated mechanisms:

1. **Weapon domain damage modifiers** (most developed):
   - Backend: `UnitComponentWeaponDomainModifier.cs` — per-weapon, per-`DomainType` fixed-point `DamageMultiplier`; also gates which domains a weapon can target.
   - Frontend: `WeaponDomainModifiers = Partial<Record<DomainType, number>>` (in both `entitysetuptool` and `unitdesign` feature type files); sparse key/value table UI in Weapon properties, with `domainModifiersToRows()`/`rowsToDomainModifiers()` helpers in `componentsApi.ts`.
2. **Movement modifiers** (data-driven, compiled server-side):
   - `MovementModifier.cs` — parsed record (`Category`, `Effect`, `Value`, `DomainType?`, `Conditions`) decoded from `CompiledDesignStats.ModifiersJson`.
   - `CompiledDesignStats.cs` stores the raw JSON alongside compiled movement points/sight range.
   - `MoveModifier.cs` enum — named bonuses: `JungleCrasher`, `ForestWalker`, `RiverRunner`, `Amphibious`, `IgnoreHillsPenalty`, `RoadAffinity`, `RailAffinity`, `MagTubeAffinity`. Only `JungleCrasher`, `RiverRunner`, and `IgnoreHillsPenalty` are actually wired up in `MovementProfileFactory.cs`; the rest are unused stubs. No frontend authoring UI exists for this yet.

## Concepts

The original discussion (kept below) was treating classification, passive bonuses, capability unlocks, combat triggers, and force-level bonuses as one problem. They don't share a shape, so they're split into five concepts. Only the first two exist today.

### 1. Tags — classification only (implemented)

- Flat, global, string-keyed catalog (`Tag`/`TagType`). No hierarchy — combine meaning via multiple tags on a component (e.g. a camel base carries both a "cavalry" tag and a "desert" tag) rather than a tag tree.
- Tags carry no data beyond their own catalog fields (`Code`, `DisplayName`, `Description`, `TagType`) — they are keys, not behavior.
- Consumed today for classification and for slot-acceptance rules (`SlotRuleType.HasTag`).

### 2. Structured Modifiers — numeric, single-unit scope (implemented, partial)

- Weapon domain damage modifiers and movement modifiers (see above) are the two existing instances.
- Modifiers always carry data (a `Value`, optional `DomainType`/`Conditions`) — this is the dividing line from Tags, which never do.
- Values support both flat and percentage amounts via an explicit `ValueType` (Flat/Percent) enum next to `Value`, rather than a bare, ambiguous number.
- Before adding new modifier categories, finish wiring the remaining `MoveModifier` cases (`ForestWalker`, `Amphibious`, `RoadAffinity`, `RailAffinity`, `MagTubeAffinity`) through the existing `Effect`/`Conditions` JSON shape — it already looks expressive enough for them.

### 3. Capability Grants — tag unlocks an action (not yet designed)

Examples from the original discussion:

- `camp-goods` (equipment) enables building a camp, if contextual conditions are met.
- ~~`self-organised` (training) enables a unit to split without a Leader, if conditions are
  met.~~ **Superseded** by the Unled leadership model on MilitaryOrganisation tags — splitting
  without a leader is now intrinsic to unled structures, not a training grant. See
  [military-organisation.md](../units/military-organisation.md#leadership-model).
- `stealth-observation` (training, sniper) enables a toggleable special movement mode.

These aren't modifiers — they don't change a number, they unlock a player-facing action. Model as a lookup (`TagCode → CapabilityCode`) checked by game/UI logic when the action is attempted, rather than folding this into the modifier shape.

### 4. Combat Trigger Effects — deferred

Examples: `hit-and-run` (move after attacking, if movement points remain), `porcupine-defence` (double defence damage vs. a melee cavalry attacker), `zone-of-control` (long bows attack an incoming attacker), `penetrate-and-explode` (chance of splash damage to nearby units on death).

These need attacker/defender-relative conditions and secondary/triggered effects (e.g. on-death), which is a combat-engine concern, not a tags-and-modifiers concern. They also depend on combat-resolution mechanics that are themselves still unresolved (see
[open-questions.md](../open-questions.md#combat-domains-and-unit-capabilities)). Hold off designing this until combat resolution is settled, otherwise this doc keeps growing open questions instead of resolving them.

### 5. Force/Specialist-Scoped Bonuses — separate scope, deferred

Examples: a blacksmith reducing weapon damage by 5%, a master forager increasing food foraging, an experienced scout reducing ambush chance, an accountant reducing upkeep by 5%, a medic with a range, a drill sergeant imputing a discipline bonus.

These apply to a Formation, not a Unit Component — a different scope entity, and belong conceptually with [specialists.md](../specialists.md) rather than here. Not building now, but the
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
- Design Capability Grants (concept 3) once a first concrete use case is prioritized.
- Combat Trigger Effects (concept 4) and Force/Specialist-Scoped Bonuses (concept 5) are parked pending combat-resolution design and `specialists.md` work, respectively.
