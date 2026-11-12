# Assets Data Model

This document describes the asset hierarchy — how it is structured in the database, how entities relate to each other, and how that hierarchy maps to frontend display components.

---

## Database Schema (`assets` schema)

### Tables

| Table | Primary Key | Description |
|---|---|---|
| `AssetMetaCategory` | `Id` | Top-level grouping, e.g. "Construction Materials", "Food" |
| `AssetCategory` | `Id` | Mid-level grouping, e.g. "Stone", "Wood". Has a `CategoryType` that determines how its assets are displayed, and a `SubjectScope` that decides whether it is shown once or projected per other player. |
| `AssetGroup` | `Id` | Sub-grouping within a grouped category, e.g. "Logs" within "Wood". |
| `Asset` | `Id` | The actual asset, e.g. "Oak", "Marble", "Coal". |
| `StorageClass` | `Id` | How an Asset is kept, as against what it is — the storage yard, the armoury, the granary. `Code` and `DisplayName` are both uniquely indexed. |
| `SupplySet` | `Id` | A group of Assets that satisfies one of a Unit's needs. Carries a `SupplyType` — `Food`, `Fuel` or `Repair`. `Code` is uniquely indexed. |
| `SupplySetAsset` | (`SupplySetId`, `AssetId`) | One Asset in a set, with its `AmountPerUnit` and its `SelectionWeight`. |

Every table above is **global**. None of them carries a `GameId` or a `PlayerId`, and `Code` is unique across the whole
catalogue, so the same tables serve every game.

`Asset.IsNaturallyOccurring` marks what the world produces by itself — timber, raw rock, bone, hides, and the gathered,
quarried and hunted raw materials. It is authoring data rather than a balance lever: nothing in play reads it and the
database seed is its only writer. The Map Resource Type Manager's Asset pickers narrow their lists by it, so a
MapResourceType can only be authored to yield something extraction could plausibly turn up.

### Storage classes

Every `Asset` names exactly one `StorageClass` through `Asset.StorageClassId`, a non-null FK with `DeleteBehavior.NoAction`,
so a class still in use cannot be deleted. The storage class is what a capacity cap binds against.

It is deliberately **not** a level of the hierarchy and **not** a tag. The hierarchy classifies what a thing *is*, for
display; a storage class says how it is *kept*, and the two cut across each other — the storage yard spans two categories
and four groups, while the fuel dump takes four rows out of the middle of a single flat one. A tag would be the wrong
shape as well: tags are many-to-many and optional, where this is exactly one and required, which only a non-null FK can
enforce. The list is flat and does not nest, so a capacity refusal names one thing.

| Code | Display name | What it holds |
|---|---|---|
| `Armoury` | Armoury | Every melee weapon, at all three grades. |
| `FuelDump` | Fuel Dump | Pitch, resin, tar and tallow. |
| `Granary` | Granary | Fodder and the dry staples — wild grains, seeds, legumes, nuts. |
| `Intangible` | Intangible | What is kept nowhere at all: the two experience points, and the three armed services. |
| `Larder` | Larder | Perishable food — meat, fish and the gathered plant foods. |
| `People` | People | The seven population statuses, our own and those we hold. |
| `StorageYard` | Storage Yard | Timber and masonry — logs, planks, raw rock, bricks. |
| `Sundries` | Sundries | The catch-all: small, odd goods that earn no store of their own, plus the special foods. |

`Sundries` is the catch-all, and is deliberately not called the storehouse — that word means the tier-granted generic
pool a class overflows into, so reusing it would make a capacity refusal read as one thing failing twice. `People` and
`Intangible` exist because the FK is required rather than because anything stores them: population statuses are counted
rather than stored, and experience points and the Force rows are not kept anywhere.

The catalogue has no endpoint of its own. It is maintained by the database seed's Storage Classes target, which dispatches
the same upsert any endpoint would.

### Supply sets

