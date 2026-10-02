# Plan: Perlin Noise Map Generator (PerlinNoiseV1)

## Context
The game needs a procedural map generator that produces realistic Campaign Maps (strategic overview) and on-demand Battle Maps (tactical 50×50 view per campaign tile). Previous attempts at this have been abandoned. The `PerlinNoiseV1` engine is already registered as a `MapCreationEngine` enum value and stubbed in `CreateCampaignMapHandler.cs` (line 83–85) — this plan fills in that stub.

The generator must produce:
- Continent-like landmasses with water, islands, mountain ranges, and biome regions
- BattleMaps that are seamlessly edge-continuous with their neighbours
- Deterministic output (same seed → same map), so BattleMaps can be generated on demand

Work begins in `MapGenerationPlay` (a standalone console prototype) and migrates into `AscendantEmpire.Application` once the algorithm is stable.

---

## Answers to Design Questions

### Geography first, then biomes — separately?
Yes. Geography (heightmap → Flat/Hilly/Mountainous/Water) is the foundation everything else depends on. Biomes require a climate simulation (temperature + moisture) which is a second pass over the same noise seed. They are implemented as separate sub-steps within the same generator.

### Do temperature and moisture need to go into the existing Biome tables?
No. Temperature and moisture are *generation inputs*, not game properties. The existing `map.CampaignBiomes` table stores game-mechanics data (movement cost, assets, etc.) and should not be changed. The Whittaker classification lookup (temperature × moisture → BiomeType) lives entirely inside the generator code.

### BattleMaps on-demand from the seed — where are seeds stored?
- The **master seed** is stored in `PropertiesJson` on the `CampaignMapGalleryIndex` record (written back after generation so the map can always be reproduced).
- When a battle is triggered on campaign tile (X, Y), the system reads the master seed from `CampaignMapGalleryIndex.PropertiesJson`, computes a **derived local seed** via `localSeed = masterSeed ^ (tileX * 2654435761L) ^ (tileY * 2246822519L)`, and generates the 50×50 BattleMap.
- The `BattleMapGalleryIndex` record stores the derived local seed (`GenerationSeed`) for that tile's BattleMap — useful for debugging and re-generating individual tiles independently.
- Do not pre-generate all BattleMaps. The `BattleMapGallery` table is populated lazily on first battle for each tile.

### Can I see the output?
Yes — `MapGenerationPlay` already references `SixLabors.ImageSharp` (v3.1.11). PNG + CSV output during all prototype steps. The console app will accept a `--count N` argument to batch-generate N maps with random seeds in one run, making it easy to iterate visually over many outputs.

### Biome transition rules?
Enforce a compatibility graph. The Whittaker model naturally produces most valid transitions, but a post-generation smoothing pass enforces these hard rules:
- Ice ↔ Tundra only (Ice may not neighbour Grassland, Forest, or Desert)
- Tundra ↔ Grassland or Forest (Tundra may not neighbour Desert)
- Desert ↔ Arid only (Desert may not neighbour Grassland, Forest)
- Coastal/Ocean buffer: ShallowWater (Coastal) normally separates Ocean from land — but **occasional Ocean-to-land transitions are permitted** to represent deep harbours or cliff faces dropping straight to deep water
The pass replaces violating tiles with the nearest valid neighbour biome via BFS.

### Continent shape variety
Plain fBm noise produces "upside-down bowl" shapes (smooth peaks, symmetric slopes) because the noise centred on an island peak is naturally radially symmetric. To avoid this:
- **Domain warping**: before sampling the height noise, warp the (x, y) coordinates using a second noise layer. This produces twisted, irregular coastlines and avoids obvious circular continent shapes.
- **No radial falloff mask**: do not apply a distance-from-centre falloff; instead, rely on domain warping alone to keep land patches from being centred.
- **Ridged multifractal noise** for mountain ranges: `ridged = 1 - |noise|`, which produces sharp ridgelines rather than smooth domes.
- **Multiple noise passes at different scales** (large = continents, medium = peninsulas/bays, small = coastal detail) combined with different weights.

