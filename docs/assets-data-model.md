# Assets Data Model

This document describes the asset hierarchy — how it is structured in the database, how entities relate to each other, and how that hierarchy maps to frontend display components.

---

## Database Schema (`assets` schema)

### Tables

| Table | Primary Key | Description |
|---|---|---|
| `AssetMetaCategory` | `Id` | Top-level grouping, e.g. "Construction Materials", "Food" |
| `AssetCategory` | `Id` | Mid-level grouping, e.g. "Stone", "Wood". Has a `CategoryType` that determines how its assets are displayed. |
| `AssetGroup` | `Id` | Sub-grouping within a grouped category, e.g. "Logs" within "Wood". |
| `Asset` | `Id` | The actual asset, e.g. "Oak", "Marble", "Coal". |

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
- Seven fields — `amount`, `amountAddedPerTurn`, `hasStrategicStockpile`, `isImported`, `isExported`, `demandNotMet`, `isLuxury` — are contract
  placeholders the API sends as zero or false until their own tickets land. A zero amount renders like any other: nothing is hidden for holding none.
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
