# Units and Formations

## Definitions

- **Unit** — multiple soldiers equipped with the same weaponry/gear, trained at the same time, and
  of the same nationality (e.g. archers).
- **Sub Unit** — a Unit is made up of Sub Units when there are enough soldiers and ranking officers
  to comprise them. A Unit of 1,000 men, with an army organised in tens, has 10 Sub Units of 100
  soldiers each.
- **Formation** — a collection of Units combined under a command structure, residing on the same
  campaign tile. (Two units of the same kind — e.g. two archer units — still form a Formation.)
  Formations can also contain [Specialists](specialists.md).
- **Unit Stack** — multiple Units that reside on the same campaign tile but are **not** combined
  into a Formation.
- **Initial force strength** — the size of the Unit when it was created. If a Unit created with
  1,000 men loses 200 in battle, its initial force strength is still 1,000.

## Formation rules

- All Units in a Formation must share the same **domain** (land, sea, air), except:
- Units (up to the applicable quantity) of domain Land or Sea may be on the Water domain provided
  there are Units carrying a respective `LAND_CARRYING_CAPACITY` or `AIR_CARRYING_CAPACITY`.

## Unit design

The weapons, armour, and training a Unit is equipped with are chosen by the player, who must weigh
**combat effectiveness against cost and production time**.

- A Unit is made up of **1 base Unit Component** and **0 or more non-base Unit Components**,
  assigned into applicable **Slots**.
- A **template entry** is a special unit design where only the base is defined and the other slots
  are empty. Players use templates to define their own unit designs.

### Material Class

When designing a Unit you choose which **Class** of component to equip (e.g. you choose to give a
soldier a sword). The *concrete* type of sword (broadsword, short sword, katana) is decided **at
creation time** from available assets, ordered by the player's [Material Preference](production-and-assets.md).

### Unlocking components, units, and slots

- Components are unlocked via **technology discovery** (e.g. discovering "Wood Tools" unlocks the
  "Club" component).
- A pre-existing Unit Design is unlocked once **all** of its requisite components are unlocked.
- Players can mix-and-match available components into unique designs for a tactical edge.
- Technology can also **add Slots** to unit designs. By default these new slots are empty in
  pre-existing units.

### Obsolescence

Discovering new components can make existing components obsolete (e.g. tank technology obsoletes
cavalry).

A **Unit** becomes obsolete via either of:

- all components on the design **except the base** are obsolete, or
- the **base** component is obsolete (regardless of how many non-obsolete components the design
  has).

Obsolescence is always at the **player's discretion**: auto-obsolescence is the default, but the
player ultimately decides when a unit is retired. Existing units with an obsolete component still
function and may upgrade to a different component.

## Unit Components

A component is one of these types:

- **base** (chassis)
- **engine** (power source)
- **armour**
- **weapon**
- **supply** (equipment)
- **training** (skill)

### Component attributes

| Attribute | Description | Empire-flexible |
| --- | --- | --- |
| Unit Component Code | API-referenceable unique name of the component | No |
| Unit Component Name | User-displayed name | No |
| Action Constraints | | No |
| Design Constraints | | No |
| Fits to Slot List | | No |
| Domain | | No |
| Catalogue Path | | No |
| Weight | Weight of the object. `Base.Power − Σ(components.weight) = movement points` | No |
| Power | Amount of power the component has (typically only populated for BASE items) | Yes |
| Attack values | Per type: Slash, Pierce, Crush/Impact, Burn, Poison | Yes |
| Defensive values | Per type: Slash, Pierce, Crush/Impact, Burn, Poison | Yes |
| Minimum Range | Only applicable to WEAPON components | Yes |
| Maximum Range | Only applicable to WEAPON components | Yes |
| Sight | | Yes |
| Disturbance | | Yes |
| Supply Health Resource / Quantity | | No |
| Supply Movement Resource / Quantity | | No |
| Ammunition Movement Resource / Quantity | | No |

### Base (chassis)

The base is the main component; it determines how many slots are available (i.e. how many other
components fit).

- A base has an **operation domain** which determines whether it moves on land, sea, or air.
- A base has a **type** used by the UI to group components.

### Component constraints

- A Unit's `Σ(weight of components)` must be `< Σ(power of components)`.
- The movement speed of a Unit on the campaign map derives from `Σ(Power)`.

### Obsoleting a component

When a component is made obsolete by a new technology discovery, it is no longer used in
automatically generated unit designs. Existing units keep working and may upgrade. When **all**
components of a unit are obsolete, the unit design itself is obsoleted.

## Technical detail

Two pairs of tables hold the unit and unit-design information (a `config.*` definition table plus a
per-game `game.Empire*` table):

| Schema & table | Description |
| --- | --- |
| `config.UnitMasterList` / `game.EmpireUnitMasterList` | "Header" information about a Unit; the list from which unit selection (for building) occurs. When a record is marked obsoleted, no more units of this type can be built. Notable columns: `UnitName` (renameable), `UnitNameCode` (fixed), `StowageSize` (space per soldier in a transport; `< 0` means cannot be transported), `MinTroopCount`, `MaxTroopCount` (divisible by `TroopIncrement`), `TroopIncrement`, `IsTemplateEntry`, `UnlockedOnTurn` (`-1` if locked), `NotificationOfUnlock`, `ObsoletedOnTurn`. |
| `config.UnitTemplate` / `game.EmpireUnitTemplate` | The design (template) of the Unit. Notable columns: `EmpireUnitComponentCode` (component in this slot), `SlotName`, `SlotCanBeModified`, `SlotIsHidden`. |

## Open questions

- Can some weapons be ranged but not melee? (Perhaps if range `> 1` they cannot be used for melee.)
- When a unit has multiple weapons, how is partial ammunition availability handled?
- Domain of weapons — Land, Air, Water, Underwater, Space (a `Domain` string on the component).
- Integrated design with [Infrastructure](campaign-map.md#infrastructure-terrain-improvements).
- Is there any benefit to unit-ID inheritance?
- How is a "compiled unit" carrying weapons of differing range handled?