### Once the algorithm is stable — where does it move?
The noise engine and generator classes migrate from `MapGenerationPlay` into `AscendantEmpire.Application` as the `GeneratePerlinNoiseCampaignMap.cs` handler. The `MapGenerationPlay` project is then retired or kept as a visualisation/QA tool only.

---

## Parameters (PropertiesJson schema for PerlinNoiseV1)

```json
{
  "seed":                 "optional int64 — random if absent; stored back after generation",
  "waterRatio":           "55",
  "hillAndMountainRatio": "25",
  "poles":                "true",
  "equator":              "false",
  "horizontalWrap":       "false",
  "verticalWrap":         "false",
  "battleMapSize":        "50"
}
```

---

## Implementation Steps (execute in order — each produces visible output before the next begins)

---

### Step 1 — Perlin Noise Engine (MapGenerationPlay prototype)
*Goal: get a working, visible heightmap before touching the application.*

Rebuild `MapGenerationPlay` as a .NET 8 console app (currently no source files, only build artifacts and historical output PNGs).

Implement from scratch:
- **`PerlinNoise.cs`** — Classic Ken Perlin gradient noise. Permutation table, fade/lerp/grad functions. Single method: `float Sample(double x, double y)`.
- **`FractalBrownianMotion.cs`** — Combines N octaves: `fbm = Σ amplitude × noise(freq × x, freq × y)`. Configurable: octave count, persistence (amplitude decay), lacunarity (frequency multiplier).
- **`RidgedNoise.cs`** — `ridged = 1 - |fBm|` variant. Used for mountain ranges and sharper terrain features.
- **`TileableFbm.cs`** — Samples noise on a torus surface so the output tiles seamlessly in X and/or Y. Used for wrapping maps and for BattleMap edge continuity.
- **`DomainWarpedNoise.cs`** — Warps (x, y) by a second noise layer before sampling, producing irregular continent shapes instead of bowl shapes.

Console app entry point: accepts `--seed <n>`, `--count <n>` (defaults to 10 random seeds), `--width`, `--height`, `--waterRatio`. Loops over each seed and writes `output/map_seed_{seed}_{w}x{h}.png` and matching `.csv`.

Output: PNG heightmap (grayscale) + CSV.

**Verification**: Run the console app. PNG should show smooth multi-scale hills and valleys with no obvious circular bowl shapes.

---

### Step 2 — Campaign Map Geography
*Goal: heightmap → CampaignMapGeographyType for every (X, Y) tile.*

Add to `MapGenerationPlay`:

**Height → Geography classification:**
- Normalise heightmap to [0, 1].
- Apply latitude polar boost if `poles=true`: add a gradient that reduces height near top/bottom rows, pushing more tiles below the water threshold → creates polar sea / ice.
- Water threshold controlled by `waterRatio`:
  - `< waterLevel` → candidate for water
  - Distance transform from water/land boundary → tiles within ~3 of boundary = ShallowWater
  - Tiles further in = DeepWater
  - **Harbour exception**: where the gradient drops steeply (steep coast detected by local height variance), allow occasional Ocean-to-land adjacency with no Coastal buffer.
- Land elevation tiers controlled by `hillAndMountainRatio`:
  - Low land band → Flat
  - Medium band → Hilly
  - High band → Mountainous
- Second noise layer at coarser scale (ridged noise) biases elevation to produce mountain *ranges* (ridgelines) rather than scattered random peaks.

**Wrapping**: use `TileableFbm` when `horizontalWrap` or `verticalWrap` is true.

Output: colour-coded PNG (blue=DeepWater, teal=ShallowWater, green=Flat, yellow=Hilly, red=Mountainous) + CSV.

**Verification**: Maps should show continent shapes with island chains and contiguous mountain ranges, not circular landmasses.

---

### Step 3 — Campaign Map Biome Assignment
*Goal: assign BiomeType to every land and water tile.*

Two additional noise passes (same master seed, distinct offsets so they are uncorrelated):
- **Temperature**: base value from latitude (0 at poles → 1 at equator); add noise variation (~30% weight) to break the strict banding. If `equator=true`, concentrate the hot band near the centre row.
- **Moisture**: independent noise pass, no latitude bias.

**Whittaker lookup** (temperature × moisture → BiomeType):

