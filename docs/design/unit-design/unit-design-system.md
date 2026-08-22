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

* **Enums serialize as numbers** over the API (no `JsonStringEnumConverter`), so the frontend TypeScript contracts use the numeric enum values.

* **Normalized slot positions.** A slot's UI X/Y is an offset from the owning body image's centre, where 0 is centre and ±1 the edges; values may fall outside −1…1 for slots beyond the image bounds.

### API surface & tooling

`UnitDesignerController` is the player-facing assembly bench, holding the unlock-scoped read/assemble endpoints: the unlocked-bodies read is `GET /unit-designer/bodies` and the slot-candidate feed is `GET /unit-designer/bodies/{ownerComponentId}/slots/{slotId}/components`, whose route names `bodies` because the owner of a slot is always a body. The player's own template list is `GET /unit-designer/templates`, already scoped to the caller's game and player by their token, and a design is saved with `POST /unit-designer/templates` and revised with `PUT /unit-designer/templates/{templateId}`. The compiled-snapshot read is `GET /unit-designer/templates/{templateId}/compiled`, the one endpoint on that controller which is not unlock-filtered — it serves enemy designs as readily as the caller's own, so it takes no game context. The dev "god-view" catalogue read is `GET /unit-design-catalogue`, the dev per-component read is `GET /unit-design-catalogue/{code}`, the single-component create is `POST /unit-design-catalogue` and the update is `PUT /unit-design-catalogue/{code}`, all on `UnitDesignCatalogueController`. The catalogue is authored through the bulk upsert path (`POST /unit-design-catalogue/bulk`, `BulkUpsertUnitComponentsHandler`, keyed by `Code`) and round-tripped between databases via the dev Entity Setup Tool's zip export/import (`CatalogExportExecutor`), which carries each component with its slots nested under the body. All three transfer endpoints now live on `UnitDesignTransferController`: `POST /unit-design-transfer/export`, `GET /unit-design-transfer/export/download` and `POST /unit-design-transfer/import`. The shared global/common template catalogue is authored through `GlobalUnitTemplatesController`, where `POST /global-unit-templates` creates one and `PUT /global-unit-templates/{id}` revises it; a global template has no game or player and a required, unique `Code`, which is why it is a separate resource from the player templates it shares a table with. Still on `UnitDesignController` until the split reaches it: the recompile-all maintenance action.

> `UnitDesignController` is being split into five controllers, one endpoint per commit — see [UD-32](../../work-in-progress/UD-32.md). This paragraph is rewritten in full when the split completes.
