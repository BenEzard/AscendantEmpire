# Units and Formations

## Definitions

These are ordered by size (so intentionally not alphabetical):

### Command

A collection of one or more `Units` operating under a shared command structure on the same Campaign Tile.

A Command has:

* one Command Commander
* a maximum total strength based on the Commander’s rank
* a maximum number of Units
* a maximum number of **attached** Specialists
* share `Detection` and `Sight` information.

### Unit

A persistent body of soldiers with the same components, faction, training time and location, and authorized organization.

A Unit has:

* a Unit Commander
* an Authorized Strength
* a Current Strength
* one or more Companies (with embedded Officers)
* no **attached** Specialists.

The Authorized Strength of the Unit is set at Unit creation time, and has size bounds of between `UnitComponentBody.MinimumUnitSize`and `Max(Faction.MaximumUnitSize, UnitComponentBody.MaximumUnitSize)`

A Unit containing two or more `Companies` requires one additional Unit Commander.

* **Initial Unit Strength** — the size of the Unit when it was created. If a Unit created with 1,000 men loses 200 in battle, its Initial Unit Strength is still 1,000. This is the maximum number of soldiers that it can have.

### Company (Plural: Companies)

A persistent organizational subdivision of a `Unit`.

* Each Company requires one Company Officer.
* Additional Companies may be formed according to preferred Company size and available Officers.
* A Unit with three or more Companies requires a separate Unit Commander.
* Companies remain after casualties and are not recalculated automatically.
* Companies may merge or disband during reorganization or when no longer viable.

A `Unit` is comprised of one or more Companies.


 when there are enough soldiers **and** ranking Officers to sub-divide. 

*Company creation rule:* enough soldiers (based on on the `UnitComponentBody.MinimumUnitSize`) and Officers to establish another Company. The number of required Officers is 1 per `Company` (up to 2) or 1 per `Company` + 1 (where greater or equal to 3)

*Company persistence rule:* the Company continues until disbanded, merged, leaderless, or critically depleted.

Each Company requires one or more Company `Officers`. 

For example if the MiniumUnitSize is 100: 
* And there are 200 soldiers with 2 Officers, then the Unit will be two Companies. 
* If there are 300 soldiers and 3 Officers, then it will be a two Companies of 150 soldiers. 
* If there are 180 soldiers and 1 Officer, then it will be a single Company. 
* If there are 120 soldiers and 2 Officers then it will be a single Company. 
* If there are 200 soldiers and 3 Officers then there will be two Companies (one with an extra Officer)

### Officer

An Officer is a type of **embedded** `Specialist`. They can be earned through `Command Experience` or paid for in `Research Points` to attain a specified `Rank`. 

The higher the rank, the longer it takes (or the more research points it costs). For example, you might generate 3,000 research points per turn, but the military academy may only spend 100 per turn; a general may take 500.

- `Combat Experience` can be **converted** toward Officers. An entire `Company` or `Unit` can be sent "to
  the academy" via a button; a percentage then emerge at various ranks. Higher `Combat Experience`
  raises the likelihood of higher ranks. This doesn't necessarily *complete* their training, but can reduce it.
- The senior-most Officer in a Command determines how many `Units` it can contain and `Specialists` can be assigned to it. (A Unit, therefore, does not itself have command experience.)


### Rank
* Ranks are **unlocked per Player** (except for Rank 1 which is always available).

* **Promotion Ceiling** - With the exception of Rank 1, how many of each rank can exist simultaneously for a Player is governed by calculations. e.g. Max 1 General per 10 settlements of city size.

#### Properties of Rank
* Rank Title - Segmented by `Movement Domain` (e.g. "General" for Land would be "Admiral" for Sea (by default). Rank Titles can be changed by the `Player` (for free, at any time) onc.




The Max # of soldiers Per Unit may be adjusted down

MaxFactionCount

|Rank| Rank Title | Max # of soldiers Per Unit | Max # of attached Specialists |
| --- | --- | --- | --- |
|1|Lieutenant | 44 | 1 |
|2|Captain | 200 | 3 |
|3|Major | 1,000 | 5 |
|4|Colonel | 3,200 | 10 |
|5|Major General | 10,000–16,000 | 12 |
|6|General | | 15 |

  
**Unit Stack** 

Multiple `Units` that reside on the same Campaign Tile but are **not** combined into a `Command`.

<span style="color:orange;">TODO: Units (up to the applicable quantity) of domain Land or Sea may be on the Water domain provided there are Units carrying a respective `LAND_CARRYING_CAPACITY` or `AIR_CARRYING_CAPACITY`.

<span style="color:orange;">TODO: Unit Design: component production time.

<span style="color:orange;">TODO: Material Class. When designing a Unit you choose which **Class** of component to equip (e.g. you choose to give a
soldier a sword). The *concrete* type of sword (broadsword, short sword, katana) is decided **at
creation time** from available assets, ordered by the player's [Material Preference](production-and-assets.md).

<span style="color:orange;">TODO: Obsoleting a component check. When a component is made obsolete by a new technology discovery, it is no longer used in automatically generated unit designs. Existing units keep working and may upgrade. When **all**
components of a unit are obsolete, the unit design itself is obsoleted.

<span style="color:orange;">TODO: When a unit has multiple weapons, how is partial ammunition availability handled?

TODO: Integrated design with [Infrastructure](campaign-map.md#infrastructure-terrain-improvements).</span> 