| Temperature \ Moisture | Dry     | Moderate  | Wet    |
|------------------------|---------|-----------|--------|
| Cold                   | Ice     | Tundra    | Tundra |
| Cool                   | Tundra  | Grassland | Forest |
| Warm                   | Arid    | Woodland  | Forest |
| Hot                    | Desert  | Arid      | Jungle |

Water tiles: ShallowWater → Coastal; DeepWater → Ocean.

**Biome sticky clustering**: 1–2 pass cellular automaton nudges isolated single-tile outliers toward their majority neighbour, producing natural-looking regions rather than speckle.

**Transition smoothing**: enforce the adjacency rules (see Design Questions section). Replace violating tiles via BFS from their nearest compatible neighbour.

**Respect domain model**: only assign Hilly/Mountainous to biomes where `CampaignBiome.CanBeElevated = true`. Reclassify any incompatible pairings to Flat.

Output: biome-colour PNG.

**Verification**: Grouped biome regions; polar ice if `poles=true`; more Jungle near equator if `equator=true`; no hard Forest–Desert boundaries; occasional Ocean-to-land harbour edges.

---

### Step 4 — Wire into Application (CampaignMap only)
*Goal: make the generator create real database rows.*

1. **`GeneratePerlinNoiseCampaignMap.cs`** in `AscendantEmpire.Application/Features/Maps/MapGallery/Handlers/` implementing `ICampaignMapGenerator`. Reads and validates PropertiesJson; runs Steps 2 + 3 fully in memory; bulk-inserts into `mapgallery.CampaignMapGallery` using the same SQL pattern as `GenerateBlankCampaignMap.cs`; writes the resolved seed back to `PropertiesJson` on the index record before returning.
2. **Fill the stub** in `CreateCampaignMapHandler.cs` line 83–85 to instantiate `GeneratePerlinNoiseCampaignMap` when `CreationEngine == MapCreationEngine.PerlinNoiseV1`.
3. No migration needed — `CampaignMapGallery` schema already exists.

**Verification**: Create a map through the existing Map Gallery UI with engine = PerlinNoiseV1. Confirm tiles appear in the DB with correct biome and geography values. Confirm the seed was written back to `PropertiesJson`.

---

### Step 5 — BattleMap Schema
*Goal: define the new DB entities for BattleMaps.*

New domain entities in `AscendantEmpire.Domain/Entities/Maps/BattleMaps/`:

- **`BattleMapGalleryIndex.cs`**
  - `Id` (int, PK)
  - `CampaignMapGalleryIndexId` (int, FK)
  - `CampaignTileX`, `CampaignTileY` (int) — which campaign tile this covers
  - `Width`, `Height` (int, always equal — square)
  - `GenerationSeed` (long) — the derived local seed used for this tile
  - `GenerationStatus` (MapCreationStatus enum)

- **`BattleMapGallery.cs`**
  - `BattleMapGalleryId` (int, PK, `DatabaseGeneratedOption.None`)
  - `BattleMapGalleryIndexId` (int, FK)
  - `XCoord`, `YCoord` (int)
  - `AltitudeLevel` (int: -1 to 4, matches BattleMapAltitude enum)
  - `BiomeTypeId` (FK to `map.CampaignBiomes`)

New enum in `AscendantEmpire.Domain/Enums/Maps/`:
- **`BattleMapAltitude.cs`**: `BelowWater = -1, Ground = 0, Low = 1, Medium = 2, High = 3, Peak = 4`

EF Core migration: `ME-10_AddBattleMapGalleryTables`. Tables in `mapgallery` schema. Register new DbSets in `AscendantEmpireContext`.

**Verification**: `dotnet ef migrations add` succeeds; `Update-Database` creates the tables.

---

### Step 6 — BattleMap Generation (on demand)
*Goal: given a campaign tile (X, Y), generate a 50×50 BattleMap with seamless edges.*

**Derived seed**: `localSeed = masterSeed ^ (tileX * 2654435761L) ^ (tileY * 2246822519L)`.

