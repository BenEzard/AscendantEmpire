# Food yield

Part of the [Campaign Map](README.md) design.

---

# 1. Conceptual overview

Food yield is **how much food a tile offers before anything is taken from it**. The biome sets a base
for each renewable food source (Forage, Hunt, Fish, Fodder) — see the Biome section of
[tiles.md](tiles.md) — and a Player's own modifiers raise or lower that base into their **effective
value**. What is left of that value once the tile's shared `Taken` amount is removed is what the
Player can actually eat, which is [depletion.md](depletion.md). Regeneration decides how fast the
tile comes back, which is [replenishment.md](replenishment.md).

So there are three separate questions, and this document covers the first:

1. **How much is there** — the biome's base, plus yield bonuses (this document).
2. **How much is left** — base and modifiers, less what anyone has taken ([depletion.md](depletion.md)).
3. **How fast it returns** — Regeneration Percent, once per round ([replenishment.md](replenishment.md)).

## Regeneration and farming bonuses

Regeneration speed depends on climate: the biome's Regeneration Percent, the same for all renewable food sources on the tile. Only a claimant's Settlement-scoped modifiers raise it (see [Replenishment](replenishment.md)). Terrain improvements and equipment add yield:

- Terrain improvement **Irrigation**: +50% yield.
- Terrain improvement **Farm (with crop)**: +300% yield.

Equipment bonuses chain by category, choosing the best available option in each category (e.g.
machinery is picked over a plough animal). One illustrative chain:

```
Machinery (100%)        / plough animal (50%)
  + hoe (20%)           / adze (10%)
  + chemical fertilizer (30%) / animal fertilizer (15%)
  + pesticides (20%)
```

So a farm with machinery, plough animal, hoe, animal fertilizer, and pesticides yields
`100% + 20% + 15% + 20% = 155%` (the plough animal is excluded because machinery wins its category).

**Productivity rate** is how much food one individual can collect:

| Action | Productivity Rate | Notes |
| --- | --- | --- |
| FORAGE | 1.05 | One person collects enough food for 1.05 people. |

## Tile bonuses

Naturally occurring tile bonuses can also boost food:

| Bonus | Occurs on | Imparts |
| --- | --- | --- |
| Oasis | Desert | +100% food |
| Reef | Coastal | +100% food |

## Formula for units which consume (and expand population)

```
Food Harvested = ROUNDUP( IF( People × Productivity Rate < Starting Food,
                              People × Productivity Rate,
                              Starting Food ), 0 )

Food Remaining on Tile (end of turn) = ROUNDUP( IF( People − Food Harvested > 0,
                                                    People − Food Harvested,
                                                    People × 0.25 ), 0 )

Remaining Energy (of population after collecting food) = People after growth × (1 − Energy to extract food)
```

---

# 2. Technical detail

