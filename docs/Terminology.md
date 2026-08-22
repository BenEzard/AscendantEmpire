# Terminology

* `Catalogue` - the shared, global set of authored unit-design data: `Unit Components` with their slots and rules, the buff/bonus tags, and the global `Unit Templates`. Spelled the British way everywhere - identifiers, routes, comments and prose alike. Two exceptions keep the American spelling and must not be renamed: the applied migrations `AddUnitComponentCatalogAndTemplates` and `UD-25_AddTagTypeCatalog`, whose names are recorded in `__EFMigrationsHistory`, and `InitialCatalog`, which is a `SqlConnectionStringBuilder` property rather than one of ours.

* `Template Slot` - relates to the Unit Design System. A slot on a `Unit Template` where a `Unit Component` *may* be fitted.  The Template Slot defines what kind of components can be fitted to it.

* `Troop` - A group of 1 or more `Units` and (optionally) their `Contingent`. A Troop move as one on the Campaign Map, and can separate into their Units during a battle on a Battle Map.

* `Unit` - A group of soldiers trained at the same time, in the same location, with the same `Unit Template`.

* `Unit Component` - a component (body, weapon, engine, armour, equipment, radar, system, fitting, training, special) that can be put onto a `Unit Template`.

* `Unit Template` - the configuration of `Unit Components` that a `Unit` has. The Unit Template may be pre-specified or player-specified. The player-specified may be from scratch, or starting from a pre-specified template.

* 