**Edge continuity**: the BattleMap noise is sampled from the *same underlying Perlin field* as the Campaign Map, just zoomed in. BattleMap tile (camX, camY) samples the noise domain at `[camX * battleMapSize + bx, camY * battleMapSize + by]`. Because the Perlin field is continuous, edge pixels of adjacent BattleMaps are evaluated at adjacent coordinates and match exactly.

**Altitude heightmap**:
- fBm at fine scale over the 50×50 region
- Domain-warped to avoid flat featureless areas
- Quantise to BattleMapAltitude enum using thresholds driven by the campaign tile's `CampaignMapGeographyType`:
  - Flat → thresholds favour Ground with occasional Low, rare Medium+
  - Hilly → thresholds favour Low–Medium with occasional High
- Neighbour campaign tile geography bleeds into edges: if a neighbour is Mountainous, the 5–10 columns/rows nearest that edge are biased upward (gradient ramp added to the heightmap at that edge). This means hilly tiles next to mountains have pronounced altitude at the mountain-facing edge.
- Adjacent tiles can differ by multiple altitude levels (representing cliff faces — intentional).

**BattleMap biome diversity**:
- Base = campaign tile's BiomeType
- A fine-scale moisture/temperature noise pass introduces small patches of adjacent-compatible biomes within the tile (e.g., a Grassland tile may have Woodland patches)
- BelowWater altitude tiles always get Coastal or Ocean biome

**Lazy handler**: `GenerateBattleMapHandler` receives `(CampaignMapGalleryIndexId, TileX, TileY)`. Reads master seed from `PropertiesJson`, derives local seed, generates and inserts into `BattleMapGallery` only if not already present.

**Verification** (in `MapGenerationPlay` first):
- Generate two adjacent BattleMaps and verify their shared edge values match.
- Verify Flat campaign tiles produce mostly Ground altitude; Hilly tiles have varied altitude.
- Verify the mountain-neighbour edge bias works.
- Output as PNG.

---

## Critical Files

| Purpose | Path |
|---------|------|
| Noise engine (new) | `MapGenerationPlay/PerlinNoise.cs`, `FractalBrownianMotion.cs`, `RidgedNoise.cs`, `TileableFbm.cs`, `DomainWarpedNoise.cs` |
| Campaign generator (new) | `AscendantEmpire.Application/Features/Maps/MapGallery/Handlers/GeneratePerlinNoiseCampaignMap.cs` |
| Stub to fill | `AscendantEmpire.Application/Features/Maps/MapGallery/Handlers/CreateCampaignMapHandler.cs:83` |
| Blank map reference | `AscendantEmpire.Application/Features/Maps/MapGallery/Handlers/GenerateBlankCampaignMap.cs` |
| Generator interface | `AscendantEmpire.Application/Features/Maps/MapGallery/Handlers/ICampaignMapGenerator.cs` |
| BattleMap entities (new) | `AscendantEmpire.Domain/Entities/Maps/BattleMaps/BattleMapGalleryIndex.cs`, `BattleMapGallery.cs` |
| BattleMap altitude enum (new) | `AscendantEmpire.Domain/Enums/Maps/BattleMapAltitude.cs` |
| DB context | `AscendantEmpire.Application/Data/AscendantEmpireContext.cs` |
| Biome properties | `AscendantEmpire.Domain/Entities/Maps/CampaignMaps/CampaignBiome.cs` |
| Geography enum | `AscendantEmpire.Domain/Enums/Maps/CampaignMapGeographyType.cs` |
| Biome enum | `AscendantEmpire.Domain/Enums/Maps/BiomeType.cs` |

---

## Execution Order Summary

| Step | Scope | Risk | Output |
|------|-------|------|--------|
| 1 — Noise engine | MapGenerationPlay only | None | Heightmap PNGs |
| 2 — Campaign geography | MapGenerationPlay only | None | Colour-coded geography PNGs |
| 3 — Biome assignment | MapGenerationPlay only | None | Biome-colour PNGs |
| 4 — Wire into Application | Application code + DB insert | Low (no schema change) | Real DB rows visible in Map Editor |
| 5 — BattleMap schema | Migration + new entities | Low | New DB tables |
| 6 — BattleMap generation | Application code + DB insert | Low | BattleMaps in DB, edge continuity verified |
