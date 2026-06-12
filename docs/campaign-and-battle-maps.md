# Campaign Maps and Battle Maps

This document describes how campaign maps and battle maps are structured, generated, stored, and related to each other — including the design intent behind the two-tier map system.

---

## Overview

The game uses two map layers that work in concert:

- **Campaign Map** — the zoomed-out strategic view. A grid of tiles each representing a large area of terrain. Players move armies and manage territory at this level.
- **Battle Map** — the zoomed-in tactical view. When a battle is triggered on a campaign tile, that tile is "expanded" into a higher-resolution grid for the battle itself.

The two layers are designed for *continuous harmony*: the terrain you see in a battle map is a faithful zoom-in of the campaign terrain beneath it, not a random or disconnected layout.

---

## Campaign Maps

### Map Gallery vs. Game Instance

Campaign maps have two representations:

| Layer | Schema | Tables | Purpose |
|---|---|---|---|
| Gallery (template) | `mapgallery` | `CampaignMapGalleryIndex`, `CampaignMapGallery` | A reusable blueprint created in the Map Editor. Multiple games can be started from the same gallery entry. |
| Game instance | `game` | `CampaignMap` | A per-game copy of the campaign map, extended with runtime state (resources, fog of war, control). |

When a game is created, `game.CampaignMap` is populated by copying biome and geography data from `mapgallery.CampaignMapGallery` for the chosen map.

### `mapgallery.CampaignMapGalleryIndex`

One row per map in the gallery. Key fields:

| Field | Description |
|---|---|
| `Id` | Primary key |
| `MapName`, `Description` | Display metadata |
| `Width`, `Height` | Map dimensions in tiles |
| `CreationStatus` | `MapCreationStatus` enum — whether generation is complete |
| `CreationEngine` | `0 = BlankMap`, `1 = PerlinNoiseV1` |
| `PropertiesJson` | JSON bag of generation parameters. For Perlin maps this includes `seed`, `waterRatio`, `hillAndMountainRatio`, `poles`, `equator`, `HorizontalWrap`, `VerticalWrap`. |
| `CampaignMapOffsetId` | First tile PK for contiguous storage (see below) |
| `HorizontalWrap`, `VerticalWrap` | Recommended wrapping behaviour (overridable per game) |
| `GeographyMapPath`, `BiomeMapPath` | Paths to preview images (Perlin engine only) |

### `mapgallery.CampaignMapGallery`

One row per tile in the gallery. Tiles are stored contiguously:

```
tile(x, y).PK = CampaignMapOffsetId + (y * Width + x)
```

Key fields: `CampaignMapGalleryId` (manual PK), `CampaignMapGalleryIndexId` (FK), `XCoord`, `YCoord`, `BiomeTypeId`, `CampaignMapGeographyTypeId`.

### `game.CampaignMap`

The live copy of the map for a specific game. Rows are inserted from the gallery tiles at game creation via a single bulk SQL `INSERT...SELECT`.

Additional fields beyond the gallery copy: `ForageAmount`, `HuntAmount`, `FishAmount`, `FodderAmount`, `FirstSeenOnTurnId`, `FirstSeenByPlayerId`, `LastVisitedOnTurnId`, `LastVisitedByPlayerId`, `ControlledByPlayerId`.

Primary key: `CampaignMapId` (identity — not contiguous offset). Foreign key: `GameId → game.GameIndex`.

### Player Campaign Map

`game.PlayerCampaignMap` holds each player's personal fog-of-war view. One row per (player, tile). Initially all tiles are unknown; biome and geography are revealed as players explore.

---

## Game Index (`game.GameIndex`)

One row per game. Relevant fields for maps:

| Field | Description |
|---|---|
| `CampaignMapIndexId` | FK to `mapgallery.CampaignMapGalleryIndex` — which gallery map this game is using |
| `HorizontalWrap`, `VerticalWrap` | The game creator's wrapping choice (overrides the gallery recommendation) |
| `GenerationSeed` | `long` — seed used for biome diversity in all battle maps generated for this game. Always populated; auto-generated randomly if not supplied at game creation. |
| `BattleMapSize` | `int` — width and height (in tiles) of every battle map in this game. Square; valid range 30–60. |