A `SupplySet` groups the Assets that satisfy one of a Unit's ongoing needs, so a unit component requires a quantity of a
*set* rather than of one Asset and an army can march on whatever the map actually grows. The needs themselves are in
[units/unit-support-and-supply.md](units/unit-support-and-supply.md#supply-sets).

Like a storage class, a set cuts across the hierarchy rather than being a level of it: `human-food` is most of one
meta-category but not all of it, while a repair set draws on metalwork and timber at once. Unlike a storage class it is
many-to-many and a member carries data of its own, which is why it is a join table and not a column on `Asset`.

| Column | On | What it is |
|---|---|---|
| `SupplyType` | `SupplySet` | The need the set satisfies, which decides what a quantity of it is measured against — per troop, per movement point, or per point of damage. |
| `AmountPerUnit` | `SupplySetAsset` | How much of this Asset one unit of the need costs, as a raw Scale-1,000 `long`. Berries cost 1,670 where most foodstuffs cost 1,000. |
| `SelectionWeight` | `SupplySetAsset` | How heavily this Asset is weighted when one member is drawn at random. Read only for a `Repair` set, and defaults to 1. |

A `Food` or `Fuel` set is an either-or: any one member satisfies the need, at its own rate. A `Repair` set is drawn from
instead — damage picks one member, weighted — so the materials a repair consumes vary from hit to hit.

The requirement rows live in the `unitdesigns` schema (`UnitComponentSupplyRequirement`), since they belong to a component
rather than to the Asset catalogue. The sets are authored in the Entity Setup Tool's Supply Sets dialog, over
`/supply-sets`, and seeded by the database seed's Supply Sets target, which dispatches those same endpoints' handlers.

### Ammunition

Ammunition is the one Unit need that names Assets directly rather than through a set. A weapon declares the kinds it
accepts — up to three — and an attack spends one unit of whichever is loaded, so a row is the Asset and nothing
else. The rows live in the
`unitdesigns` schema (`UnitComponentWeaponAmmunition`), hanging off `UnitComponentWeapon`, and are authored on the weapon
in the component dialog rather than anywhere in the Asset Manager. Why they are not a set is in
[units/unit-support-and-supply.md](units/unit-support-and-supply.md#ammunition).

The Assets themselves sit in the `Weapons.Ammunition` category — sling shot, arrows, bullets and artillery rounds —
stored in the armoury like the weapons beside them. They are **ungraded**, unlike every other `Weapons` Asset, because a
weapon names the Asset it fires outright and has no quality model to pick a grade with.

### Player-scoped tables

| Table | Primary Key | Description |
|---|---|---|
| `PlayerUnlockedAsset` | `(PlayerId, AssetId)` | The Asset is unlocked for that Player. No row means they have never had it. `ObsoleteOnRound` is future-dated: the Asset is available while it is null *or* still ahead of the current Round. |
| `PlayerUnlockedAssetMetaCategory` | `(PlayerId, AssetMetaCategoryId)` | Whether the meta-category's icon appears in the Player's Assets toolbar at all. Categories and groups have no unlock rows — they are shown when they hold at least one available Asset. |

These say what a Player can *see*, not what they *hold*. What a Player holds is not in this schema at all: a banked
figure lives in `AssetBankBalance`, a player-level one on the row that already owns it, and a derived one nowhere.
`Asset.StorageScope` says which, and `GetPlayerAssetsHandler` resolves it at read time. The six fields beside
`Amount` are still contract placeholders (see **Read Path** below).

#### Population, and the subject player

Population is the one part of the catalogue where an amount needs **two** players attached to it — who holds these
people, and whose people they are — and the four global tables have nowhere to put the second.

The catalogue does not gain a second player column. Instead `AssetCategory.SubjectScope` marks a category's **groups**
as templates, and `GetPlayerAssetsHandler` stamps each of them out once per other player at read time. Nothing is
written per game, so a group list cannot go stale when a player is eliminated or renames their people.

| `AssetSubjectScope` | Int | Meaning |
|---|---|---|
| `Own` | 1 | Shown once, counting what the reading player holds. Every category but the population one. |
| `PerOtherPlayer` | 2 | Shown once under its own name; each group beneath it is projected once per other player. |

The `Society` meta-category holds both halves, as two categories and no more — however many players are in the game:

- **`Society.Population`** (`Own`, shown as **My Population**) — `My People` holds Civilians / Slaves / Interred /
  P.o.W, and `Forces` holds Army / Airforce / Navy.
- **`Society.ForeignPopulation`** (`PerOtherPlayer`, shown as **Enemies**) — one group, `Subjugated`, holding Slaves /
  Interred / P.o.W. That group is the template, and the category is a column per opponent once it has been stamped out.

A projected group takes its `DisplayName` from the subject player's `PeopleName` — unique within a game and locked once
play begins — and carries that player's id in `SubjectPlayerId`. Its `Code` is the template's code suffixed with the
subject, e.g. `Society.ForeignPopulation.Subjugated.P12`. **Projections share the template's `Id`**, so `Code` is what
identifies a group in the returned tree; everything in the frontend keys on it. Groups are laid out left to right in
the order the API returns them, so the columns read alphabetically by people name.

A template group is pruned on its own contents *before* the fan-out, so a player who has unlocked none of the foreign
assets gets no `Enemies` category rather than one empty column per opponent. An eliminated player keeps their column:
people held from them are still held.

##### What the figures will come from

Every population figure reads zero, because the ledger behind it does not exist. The holdings ledger that fills them
is `RS-20` work that has not landed: a table keyed `(HolderPlayerId, AssetId, SubjectPlayerId)`, read on two axes. Rows where `HolderPlayerId` is me and `SubjectPlayerId` is another player fill that player's
projected group. Rows where `SubjectPlayerId` is me, summed across every holder, fill the Slaves / Interred / P.o.W.
entries under `My People` — members of my people held by somebody else.

That second axis tells a player how many of their people an enemy is holding without their having scouted anything.
The disclosure is **deliberate**: it is what puts pressure on a player to negotiate for their release.

Civilians, Army, Airforce and Navy share the panel but will not be ledger rows at all — they come from settlement
populations and the force rosters.

##### Which players are projected

`GetPlayerAssetsHandler` projects onto every other **active** player in the game. Two narrower gates are specified but
not yet applicable:

- **Only discovered players.** There is no record of one player having met another; first contact is a Diplomacy
  concern. `PlayerKnownForce` and `PlayerKnownStructure` carry an observer and an owner, so the question is derivable,
  but nothing durable records it.
- **Only where the figures are above zero.** This needs the ledger. It costs nothing once it exists — no row means no
  category.

---

## Hierarchy Paths

A `Asset` can reach its `AssetMetaCategory` via **two mutually exclusive paths**, determined by `AssetCategory.CategoryType`:

### Grouped Path (`CategoryType = GroupedAssets`)

```
AssetMetaCategory
  └─ AssetCategory  (CategoryType = GroupedAssets)
       └─ AssetGroup
            └─ Asset  (AssetGroupId set, AssetCategoryId NULL)
```

Assets in this path are displayed as a labelled group of tickers — one row per `AssetGroup`.

### Standalone Path (`CategoryType = StandaloneAssets`)

```
AssetMetaCategory
  └─ AssetCategory  (CategoryType = StandaloneAssets)
       └─ Asset  (AssetCategoryId set, AssetGroupId NULL)
```

Assets in this path are displayed as a flat grid with no group labels.

### Mutual Exclusivity

The `assets.Asset` table has a check constraint `CK_Asset_GroupOrCategoryXor` that enforces exactly one of `AssetGroupId` / `AssetCategoryId` is non-null:

```sql
([AssetGroupId] IS NOT NULL AND [AssetCategoryId] IS NULL)
OR
([AssetGroupId] IS NULL  AND [AssetCategoryId] IS NOT NULL)
```

`AssetMetaCategoryId` on `Asset` is a **denormalized** FK kept for query convenience (avoids joining through the full hierarchy). It always reflects the `AssetMetaCategory` the asset transitively belongs to.

---

## Entity Relationships

```
AssetMetaCategory (1)
  ├──< AssetCategory (N)     [FK: AssetCategory.AssetMetaCategoryId]
  └──< Asset (N)             [FK: Asset.AssetMetaCategoryId — denormalized]

AssetCategory (1)
  ├──< AssetGroup (N)        [FK: AssetGroup.AssetCategoryId]   — GroupedAssets only
  └──< Asset (N)             [FK: Asset.AssetCategoryId]         — StandaloneAssets only

AssetGroup (1)
  └──< Asset (N)             [FK: Asset.AssetGroupId]            — GroupedAssets only

StorageClass (1)
  └──< Asset (N)             [FK: Asset.StorageClassId]          — required, cuts across the hierarchy

SupplySet (1)
  └──< SupplySetAsset (N)    [FK: SupplySetAsset.SupplySetId]    — the Assets that satisfy the need

Asset (1)
  └──< SupplySetAsset (N)    [FK: SupplySetAsset.AssetId]        — an Asset may serve several sets

Asset (1)
  └──< UnitComponentWeaponAmmunition (N)                         — the weapons that fire it
                             [FK: UnitComponentWeaponAmmunition.AssetId]
```

---

## `AssetCategoryType` Enum

| Value | Int | Meaning |
|---|---|---|
| `StandaloneAssets` | 1 | Assets in this category display as a flat grid (no group labels). Assets link via `Asset.AssetCategoryId`. |
| `GroupedAssets` | 2 | Assets in this category are organised into `AssetGroup`s and display as labelled ticker rows. Assets link via `Asset.AssetGroupId`. |

---

## Frontend Mapping

The frontend type system mirrors this hierarchy. The entry point is `AssetTickerCollection`, which receives a `AssetCategoryData[]` and dispatches to the appropriate component.

### Type Mapping

| Backend | Frontend Type | Component |
|---|---|---|
| `AssetMetaCategory` | (one toolbar button and one panel, not a component of its own) | `AssetTickerCollection` |
| `AssetCategory` (GroupedAssets) | `GroupedAssetCategoryData` | `OrderedAssetCategory` |
| `AssetCategory` (StandaloneAssets) | `StandaloneAssetCategoryData` | `StandaloneAssetCategory` |
| `AssetGroup` | `AssetTickerGroupData` | `OrderedAssetTickerGroup` |
| `Asset` | `AssetTickerData` | `AssetTicker` |

The `Ordered` prefix is about the tickers inside a group being draggable into a usage order, not about a second kind of category.

### Frontend Hierarchy

**Grouped:**
```
AssetTickerCollection
  └─ OrderedAssetCategory                             ← GroupedAssetCategoryData
       └─ OrderedAssetTickerGroup                     ← AssetTickerGroupData
            └─ AssetTicker                            ← AssetTickerData
```

**Standalone:**
```
AssetTickerCollection
  └─ StandaloneAssetCategory                          ← StandaloneAssetCategoryData
       └─ AssetTicker                                 ← AssetTickerData
```

### Discriminant

Both category shapes share the same `type` discriminant field, which `mapPlayerAssets.ts` maps from the wire's `categoryType`:

| `AssetCategoryData.type` | From `categoryType` | Maps to |
|---|---|---|
| `"grouped-assets"` | `GroupedAssets` | `GroupedAssetCategoryData` → `OrderedAssetCategory` |
| `"standalone-assets"` | `StandaloneAssets` | `StandaloneAssetCategoryData` → `StandaloneAssetCategory` |

### Read Path

The Resources toolbar and its panels are driven entirely by `GET /player/assets`:

```
GET /player/assets
  └─ playerAssetsApi.ts        wire DTOs, one type per FrontendContracts interface
       └─ usePlayerAssetsQuery cached under playerAssetsKey(gameId, playerId)
            └─ mapPlayerAssets.ts   view models for one meta-category's panel
                 └─ AssetTickerCollection
```

- One toolbar button and one panel per unlocked `AssetMetaCategory`, in the order the API returned; one panel is open at a time. The button's DOM
  id comes from `assetToolbarButtonDomId(code)`, and its panel anchors to it.
- The API sorts meta-categories, categories, groups and assets, so array order is render order. A group's grid `column` is its 1-based position in
  that order — there is no column on the wire.
- The query refetches when the window regains focus, and the end-turn mutation invalidates it: the amounts and the obsoleting countdowns are measured
  against the Round.
- `AssetGrade` is the API's member names — `None`, `Low`, `Medium`, `High`. The bronze, silver and gold shields a ticker wears are presentation for
  Low, Medium and High.
- `AssetTickerData.obsoleteIn` is in **rounds**, from `ObsoleteInRounds`.
- `amount` is measured, resolved per asset from whichever store its `StorageScope` names: summed across every bank the player owns for a
  `SettlementBank` asset, `PlayerResearchState.UnspentDirectedPoints` for Technology experience, and zero for Culture and every `Derived` figure,
  whose stores do not exist yet. Whole and rounded down, so holding nine tenths of a log reads as none of it.
- A banked asset the player holds none of is **not in the tree at all**, so a panel lists only what they hold. The gate is on `StorageScope` rather
  than on the figure: a `Player` asset is always sent, because Technology experience empties at the end of every turn by design and a ticker that
  vanished each turn would read worse than one at zero, and a `Derived` figure is always sent while no ledger records it. A group left with no
  assets drops and takes its category with it; a meta-category keeps its icon over an empty panel, which its own unlock row decides.
- Six fields — `amountAddedPerTurn`, `hasStrategicReserve`, `isImported`, `isExported`, `demandNotMet`, `isLuxury` — are contract
  placeholders the API sends as zero or false until their own tickets land.
- An asset's `distributionConfig` and `productionConfig` have no wire source, so the Resource Manager's distribution and production pages show their
  empty states.

---

## API Contract

The upsert endpoint `PUT /assets` accepts `UpsertAssetMetaCategoryDto[]`. The nested structure mirrors the DB hierarchy:

```
UpsertAssetMetaCategoryDto
  └─ UpsertAssetCategoryDto  (CategoryType required)
       ├─ UpsertAssetGroupDto[]   ← populated when CategoryType = GroupedAssets
       │    └─ UpsertAssetDto[]
       └─ UpsertAssetDto[]        ← populated when CategoryType = StandaloneAssets
```

All codes are derived automatically from `DisplayName` (ProperCase, non-alpha stripped) if not explicitly supplied. Category codes are hierarchical: `MetaCode.CategoryCode`. Group codes extend further: `MetaCode.CategoryCode.GroupCode`.

`UpsertAssetDto.StorageClassCode` is the one required field with no derivation and no partial-update preservation: the
column behind it is a non-null FK with no sensible default. It is resolved against `StorageClass` rows already committed,
and an unknown code fails the whole request naming every unresolved code at once. Those rows are written by the database
seed's Storage Classes target rather than over HTTP — the catalogue has no endpoint.
