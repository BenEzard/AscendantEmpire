# Unit Design System

The Unit Design System lets a player assemble their own unit designs from a catalogue of unlockable parts. This page covers the player-facing concepts first, then the technical model that backs them.

## Concepts & In-Game Considerations

**Terminology specific to this page:**

* Cost - a cost is both a type and a quantity of `Resource`.

* A `Template Slot` is a place where a `Unit Component` can be plugged in to a Body Component. Only Body Components expose Template Slots. It has the following properties:
  
  * Visible - y/n
  
  * X/Y (where it sits on the UI)
  
  * 1 or more Unit Component acceptance rules (e.g. must be of type weapon, must have a weight no greater than X, must be a subtype of Y)

### The building blocks

* A **Unit Component** is a single part you fit onto a design. Every component has a category — a body, weapon, engine, armour, equipment or training aid.

* A **Body** is the chassis every design is built around. It decides the Domain the unit moves in (land, sea, air or near-Earth orbit), how fast it moves, how much health it has, how many individuals the unit fields, and — crucially — which **Template Slots** are available to fit other parts into.

* A **Template Slot** is a socket on the body. Each slot only accepts certain kinds of part (via its acceptance rules), so a "weapon mount" won't take an engine.

* A **Unit Template** is a finished design: one body, plus the parts fitted into its slots.

### Unit Template

* As the Player gains certain technological advances, they unlock `Unit Components`.

* Once all Unit Components are unlocked, a `Unit Template` is unlocked.
  
  * <span style="color:orange;">TODO (later): The player will need to be notified that this is available</span>
  
  * <span style="color:orange;">TODO (later): Need to analyse existing models for obsoleting</span>

### Designing a unit — the trade-offs

The interesting decisions live in how the parts play off each other:

* **Weight vs. mobility & power.** Heavier loadouts cost movement and power budget.

* **Detection vs. visibility.** Detection is how well the unit spots others; visibility is how easily it is spotted. A scout wants high detection and low visibility.

* **Weapons.** Damage is split across five types — crush, pierce, burn, slash and poison — and a weapon has a range band, may be dual-wielded, and can only engage the Domains it is built to hit.

* **Armour.** Reduces incoming damage per damage type, so armour choices are a direct answer to the weapons you expect to face.

* **Unit size.** Size bounds no longer live on the body (the min/max/step fields were removed in UD-29). A unit's legal sizes come from its military organisation: the structure's tiers and rank ceilings for commissioned units, or the tag's authored Base/Default/Max for unled units. See [military-organisation.md](../units/military-organisation.md#size-model--operative-vs-ceiling).

* **Tags.** Tags act both as buffs and as classification labels (e.g. "sword", "stealth"). Because slot acceptance rules can match on tags, they also decide what is allowed to fit where.

### Upgrades & obsolescence

Newer components and templates can **supersede** older ones, forming upgrade paths. When a player unlocks a superseding part, the game can offer to retire the part it replaces. (See the supersession design doc for detail.)

### Past Decisions

1. **Only a Body component can have slots.** I previously lifted this, but design complexity wasn't worth the pay-off.

2. **Slots are not locked per Player.** In fact, I've removed all locking functionality on a slot. Customisation is prevented by components only.

## Technical Details

### Model overview