---

## Perlin Noise Campaign Map Generation

The `PerlinNoiseV1` engine produces maps via `AddCampaignMapHandler` → `PerlinNoiseCampaignMapGenerator`.

### Key concepts

**Noise field**: A domain-warped FBM (Fractal Brownian Motion) Perlin noise field is used for altitude. The same field is later reused by the battle map generator to guarantee edge continuity.

**Geography assignment**: Noise values are thresholded to assign `CampaignMapGeographyType`:
- DeepWater, ShallowWater, Flat, Hilly, Mountainous

**Biome assignment**: Biome is determined by geography type combined with optional climate modifiers (`poles`, `equator`) and altitude-relative position.

**PropertiesJson**: The master seed and all generation parameters are persisted in `CampaignMapGalleryIndex.PropertiesJson`. The battle map generator reads `seed` from this JSON to reproduce the same noise field later.

---

## Battle Maps

### Design intent

Battle maps are not stored in a shared gallery — they are per-game. Two games built on the same campaign map will have different biome distributions in their battle maps, but identical altitude topology (because altitude is derived from the shared campaign Perlin field).

- **Altitude** — sourced from a Perlin noise field seeded with `masterSeed ^ GameIndex.GenerationSeed`. Both adjacent tiles within a game share this combined seed, so edge continuity is preserved. Different games produce different altitude topology for the same campaign tile.
- **Biome diversity** — sourced from `GameIndex.GenerationSeed` XOR-mixed with tile coordinates. This makes each game's battles feel distinct.

### `battle.BattleMapIndex`

One row per campaign tile that has had a battle generated for it (within a specific game). Created lazily when the first battle on that tile is triggered.

| Field | Description |
|---|---|
| `Id` | Identity PK |
| `CampaignMapId` | FK → `game.CampaignMap.CampaignMapId` (unique — one battle map per game tile) |
| `BattleMapOffsetId` | First `battle.BattleMap.Id` for this battle map's tiles (see contiguous storage below) |
| `GenerationStatus` | `MapCreationStatus` enum |
| `Status`, `ErrorMessage` | Human-readable generation state for debugging |

### `battle.BattleMap`

One row per tile within a battle map. Tiles are stored contiguously:

```
tile(x, y).Id = BattleMapOffsetId + (y * BattleMapSize + x)
```

`Id` is assigned manually at generation time (not auto-increment). This mirrors the pattern used by `CampaignMapGallery`.

| Field | Description |
|---|---|
| `Id` | Manual PK |
| `BattleMapIndexId` | FK → `battle.BattleMapIndex.Id` |
| `XCoord`, `YCoord` | Position within the battle map (0-based) |
| `AltitudeLevel` | `BattleMapAltitude` enum: `BelowWater=-1`, `Ground=0`, `Low=1`, `Medium=2`, `High=3`, `Peak=4` |
| `BiomeTypeId` | FK → `map.CampaignBiomes` |

Indexes:
- Unique composite on `(BattleMapIndexId, XCoord, YCoord)`
- Non-unique on `BattleMapIndexId` (for bulk tile retrieval)
- Non-unique on `BiomeTypeId`

### Battle Map Generation (`BattleMapGenerator`)

Located at `AscendantEmpire.Application/MapGeneration/BattleMapGenerator.cs`.

Triggered by `GenerateBattleMapRequest(GameId, CampaignMapId)` via `GenerateBattleMapHandler`.

**Steps:**

1. **Guard** — if `battle.BattleMapIndex` already exists for this `CampaignMapId` with status `Complete`, return the existing Id immediately.
2. **Load** `GameIndex` (for `BattleMapSize`, `GenerationSeed`, `CampaignMapIndexId`).
3. **Load** `game.CampaignMap` (for `XCoord`, `YCoord`, `BiomeTypeId`, `CampaignMapGeographyTypeId`).
4. **Load** `mapgallery.CampaignMapGalleryIndex` (for `masterSeed` from `PropertiesJson`, and `Width`/`Height` for bounds checking).
5. **Load neighbour geography** — the four orthogonal `game.CampaignMap` neighbours. Used for edge blending.
6. **Generate** using `BattleMapGenerator.Generate(masterSeed, gameSeed, ...)`.
7. **Persist** — insert `BattleMapIndex` row (status `Creating`), bulk-insert all tiles via `SqlBulkCopy`, then update status to `Complete`.