Of the yield mechanics above, the parts with code today are the **biome's base values**, its
**Regeneration Percent**, and the **per-Player modifiers** that turn a base into an effective value.
Terrain improvements, equipment chains, productivity rates, tile bonuses and the population formula
have no schema, code or seed data yet — see [Not yet implemented](#not-yet-implemented).

## Database tables

Every base value and flat modifier is stored as a `bigint` scaled by `ScaledAmount.Scale` (1,000), so
a value of `400` is held as `400000`. Modifier percentages use `ScaledPercent.Scale` (1,000 = 100%).
Regeneration Percent is the exception: a plain `decimal(18,3)` fraction, so `0.25` is 25%.

| Schema | Table | Purpose |
| --- | --- | --- |
| `map` | `CampaignBiomes` | The biome catalogue: the base yield of each resource in the biome, and the share that regrows each round. Global, authored data — the same in every game. |
| `game` | `PlayerSustenanceModifier` | One modifier to one Player's yield for one resource, in one biome or in all of them. Per game and per Player. |
| `game` | `CampaignMap` | The tile. Names the biome (`BiomeTypeId`) whose values apply, and the geography that decides whether it is water. |

### `map.CampaignBiomes` — the columns this feature uses

| Column | Type | Purpose |
| --- | --- | --- |
| `BiomeTypeId` | `int`, PK (`BiomeType` enum) | The biome. |
| `Name` | `nvarchar(50)` | Display name. |
| `IsWater` | `bit` | Decides which resources are in a tile's food pool: Fish on water, Forage and Hunt on land. |
| `BaseForageValue` | `bigint`, nullable | Base Forage yield. Null means the biome does not have the resource at all, which no modifier can change. |
| `BaseHuntValue` | `bigint`, nullable | Base Hunt yield. Null as above. |
| `BaseFishValue` | `bigint`, nullable | Base Fish yield. Null as above. |
| `BaseFodderValue` | `bigint`, nullable | Base Fodder yield — for animals, and nothing eats it yet. Null as above. |
| `RegenerationPercent` | `decimal(18,3)`, nullable | The share of each base value that regrows at the end of every round, as a fraction. The same share for all four resources. |
| `BaseBoneValue`, `BaseWoodValue`, `BaseStoneValue` | `bigint`, nullable | The material yields. Static and the same for every Player — no modifier applies to them, so they are outside this feature. |

`RegenerationPercent` is required, more than `0` and at most `1`, whenever the biome has any
sustenance base value; and must be null when it has none, since there is nothing to regrow. Both
rules are enforced by `CampaignMapBiomeValidator`. A biome that is not paintable on the campaign map
must have no Regeneration Percent at all.

Renamed from `RegenerationRate` by migration
`20260918113502_CM-43_RenameRegenerationRateToRegenerationPercent`; the base value columns were
widened to `bigint` and rescaled to thousandths by `20260613204637_ScaleBiomeBaseValuesToBigint` and
`20260613210205_RescaleBiomeBaseValuesTo1000`.

### `game.PlayerSustenanceModifier` — the columns this feature uses

| Column | Type | Purpose |
| --- | --- | --- |
| `Id` | `int`, PK | Surrogate key: the natural key includes the nullable `BiomeTypeId`, so it cannot be the primary key. |
| `GameId` | `int` | The Game. Implied by the Player, but carried so the game delete can purge these rows directly. |
| `PlayerId` | `int` | The Player whose yield is modified. |
| `SourceType` | `int` (`ModifierSourceType`: Technology, Specialist, Department) | What kind of thing granted the modifier, so every row it granted can be removed together when it is lost. |
| `SourceId` | `int` | The particular Technology, Specialist or Department. Not a foreign key — none of the three is modelled yet. |
| `Resource` | `int` (`SustenanceResource`) | Which of Forage, Hunt, Fish, Fodder is modified. |
| `BiomeTypeId` | `int`, nullable (`BiomeType`) | The biome affected, or null for every biome. |
| `FlatValue` | `bigint` | A flat amount added to the value. Negative for a penalty; `0` when the modifier has no flat part. |
| `PercentValue` | `bigint` | A percentage of the base added to the value. Negative for a penalty; `0` when the modifier has no percentage part. |

- Check constraint `CK_PlayerSustenanceModifier_HasValue` — a row must carry a flat part, a percentage
  part, or both.
- Unique index `UX_PlayerSustenanceModifier_Player_Source_Resource_Biome` — one row per Player,
  source, resource and biome.
- Index `IX_PlayerSustenanceModifier_GameId` — the game delete's filter.

## Key code files

### The yield formula

| File | Role |
| --- | --- |
| [SustenanceValueCalculator.cs](../../../AscendantEmpire.Application/Features/Maps/Biomes/Services/SustenanceValueCalculator.cs) | The one place the formula lives: `Effective = Base + Base × ΣPercent + ΣFlat`, with the summed percentage floored at −100% and the result floored at 0. Also `Available = MAX(0, Effective − Taken)`. Pure. |
| [PlayerSustenanceValueReader.cs](../../../AscendantEmpire.Application/Features/Maps/Biomes/Services/PlayerSustenanceValueReader.cs) | Reads every biome's bases and a Player's modifiers, and produces the Player's effective value for each biome and resource. |
| [PlayerSustenanceValues.cs](../../../AscendantEmpire.Application/Features/Maps/Biomes/Classes/PlayerSustenanceValues.cs) | The result: an effective value per (biome, resource), or null where the biome does not have the resource. |
| [BiomeRegenerationValueReader.cs](../../../AscendantEmpire.Application/Features/Maps/Depletion/Services/BiomeRegenerationValueReader.cs) | Reads each biome's bases and Regeneration Percent, for the end-of-round regrowth and the support forecast. |
| [ScaledAmount.cs](../../../AscendantEmpire.Application/Helpers/ScaledNumerics/ScaledAmount.cs) · [ScaledPercent.cs](../../../AscendantEmpire.Application/Helpers/ScaledNumerics/ScaledPercent.cs) · [ScaledResourceValue.cs](../../../AscendantEmpire.Application/Helpers/ScaledNumerics/ScaledResourceValue.cs) | Integer arithmetic in thousandths, and the conversion between a whole number as a user types it and the scaled value stored. |

### Domain

| File | Role |
| --- | --- |
| [CampaignBiome.cs](../../../AscendantEmpire.Domain/Entities/Maps/CampaignMaps/CampaignBiome.cs) | The biome row, including the base yields and `RegenerationPercent`. |
| [PlayerSustenanceModifier.cs](../../../AscendantEmpire.Domain/Entities/Games/PlayerSustenanceModifier.cs) | One modifier row, with the reasoning for deriving the effective value rather than storing it. |
| [SustenanceResource.cs](../../../AscendantEmpire.Domain/Enums/Resources/SustenanceResource.cs) | The four modifiable resources — the material yields are deliberately absent. |
| [ModifierSourceType.cs](../../../AscendantEmpire.Domain/Enums/Modifiers/ModifierSourceType.cs) | Technology / Specialist / Department. |

### Authoring and seeding the biome catalogue

| File | Role |
| --- | --- |
| [CampaignMapBiomeValidator.cs](../../../AscendantEmpire.Application/Features/Maps/Biomes/Validators/CampaignMapBiomeValidator.cs) | The rules tying `RegenerationPercent` to having sustenance, and to being paintable. |
| [AddCampaignMapBiomesHandler.cs](../../../AscendantEmpire.Application/Features/Maps/Biomes/Handlers/AddCampaignMapBiomesHandler.cs) · [UpsertCampaignMapBiomesHandler.cs](../../../AscendantEmpire.Application/Features/Maps/Biomes/Handlers/UpsertCampaignMapBiomesHandler.cs) · [GetCampaignMapBiomesHandler.cs](../../../AscendantEmpire.Application/Features/Maps/Biomes/Handlers/GetCampaignMapBiomesHandler.cs) | Write and read the catalogue. The writers scale the whole numbers a user enters by 1,000 through `ScaledResourceValue.ToRaw`. |
| [BiomeSeedData.cs](../../../AscendantEmpire.Application/Features/DatabaseSeeding/SeedData/BiomeSeedData.cs) | The initial `map.CampaignBiomes` values applied by the database seed, held as plain whole numbers and scaled by the upsert handler. |
| [MapExportExecutor.cs](../../../AscendantEmpire.Application/Features/Maps/MapGallery/Executors/MapExportExecutor.cs) · [MapImportExecutor.cs](../../../AscendantEmpire.Application/Features/Maps/MapGallery/Executors/MapImportExecutor.cs) | Carry the biome yield values in and out with an exported map. |

### API

| Endpoint | Action | Purpose |
| --- | --- | --- |
| `GET biomes/campaign-map` | `GetCampaignMapBiomesAsync` | Read the biome catalogue, yields included. |
| `POST biomes/campaign-map` | `AddCampaignMapBiomesAsync` | Add biomes. |
| `PUT biomes/campaign-map` | `UpsertCampaignMapBiomesAsync` | Upsert biomes — how yields are edited. |

All three are on [BiomesController.cs](../../../AscendantEmpire.Api/Controllers/BiomesController.cs).
A Player's own effective values are never returned as such; they reach the client only as the amounts
available on a tile, through `GET campaign-map/player/tiles/{x}/{y}/sustenance` — see
[depletion.md](depletion.md).

### Frontend

| File | Role |
| --- | --- |
| [Biome.ts](../../../AscendantEmpire.AscendantEmpireUI/src/types/map/Biome.ts) | The biome contract, including `regenerationPercent` and each `base*Value`. |
| [biomesApi.ts](../../../AscendantEmpire.AscendantEmpireUI/src/api/biomesApi.ts) · [useCampaignMapBiomes.ts](../../../AscendantEmpire.AscendantEmpireUI/src/hooks/useCampaignMapBiomes.ts) | The catalogue reads and writes, shared by the game, the Map Editor and the Entity Setup Tool. |
| [BiomeManagerDialog.tsx](../../../AscendantEmpire.AscendantEmpireUI/src/features/entitysetuptool/components/BiomeManagerDialog.tsx) | Where a designer edits each biome's yields and Regeneration Percent, in the Entity Setup Tool. |
| [BiomeListDialog.tsx](../../../AscendantEmpire.AscendantEmpireUI/src/features/mapgallery/components/BiomeListDialog.tsx) | The Map Editor's read-only table of the catalogue, for looking up what a biome yields while drawing a map. |
| [regenerationPercent.ts](../../../AscendantEmpire.AscendantEmpireUI/src/util/regenerationPercent.ts) | Converts between the percentage a designer types and the fraction stored. |

### Tests

`AscendantEmpire.Tests/Features/Maps/Biomes/` covers the formula
(`SustenanceValueCalculatorTests`), the reader (`PlayerSustenanceValueReaderTests`), the biome rules
(`CampaignMapBiomeValidatorTests`) and the modifier constraints
(`PlayerSustenanceModifierConstraintTests`).

## Process flow

### Authoring a biome's yield

1. A designer edits the biome in the Entity Setup Tool's `BiomeManagerDialog`, entering base values as
   plain whole numbers and Regeneration Percent as a percentage.
2. `PUT biomes/campaign-map` reaches `UpsertCampaignMapBiomesHandler`, which runs
   `CampaignMapBiomeValidator` — including the rules tying `RegenerationPercent` to having sustenance.
3. The handler scales each base value by 1,000 (`ScaledResourceValue.ToRaw`) and writes the row.
   `RegenerationPercent` is stored as the fraction it is.
4. `BiomeSeedData` applies the same values through the same idempotent upsert when the database is
   seeded, so seeded and hand-edited biomes cannot diverge in scale.

### Reading a Player's yield

1. A caller that needs a Player's yields — the end-of-turn feed, the tile toolbar, the support
   indicator — asks `IPlayerSustenanceValueReader` once, on the same `DbContext`.
2. It reads every biome's four sustenance bases, and every modifier belonging to that Player. A Player
   holds few enough modifiers that they are summed in memory, which also keeps the "all biomes" rows
   (a null `BiomeTypeId`) simple: one row counts towards every biome.
3. Per (biome, resource) it sums the applicable `FlatValue` and `PercentValue` and calls
   `SustenanceValueCalculator.CalculateEffectiveValue`:
   - a null base stays null — a resource the biome does not have can never be given one;
   - the summed percentage is floored at −100% and applies to the base alone, so percentages add
     rather than compound and never scale the flat part;
   - the result is floored at 0.
4. Nothing is stored. Losing a source is just deleting its rows by (`SourceType`, `SourceId`), and the
   next read produces the new value — there is no running total to unwind.

### Yield in play

1. What a Player can eat on a tile is their effective value less the tile's shared `Taken` amount:
   `MAX(0, Effective − Taken)` — [depletion.md](depletion.md).
2. How much comes back each round is `RegenerationPercent × Base`, where `Base` is the biome's value
   on unclaimed land — [replenishment.md](replenishment.md).
3. How long a tile can feed a set of Forces plays those two forward together —
   [support-indicator.md](support-indicator.md).

## Not yet implemented

Nothing in the codebase yet corresponds to:

- **Terrain improvements** — Irrigation (+50%) and Farm with crop (+300%). There is no terrain
  improvement entity, table or enum; see [infrastructure.md](infrastructure.md).
- **Equipment bonus chains** — best-in-category selection across machinery, plough animals, hoes,
  fertilizers and pesticides.
- **Productivity rate** — the food one individual can collect per Action (FORAGE at 1.05).
- **Tile bonuses** — Oasis on desert and Reef on coastal, each +100% food.
- **The population formula** — food harvested, food remaining and remaining energy for units that
  consume and expand population. These wait on Settlements.
- **Settlement-scoped modifiers**, including the regeneration modifiers that raise a claimed tile's
  Regeneration Percent. `PlayerSustenanceModifier` has no `SettlementId` scope yet.
- **Sources of modifiers** — Technology, Specialist and Department are named by `ModifierSourceType`
  but none is modelled, so `SourceId` carries no foreign key and nothing grants a modifier in play
  today.
