# Terminology

## Forces — what stands on a tile

* `Unit` - one fielded body of soldiers, built from a single `Unit Template`, standing on one tile. Soldiers within a Unit were trained at the same time, in the same location, to the same template. A Unit moves as one on the Campaign Map and can separate into its constituent formations during a battle on a Battle Map. Modelled by the `UnitInstance` entity, whose suffix distinguishes the fielded thing from the `Unit Template` it was built from.

* `Command` - one or more `Units` operating under a shared command structure on a single Campaign Tile, led by a commanding officer whose rank bounds how many Units it may contain and how many `Specialists` may be attached. A Command is always **optional and deliberate**: a Unit standing alone is never *automatically* wrapped in a Command of one, because a Command consumes an officer from a deliberately scarce pool. A player may still choose to form one around a single Unit — the design permits it, it is simply rarely worth the officer.

* `Force` - the umbrella term for *either a Unit or a Command*: whatever occupies a tile and moves as one. It is a category, not a type - there is no Force entity, no base class and no polymorphic foreign key. In code it is the `RecordType` discriminator (`Unit` or `Command`, and note that `Structure` is a record type but not a Force) plus the flat projections that carry either, such as `ObservedForce` and `MapObserver`.

* `troops` - individual soldiers. Lower case, a mass noun, and never a group: a Unit contains troops, it is not one. This is the sense in `HealthyTroops` and `InjuredTroops`.

> **Retired terms.** `Formation` (which meant a group of Units in some documents, a grouping inside a Unit in others, and a synonym for Unit in a third), `Contingent`, and `Troop` used as the name of a group. See [work-in-progress/unit-command-terminology.md](work-in-progress/unit-command-terminology.md) for the reasoning and the migration.
>
> Two survive as prose only, never as types. **"Unit stack"** means Units sharing a tile that are *not* under one Command. **"formation"**, lower-case, survives solely inside [military-organisation.md](design/units/military-organisation.md), where it is defined narrowly as a grouping of soldiers at one tier *inside* a Unit — a company, a platoon. That is a real thing the model needs a noun for and it is none of the four terms above, since "tier" names the level rather than what sits at it. It appears nowhere else.

## Unit design

* `Catalogue` - the shared, global set of authored unit-design data: `Unit Components` with their slots and rules, the buff/bonus tags, and the global `Unit Templates`. Spelled the British way everywhere - identifiers, routes, comments and prose alike. Two exceptions keep the American spelling and must not be renamed: the applied migrations `AddUnitComponentCatalogAndTemplates` and `UD-25_AddTagTypeCatalog`, whose names are recorded in `__EFMigrationsHistory`, and `InitialCatalog`, which is a `SqlConnectionStringBuilder` property rather than one of ours.

* `Template Slot` - a slot on a `Unit Template` where a `Unit Component` *may* be fitted. The Template Slot defines what kind of components can be fitted to it.

* `Unit Component` - a component (body, weapon, engine, armour, equipment, radar, system, fitting, training, special) that can be put onto a `Unit Template`.

* `Unit Template` - the configuration of `Unit Components` that a `Unit` has. The Unit Template may be pre-specified or player-specified. The player-specified may be from scratch, or starting from a pre-specified template.