### Noise and edge continuity

The battle map samples a Perlin noise field seeded with `masterSeed ^ gameSeed`. The sampling coordinates zoom into the campaign tile's sub-region:

```
noiseX = camX + (bx + 0.5) / battleSize
noiseY = camY + (by + 0.5) / battleSize
```

Tile `(camX, camY)` samples the noise domain at `x ∈ [camX, camX+1)`. Because all tiles within the same game share the same `masterSeed ^ gameSeed`, adjacent battle maps share identical noise values at their shared edge — the seam is invisible. Different games produce different altitude topology for the same campaign tile.

### Neighbour blending

The four orthogonal campaign tile neighbours contribute a gradient to the edges of the battle map. A Mountainous neighbour raises altitude near that edge; a Flat or water neighbour lowers it. The blend falls off over the first 20% of the tile width/height.

Bias values per geography type:
- `DeepWater`: −0.30
- `ShallowWater`: −0.15
- `Flat`: 0.00
- `Hilly`: +0.20
- `Mountainous`: +0.45

### Altitude classification

Altitude thresholds vary by the campaign tile's own geography type:

| Geography | Ground | Low | Medium | High | Peak |
|---|---|---|---|---|---|
| Flat | < 0.55 | < 0.85 | < 0.95 | < 0.99 | ≥ 0.99 |
| Hilly | < 0.20 | < 0.55 | < 0.80 | < 0.95 | ≥ 0.95 |
| ShallowWater | < 0.70 | < 0.90 | < 0.97 | < 0.99 | ≥ 0.99 |
| DeepWater | < 0.90 | < 0.97 | < 0.99 | < 1.00 | — |

Water tiles with `Ground` altitude are reclassified as `BelowWater`.

### Biome diversity

A separate fine-scale noise pass (seeded from `gameSeed ^ (camX * prime1) ^ (camY * prime2)`) adds patches of compatible biomes within the tile. The campaign biome is the majority; variants are chosen from a compatibility matrix:

| Campaign Biome | Possible Variants |
|---|---|
| Grassland | Grassland, Woodland, Arid |
| Forest | Forest, Woodland, Grassland |
| Woodland | Woodland, Forest, Grassland |
| Jungle | Jungle, Forest, Woodland |
| Arid | Arid, Grassland, Desert |
| Desert | Desert, Arid |
| Tundra | Tundra, Grassland |
| Ice | Ice, Tundra |
| Coastal / Ocean | Coastal / Ocean (no variants) |

`BelowWater` tiles always receive `Coastal` biome regardless of the campaign biome.

---

## Contiguous ID Storage Pattern

Both `CampaignMapGallery` and `battle.BattleMap` use manually assigned PKs stored contiguously from an offset. The offset (stored on the index row) points to the first tile; any tile can be addressed by:

```
Id = OffsetId + (y * Width + x)
```

This avoids row-by-row inserts and enables bulk `SqlBulkCopy` writes. A table-level lock (`HOLDLOCK, TABLOCKX`) is acquired when computing the next available offset to prevent collisions under concurrent generation.

---

## Schema Summary

```
mapgallery.CampaignMapGalleryIndex  ──┐
                                      ├─ game.GameIndex.CampaignMapIndexId
mapgallery.CampaignMapGallery       ──┘
         │
         │ (copied at game creation)
         ▼
game.CampaignMap  (FK: GameId → game.GameIndex)
         │
         │ (FK: CampaignMapId, one per game tile)
         ▼
battle.BattleMapIndex
         │
         │ (FK: BattleMapIndexId)
         ▼
battle.BattleMap  (individual battle tiles)
```

`game.GameIndex` carries `GenerationSeed` and `BattleMapSize`, which govern all battle map generation for that game.