The catalogue is a single `UnitComponent` table (schema `unitdesigns`, see `AscendantEmpireContext`), with each row carrying at most one type-specific extension: `UnitComponentBody`, `UnitComponentWeapon` or `UnitComponentArmour` (each 1:1, sharing the component's primary key). A component's `ComponentType` decides which extension it has. A body may additionally carry an *inherent* weapon and/or armour (e.g. a cavalry charge) alongside its body extension.

Template Slots (`UnitComponentSlot`) hang off `UnitComponentBody` — only bodies expose slots. Each slot owns one or more acceptance rules (`UnitComponentSlotRule`).

### Component types

`ComponentType` is: `Body`, `Weapon`, `Engine`, `Armour`, `Equipment`, `Training`, `Extension`. `Extension` exists in the enum but is currently a no-op (no special handling); the others behave as their names suggest, though only Body/Weapon/Armour have a dedicated extension table today.

### Bodies, weapons and armour

* `UnitComponentBody` holds the movement Domain (`DomainType`), campaign and battlefield movement points, power, hitpoints, and the body's `Slots`. (The unit-size bounds — min/max/increment — were removed in UD-29; sizing is a military-organisation concern.)

* `UnitComponentWeapon` holds an attack `DamageProfile` (a five-value complex type: Slash, Pierce, Crush, Burn, Poison), a min/max range, a dual-wield flag, and a set of `UnitComponentWeaponDomainModifier` rows. Each modifier both declares a Domain the weapon can attack and scales its damage there.

* `UnitComponentArmour` holds a resistance `DamageProfile`.

### Tags and supersession

Bonus/classification tags live in `Tag` (`TagType` categorises them) and attach to components through the `UnitComponentTag` join. Upgrade paths are directed links: `ComponentSupersession` between components and `TemplateSupersession` between templates, each keyed predecessor→successor with an `IsRecommended` flag.

### Slots and acceptance rules

A `UnitComponentSlot` records its owning body (`UnitComponentBodyId`), an optional label, a visibility flag, a normalized UI position, and its rules. A `UnitComponentSlotRule` has a `RuleType` (`ComponentType` or `HasTag`), a `SlotRuleOperator` (`Equals`, `NotEquals`, `In`) and a string value. `SlotRuleEvaluator` is the single authority for "what fits where" — a candidate is accepted only if it satisfies *every* rule on the slot.

### Templates and compilation

A `UnitTemplate` references its `BodyComponent` and holds one `UnitTemplateComponent` per filled body slot (each pins a slot to the fitted component; at most one component per slot). Global/common templates carry a unique `Code` and no game/player; custom templates are owned by a player.

`UnitTemplateAssembly` resolves a save request (a base body code plus a positional list of slot codes), enforcing that the base is a Body and that every fitted part passes `SlotAssemblyValidator` — which checks each fitted component sits in one of the body's own slots, satisfies that slot's rules, and that no slot is filled twice. `CompileUnitTemplateHandler` then produces a denormalized snapshot (`CompiledUnitTemplate` + `CompiledUnitTemplateComponent` + `CompiledUnitTemplateCapability`) with the design's aggregated stats, readable without the per-player unlock filter (e.g. for enemy designs).

### Per-player unlock state

`PlayerUnlockedComponent` and `PlayerUnlockedTemplate` track which parts and templates a player has unlocked, each with an `ObsoleteOnTurn` field for soft obsolescence. There is **no** per-player slot locking — slots have no lock flag and no per-player unlock table (see Past Decisions).

### Conventions

* **Fixed-point (Scale 1,000).** Weight, detection, visibility, movement points, power, hitpoints, every `DamageProfile` value and weapon domain multipliers are stored as raw `long`s where 1,000 = 1.0 (`ScaledAmount`). Editors convert to/from human-friendly decimals at the edit boundary.

* **Enums serialize as their string names** over the API — `Program.cs` registers a `JsonStringEnumConverter` — so the frontend TypeScript contracts use string unions matching the C# member names, not numbers.

* **Normalized slot positions.** A slot's UI X/Y is an offset from the owning body image's centre, where 0 is centre and ±1 the edges; values may fall outside −1…1 for slots beyond the image bounds.

### API surface & tooling

Unit design is served by **five controllers**, split out of a single 712-line `UnitDesignController` one endpoint per commit (see [UD-32](../../work-in-progress/UD-32.md) for the increment-by-increment record). Routes come from the class name via `KebabCaseParameterTransformer`, so each controller owns exactly one URL prefix.

| Controller | Route prefix | Endpoints | Authorization |
|---|---|---|---|
| `UnitDesignerController` | `/unit-designer` | `GET bodies`, `GET bodies/{ownerComponentId}/slots/{slotId}/components`, `GET templates`, `POST templates`, `PUT templates/{templateId}`, `GET templates/{templateId}/compiled` | `[Authorize]`, any authenticated player |
| `UnitDesignCatalogueController` | `/unit-design-catalogue` | `GET`, `GET {code}`, `POST`, `PUT {code}`, `POST bulk` | `EntitySetupTool` on the writes; the two reads are ungated — see below |
| `UnitDesignTagsController` | `/unit-design-tags` | `GET`, `POST`, `PUT {id}`, `GET types`, `PUT types` | `EntitySetupTool` on the writes; the two reads are ungated — see below |
| `UnitDesignTransferController` | `/unit-design-transfer` | `POST export`, `GET export/download`, `POST import` | `SystemPanel` |
| `GlobalUnitTemplatesController` | `/global-unit-templates` | `POST`, `PUT {id}`, `POST recompile-all` | `EntitySetupTool` |

**The designer** is the player-facing assembly bench, and the only one of the five reachable by an ordinary player. It never takes identity from the request: game and player come from the caller's game token via `TryGetGameContext`, which answers 403 when no game has been entered. Its feeds are unlock-scoped — `GET bodies` returns only bodies the player has unlocked, and the slot-candidate feed returns only unlocked components that satisfy the slot's rules, evaluated in memory by the shared `SlotRuleEvaluator`. The route names `bodies` because the owner of a slot is always a body; `GetSlotCandidateComponentsHandler` enforces that by matching the slot on `UnitComponentBodyId`. The compiled-snapshot read is the single exception to all of this: it is not unlock-filtered and takes no game context, because it serves enemy designs as readily as the caller's own.

**The catalogue** is the dev "god-view" over every component: `GET /unit-design-catalogue` returns the whole graph, `GET /unit-design-catalogue/{code}` one component in full for the edit dialog, and authoring goes through either the single-component `POST`/`PUT {code}` pair or the bulk upsert path (`POST /unit-design-catalogue/bulk`, `BulkUpsertUnitComponentsHandler`, keyed by `Code`). Tags and tag types are the same shape of dev-authored reference data and sit on their own controller, with `{id:int}` on the tag update keeping it distinct from the literal `types` segment.

**Transfer** round-trips the catalogue between databases as a zip through the dev Entity Setup Tool, carrying each component with its slots nested under the body. It is the only controller here that is not purely MediatR: export and import run as background processes and return a `processId` the caller polls, while the download reads the process row through an `IDbContextFactory` and streams a file off disk. That mix of authorization posture (`SystemPanel`, not `EntitySetupTool`) and architectural style is why transfer is its own controller rather than part of the catalogue.

**Global templates** are separated from the player templates they share a table with because they are a different resource: no game, no player, a required and unique `Code`, and no unlock check on save. `POST /global-unit-templates/recompile-all` is the exception to that controller's name — it rebuilds every compiled snapshot, custom templates included, and lives there for its permission gate rather than its scope. Run it after editing catalogue components, whose changes do not propagate to snapshots already built from them.

> **Four reads carry no permission gate**: `GET /unit-design-catalogue`, `GET /unit-design-catalogue/{code}`, `GET /unit-design-tags` and `GET /unit-design-tags/types`. They are not anonymous — the `FallbackPolicy` still requires a token — but any authenticated account can read the whole dev catalogue. Gating them is tracked as **UD-35**, and is blocked on the designer moving onto its own unlock-scoped feeds rather than loading through the god-view.
