# Resources Data Model

This document describes the resource hierarchy — how it is structured in the database, how entities relate to each other, and how that hierarchy maps to frontend display components.

---

## Database Schema (`resources` schema)

### Tables

| Table | Primary Key | Description |
|---|---|---|
| `ResourceMetaCategory` | `Id` | Top-level grouping, e.g. "Construction Materials", "Food" |
| `ResourceCategory` | `Id` | Mid-level grouping, e.g. "Stone", "Wood". Has a `CategoryType` that determines how its resources are displayed. |
| `ResourceGroup` | `Id` | Sub-grouping within a grouped category, e.g. "Logs" within "Wood". |
| `Resource` | `Id` | The actual resource, e.g. "Oak", "Marble", "Coal". |

---

## Hierarchy Paths

A `Resource` can reach its `ResourceMetaCategory` via **two mutually exclusive paths**, determined by `ResourceCategory.CategoryType`:

### Grouped Path (`CategoryType = GroupedResources`)

```
ResourceMetaCategory
  └─ ResourceCategory  (CategoryType = GroupedResources)
       └─ ResourceGroup
            └─ Resource  (ResourceGroupId set, ResourceCategoryId NULL)
```

Resources in this path are displayed as a labelled group of tickers — one row per `ResourceGroup`.

### Standalone Path (`CategoryType = StandaloneResources`)

```
ResourceMetaCategory
  └─ ResourceCategory  (CategoryType = StandaloneResources)
       └─ Resource  (ResourceCategoryId set, ResourceGroupId NULL)
```

Resources in this path are displayed as a flat grid with no group labels.

### Mutual Exclusivity

The `resources.Resource` table has a check constraint `CK_Resource_GroupOrCategoryXor` that enforces exactly one of `ResourceGroupId` / `ResourceCategoryId` is non-null:

```sql
([ResourceGroupId] IS NOT NULL AND [ResourceCategoryId] IS NULL)
OR
([ResourceGroupId] IS NULL  AND [ResourceCategoryId] IS NOT NULL)
```

`ResourceMetaCategoryId` on `Resource` is a **denormalized** FK kept for query convenience (avoids joining through the full hierarchy). It always reflects the `ResourceMetaCategory` the resource transitively belongs to.

---

## Entity Relationships

```
ResourceMetaCategory (1)
  ├──< ResourceCategory (N)     [FK: ResourceCategory.ResourceMetaCategoryId]
  └──< Resource (N)             [FK: Resource.ResourceMetaCategoryId — denormalized]

ResourceCategory (1)
  ├──< ResourceGroup (N)        [FK: ResourceGroup.ResourceCategoryId]   — GroupedResources only
  └──< Resource (N)             [FK: Resource.ResourceCategoryId]         — StandaloneResources only

ResourceGroup (1)
  └──< Resource (N)             [FK: Resource.ResourceGroupId]            — GroupedResources only
```

---

## `ResourceCategoryType` Enum

| Value | Int | Meaning |
|---|---|---|
| `StandaloneResources` | 1 | Resources in this category display as a flat grid (no group labels). Resources link via `Resource.ResourceCategoryId`. |
| `GroupedResources` | 2 | Resources in this category are organised into `ResourceGroup`s and display as labelled ticker rows. Resources link via `Resource.ResourceGroupId`. |

---

## Frontend Mapping

The frontend type system mirrors this hierarchy. The entry point is `ResourceTickerCollection`, which receives a `ResourceCategoryData[]` and dispatches to the appropriate component.

### Type Mapping

| Backend | Frontend Type | Component |
|---|---|---|
| `ResourceMetaCategory` | (container, not a standalone component) | `ResourceTickerCollection` |
| `ResourceCategory` (GroupedResources) | `ProcessedResourceCategoryData` | `OrderedResourceCategory` / `GroupedResourceCategory` |
| `ResourceCategory` (StandaloneResources) | `StandaloneResourceCategoryData` | `StandaloneResourceCategory` |
| `ResourceGroup` | `ResourceTickerGroupData` | `OrderedResourceTickerGroup` / `GroupedResourceTickerGroup` |
| `Resource` | `ResourceTickerData` | `ResourceTicker` |

### Frontend Hierarchy

**Grouped:**
```
ResourceTickerCollection
  └─ OrderedResourceCategory / GroupedResourceCategory   ← ProcessedResourceCategoryData
       └─ OrderedResourceTickerGroup / GroupedResourceTickerGroup  ← ResourceTickerGroupData
            └─ ResourceTicker                            ← ResourceTickerData
```

**Standalone:**
```
ResourceTickerCollection
  └─ StandaloneResourceCategory                          ← StandaloneResourceCategoryData
       └─ ResourceTicker                                 ← ResourceTickerData
```

### Discriminant

Both category shapes share the same `type` discriminant field:

| `ResourceCategoryData.type` | Maps to |
|---|---|
| `"processed-resources"` | `ProcessedResourceCategoryData` → `OrderedResourceCategory` |
| `"grouped-resources"` | `ProcessedResourceCategoryData` → `GroupedResourceCategory` |
| `"standalone-resources"` | `StandaloneResourceCategoryData` → `StandaloneResourceCategory` |

---

## API Contract

The upsert endpoint `PUT /resources` accepts `UpsertResourceMetaCategoryDto[]`. The nested structure mirrors the DB hierarchy:

```
UpsertResourceMetaCategoryDto
  └─ UpsertResourceCategoryDto  (CategoryType required)
       ├─ UpsertResourceGroupDto[]   ← populated when CategoryType = GroupedResources
       │    └─ UpsertResourceDto[]
       └─ UpsertResourceDto[]        ← populated when CategoryType = StandaloneResources
```

All codes are derived automatically from `DisplayName` (ProperCase, non-alpha stripped) if not explicitly supplied. Category codes are hierarchical: `MetaCode.CategoryCode`. Group codes extend further: `MetaCode.CategoryCode.GroupCode`.
