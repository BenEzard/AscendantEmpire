# Military Organisation — Implementation Notes

Status of the per-player ranks & structures feature (Officer Class + Military Organisation pages of the
Military Advisor). Companion to [military-organisation.md](military-organisation.md).

## What was implemented

**Backend**
- Domain entities (`AscendantEmpire.Domain.Entities.Military`): `PlayerOfficerRank`,
  `PlayerOrganisationStructure`, `PlayerOrganisationStructureTier`.
- EF config: new `military` schema, DbSets, unique indexes, FKs (`ConfigureMilitarySchema` in
  `AscendantEmpireContext`).
- Code-defined defaults: `MilitaryOrganisationDefaults` (rank ladders per domain; fallback "Infantry"
  structure 10/50/100/500/2000).
- CQRS handlers + contracts: get/save officer ranks, get organisation tags, get/save organisation structure.
- `MilitaryController` (game-scoped, reads game/player from the game token).
- Game setup: `GameSetupService.SeedPlayerMilitaryAsync` seeds every player's rank ladders at game creation.
- Tests: `AscendantEmpire.Tests/Handlers/Military/MilitaryHandlersTests.cs`.

**Frontend**
- `militaryApi.ts` API client.
- Officer Class page/table **realigned** to ranks only (In Use / Level / Rank title / Max Soldiers /
  Max Specialists), loaded & saved per player, per domain.
- Military Organisation page/table **realigned** to structures: a searchable combo box of
  `MilitaryOrganisationalGroup` tags selects a structure; the table is tier-name + base/fan-out + computed
  size, with the tier number standing in for the positional officer rank.
- `SearchableComboBox` gained an optional `inputClassName` for light surfaces (default unchanged).

## Outstanding work

1. **EF migration — done.** `20260724230206_UD-27_AddPlayerMilitaryOrganisation` creates the three `military`
   tables and matches the current model (`dotnet ef migrations has-pending-model-changes` reports no pending
   changes). It has been applied to the database. The SqlServer/Testcontainers tests can now run.
