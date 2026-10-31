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

Every table above is **global**. None of them carries a `GameId` or a `PlayerId`, and `Code` is unique across the whole
catalogue, so the same four tables serve every game.

`Asset.IsNaturallyOccurring` marks what the world produces by itself — timber, raw rock, bone, hides, and the gathered,
quarried and hunted raw materials. It is authoring data rather than a balance lever: nothing in play reads it and the
database seed is its only writer. The Map Resource Type Manager's Asset pickers narrow their lists by it, so a
MapResourceType can only be authored to yield something extraction could plausibly turn up.

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
- Six fields — `amountAddedPerTurn`, `hasStrategicStockpile`, `isImported`, `isExported`, `demandNotMet`, `isLuxury` — are contract
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
