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
   tier above tier 1 must group at least two of the tier below.
4. **Structure ↔ domain coupling — done.** A `MilitaryOrganisationalGroup` tag now carries a `MovementDomain`
   (migration `UD-28`, direct-on-the-tag). A hierarchy's tier depth is capped at `max(1, in-use ranks - 1)` for
   that domain, and the Organisation table shows each tier's real rank title. Body↔tag domain consistency
   (a body's default org tag must match the body's domain) is the deliberately-deferred follow-up.
5. **Default structure per body/tag.** The design says a structure is seeded from the body component's default
   tag. Currently every tag with no saved structure returns the same generic "Infantry" default. Per-tag /
   per-body default derivation is not implemented.
6. **`NearEarthOrbit` domain** is excluded from seeding (no title set). Add it when its ranks are defined.
7. **`BelowWater` rank titles** reuse the naval (Water) set as a placeholder — give it a distinct set.
8. **Officer economy & population cap** (growth from combat, per-rank population ceiling, banked-XP promotion)
   from the design doc are **not** implemented here — this pass is configuration only.
9. **Whole-game save/load**: there is no whole-game export/import in the codebase — per-player military config
    persists with the game in the DB. The catalog export/import already carries `MilitaryOrganisationalGroup`
    tags via the components that reference them, so no catalog-transfer change was needed.

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