2. **Per-tier size ceiling.** The tier *depth* cap is now enforced (tier count ≤ the domain's `max(1, ranks-1)`),
   but the per-tier *size* rule — "a tier's soldier size ≤ the Rank-N `MaxSoldiers` ceiling" — is still not
   enforced in either the save handler or the UI.
3. **Fan-out ≥ 2 (the design's floor rule).** Only `multiplier ≥ 1` is enforced server-side; the design says a
   tier above tier 1 must group at least two of the tier below. *(Commissioned structures only — unled tags
   have no tiers, so the rule does not apply to them.)*
4. **Structure ↔ domain coupling — done.** A `MilitaryOrganisationalGroup` tag now carries a `MovementDomain`
   (migration `UD-28`, direct-on-the-tag). A hierarchy's tier depth is capped at `max(1, in-use ranks - 1)` for
   that domain, and the Organisation table shows each tier's real rank title. Body↔tag domain consistency
   (a body's default org tag must match the body's domain) is the deliberately-deferred follow-up.
5. **Default structure per body/tag.** The design says a structure is seeded from the body component's default
   tag. Currently every tag with no saved structure returns the same generic "Infantry" default. Per-tag /
   per-body default derivation is not implemented. *(Closed for **unled** tags by the unled pass below — an
   unled tag's sizing is authored on the tag itself and needs no per-player structure. Still open for
   commissioned tags.)*
6. **`NearEarthOrbit` domain** is excluded from seeding (no title set). Add it when its ranks are defined.
7. **`BelowWater` rank titles** reuse the naval (Water) set as a placeholder — give it a distinct set.
8. **Officer economy & population cap** (growth from combat, per-rank population ceiling, banked-XP promotion)
   from the design doc are **not** implemented here — this pass is configuration only.
9. **Whole-game save/load**: there is no whole-game export/import in the codebase — per-player military config
    persists with the game in the DB. The catalogue export/import carries `MilitaryOrganisationalGroup` tags via
    the components that reference them — and, since the starter units became hand-authored content, `TagDto`
    now also carries the tag's `MovementDomain`, `LeadershipModel` and unled sizes. Without those the zip
    would move a settler tag between databases silently commissioned, and game setup would then reject a
    catalogue that looked correct at source. The import applies the same unled sizing rules as the Tag
    manager (shared via `TagValidation.ValidateOrganisationFields`), so it is not a back door around them.

## Unled units & game-start spawn (current pass)

Implements the [Leadership model](military-organisation.md#leadership-model) additions to the design:

- `Tag` (`MilitaryOrganisationalGroup` tags only) gains an authored `LeadershipModel`
  (Commissioned / Unled) plus `UnledBaseSize` / `UnledDefaultSize` / `UnledMaxSize` — the
  direct-on-the-tag pattern from UD-28. Unled tags have **no per-player structure rows**; the
  Military Organisation page shows them read-only.
- A template's org tag is **derived from its components** at compile time (a non-body component's
  org tag beats the body's; two non-body org tags fail compilation) and snapshotted onto
  `CompiledUnitTemplate.OrganisationTagId`.
- `UnitInstance` now references `UnitTemplateId` (the canonical template model) instead of the
  legacy `UnitDesignId`; unit display and movement reads come from the compiled snapshot.
- Spawn validates unled sizing (`n × Base`, `≤ Max`); `GameSetupService` unlocks the starter templates
  (`SETTLER`, `SCOUT`) and auto-spawns a settler (`UnledDefaultSize`) and scout (1) per player at their
  start position.
- **Not** implemented (doc-only for now): split/merge runtime, any unled combat/Command/veterancy
  guardrails (see the armed-settler risk in the design doc's Future review).

### The starter units are hand-authored catalogue content

Nothing in the application creates them. `GameSetupService.StarterTemplateCodes` (`SETTLER`, `SCOUT`)
names two **global unit templates that must already exist in the catalogue**; game setup reads them,
unlocks them and their components for every player, and spawns one of each. The database seed deliberately
does **not** create them — it covers reference data only (tag types, geographies, biomes).

Each starter template must satisfy all of the following, or game creation fails with a specific error
rather than starting a game with no units:

| Requirement | Failure if missing |
| --- | --- |
| A global template exists with the code | "Starter template(s) not found in the catalogue…" |
| It has a compiled snapshot | "…has no compiled snapshot — recompile the catalogue templates." |
| Its resolved organisation tag is `Unled` | "…is not unled — starting units must resolve to an unled organisation tag…" |
| That tag has an authored `UnledDefaultSize` | "…which has no authored UnledDefaultSize." |
| The map has one start position per player | "The map has N start position(s) but the game has M players…" |

The unled requirement is not incidental: the spawn size *is* the tag's `UnledDefaultSize`, and a
commissioned template has no equivalent single default.

**[starting-requirements.md](../starting-requirements.md) is the authoring checklist** — the exact tags,
components and templates, in order, with the field values. In short: organisation tags first (leadership
model + unled sizes), then the components carrying those tags, then the global templates in the Unit
Designer. The catalogue export/import zip carries all of it, including the organisation fields, so a
catalogue authored once can be moved to another database (see the note under Outstanding work item 9).

Code matching is case-insensitive, so the lower-cased code the Unit Designer derives from a display name
(`settler`) satisfies the `SETTLER` requirement.

Carried debts from the `UnitInstance` FK swap (`UnitDesignId` → `UnitTemplateId`):

- **Movement modifiers are lost for template-built Units.** The legacy `CompiledDesignStats.ModifiersJson` has
  no compiled-template equivalent, so `MovementProfileFactory` currently builds an empty modifier list —
  template-built Units move un-modified. The parsing is left wired so the JSON drops straight back in once it is
  compiled onto `CompiledUnitTemplate`. **Still open**, and it now bites slightly harder: F-14 made the
  per-turn allowance real, so an un-modified allowance is one a player can actually see being handed out.
- **Movement replenishment — done (TM-3, delivered as F-14).** `UnitInstance.CampaignMovementRemaining` is
  granted at the start of the owning player's turn, after supply is reckoned, with unspent movement carried up
  to one point. Battle movement moved to its own column, `BattleMovementRemaining`, which is granted at spawn
  and **not** replenished — battle rounds are not campaign rounds and the battle turn pipeline has no processes.
- **The Hunt action is disabled in the UI.** It was gated on legacy unit design id 2; re-enable it via a
  compiled capability tag once capability grants exist (`tags-and-modifiers.md`, concept 3).
- **`UnitDesign` still exists** and is still referenced by `PlayerUnitDesignState` and `CompiledDesignStats`.
  Only `UnitInstance` moved off it; retiring the entity is a separate cleanup.
- **Migration `UD-30` deletes all existing `UnitInstance` rows** (and their orders/participants/movement
  steps) — the ids it carried were design ids with no meaning as template ids. Dev-only data, but the
  deletion is deliberate and irreversible.

## Questionable implementations (flagged for review)

- **`MaxSoldiers` nullability doubles as "no ceiling".** A blank/`null` ceiling means "no soldier cap" (top
  rank). In the UI a blank field and "no ceiling" are the same state — acceptable, but worth confirming this
  isn't confused with "0 / not yet set".
- **Save is a keyed upsert, but not concurrency-safe.** Both save handlers now update in place by natural key
  (ranks by `(PlayerId, Domain, Level)`, tiers by `(StructureId, TierIndex)`) — inserting new keys and
  deleting dropped ones — so unchanged rows keep their ids. There is still no row-version / optimistic
  concurrency check, so two concurrent saves for the same player last-writer-wins.
- **`GetOfficerRanks` synthesises defaults for un-seeded domains.** A game predating the seeding (or a domain
  never seeded) silently returns code defaults that aren't persisted until the next save. Convenient, but the
  read is then not a pure reflection of the DB.
- **Per-player rows carry no `GameId`.** They key on `PlayerId` only (matching `PlayerUnitDesignState`). Player
  ids are globally unique so this is correct, but game-scoped bulk operations must join through `Player`.
- **Combo box feed reuses the whole tag list filtered by type.** Fine at current tag volumes; revisit if the
  organisation-tag set grows large (no server-side search/paging).
- **Structure tier `Multiplier` is overloaded** (tier 1 = base soldiers; tiers above = fan-out). One column,
  two meanings — mirrors the UI but is a mild schema smell.
