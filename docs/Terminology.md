# Terminology

The project-wide home for definitions. A term defined here means the same thing in every document, every task description and every identifier in the codebase. **This file is the authority**: other documents may repeat a definition where a reader needs it to hand — `docs/work-in-progress/asset-bank-refactor.md` keeps its own copy of the storage terms — but where two documents disagree, this one wins and the other is wrong. Every new term goes in here, in the section it belongs to, at the point it is agreed rather than afterwards.

Where a definition rules out a near-synonym, the ban is part of the definition. Those entries exist because the wrong word was in use at some point and cost a conversation.

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

## What is built — three words, three things

* `Building` - something built **inside** a settlement, and what grants the settlement its capabilities: an output each turn, a storage class it can keep, a unit design or component unlocked, a specialist employable, technology or culture points. There is no building entity yet. See [design/settlements.md](design/settlements.md#buildings).

* `Structure` - something that stands on a **campaign map tile**: the object with artwork, a tile slot, a position, a sight range and build points. Modelled by the `Structure` catalogue and the `StructureInstance` world row. A settlement's own tile carries one to draw it, which is why a village and a camp are structures while everything a settlement contains is not. `Structure` is also a `RecordType`, and note that it is not a `Force`.

* `Infrastructure` - the `Structures` built on the tiles **surrounding** a settlement, linked back to the settlement that raised them. The terrain improvements: roads, farms, mines, an offshore rig. See [design/campaign-map/infrastructure.md](design/campaign-map/infrastructure.md).

The three are not interchangeable. Only a building grants dedicated storage; a structure never does.

## Assets and storage

* `Asset` - anything the economy counts: a good, a population status, a point currency. One row in the asset catalogue, which is static and seedable and knows nothing about games or players.

* `Storage class` - how an asset is kept, as against what it is. `Asset.StorageClassId`, required, exactly one per asset. The display hierarchy of meta-category, category and group classifies what a thing *is* and is a separate axis entirely.

* `Stockpile` - a settlement's ordinary storage taken as a whole: its **dedicated storage** plus its **generic storage**. What production is credited into and what an expense is drawn from. A `Strategic Reserve` is deliberately **not** part of a stockpile; keeping the two apart is the whole point of it.

* `Dedicated storage` - capacity granted for one storage class by a building: the armoury, the oil dump. Not "dedicated store".

* `Generic storage` - tier-granted capacity any storage class can overflow into. The storehouse. Never "general stockpile", and not the same thing as `Sundries`.

* `Sundries` - the catch-all *storage class*, for goods that earn no store of their own. Not generic storage: that is the storehouse, and the two are not interchangeable.

* `Strategic Reserve` - the building that holds a quantity out of circulation, and the mechanism around it. Its storage class is designated per instance by the player at build time rather than fixed by the building type, and its capacity is a budget separate from the settlement's stockpile. A camp never holds one. **Never "Strategic Stockpile"**, which is what this was called until the word *stockpile* was given to ordinary storage.

* `Reserved`, or **in reserve** - `AssetBankBalance.ReservedQuantity`. Out of circulation and out of every total: the only state a quantity is in before it is released. Never "stockpiled", never "locked".

* `Released` - moved from `ReservedQuantity` back to `Quantity`, which is the single act that makes a reserved quantity available and visible in totals. Whole or partial, by player order or by the conquest rule. Never "unlocked".

* `Diversion` - the percentage of production a player's policy sends to a strategic reserve, split off at the production credit rather than swept up afterwards. A percentage of **production**, never of existing stock.

* `Cargo` - what a unit bank holds. Excluded from empire totals until deposited, even inside a settlement.

## Asset banks and networks

* `Asset bank` - where a quantity physically sits. Every bank is a settlement's bank or a unit's bank; there is no player-level bank. A settlement's bank is its bank for the settlement's whole life, and ownership moves rather than the bank.

* `Network` - the set of banks sharing a `NetworkKey`. What a settlement can draw on. Also what a Command's units' banks form.

* `Re-key` - changing which network a bank belongs to. Never moves a balance.

* `Local-first` - a settlement expense drains the site's own bank before touching the rest of its network.

* `Levelling` - cutting the fullest banks to a common water-line on a debit, or filling the emptiest first on a credit.

* `Parent settlement` - the settlement a camp delivers its production to each turn (`Settlement.ParentSettlementId`). Stored, never recomputed. The camp delivers; the parent only receives.

* `Segment` - one `PopulationSegment` row: a quantity of one status, of one nationality, in one bank.
