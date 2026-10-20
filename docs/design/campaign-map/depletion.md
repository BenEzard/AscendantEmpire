# Depletion of resources

Part of the [Campaign Map](README.md) design. How a depleted tile regrows is in
[replenishment.md](replenishment.md).

---

# 1. Conceptual overview

A unit moving onto or through a tile depletes some of its resources (typically via scavenging),
scaled by unit size. Enough scavenging — too large a unit, too many units passing through, or units
lingering — drives the available amount to **0**.

The purpose of these mechanics:

- Force **nomadic behaviour** early-game (until food technology improves, or a food-rich location
  is found).
- Make **supply chains** more important (especially after the early game).
- Make **raiding** of settlements more likely than conquering in the early game.
- Add pressure on both the besieged and the besieging.
- Make "Stacks of Death" less likely, or harder to supply.
- Enable **scorched earth, salting, and cavalry rushes** (à la the Mongols).

## Tracking

A tile's depletion state is held in `game.CampaignMapDepletion`, one row per tile that has been fed
on, keyed by the game tile's `CampaignMapId`. The row is created the first time a Force feeds on the
tile and persists between turns; a tile with no row has had nothing taken. A Player's view joins to
it on `PlayerCampaignMap.CampaignMapId`.

> TODO: Tiles within a settlement zone also need to be found for replenishment, and wait on
> Settlements.

## What a tile stores: the amount taken

A tile's depletion state is **Player-neutral**. For each sustenance resource (Forage, Hunt, Fish,
Fodder) the tile stores only **how much has been taken** from it, `Taken` (never below `0`), plus
`FedOnRound`, the last round anyone **fed on it** (see [Replenishment](replenishment.md)). A tile was
fed on this round when `FedOnRound` is the current round number, so the marker never needs resetting. It does not store an
amount remaining, because the amount remaining is different for every Player.

Each Player sees, and can extract, only what is left of **their own** effective value:

```
Available to a Player = MAX(0, [Player's Effective Value] − Taken)
```

- **Feeding** adds what was extracted to `Taken`. A Player can never extract more than is
  available to them.
- **Modifier changes** need no reconciliation. Effective values are read live, so a Player who
  gains or loses a modifier simply sees a different amount against the same `Taken`.
- **No Player is recorded** on the tile, and there is no hand-over calculation when a different
  Player arrives: every Player reads the same `Taken`.
- **Shown only on tiles in live sight.** The tile toolbar shows a Player what is available to them
  on the selected tile (`GET campaign-map/player/tiles/{x}/{y}/sustenance`), but only while one of
  their Observers can see it. `Taken` grows whenever anyone feeds, so disclosing it on an unwatched
  tile would reveal where other Players' Forces have been. The toolbar shows each resource as
  `MAX(0, effective − Taken)`, and a resource the biome does not have is left out.

A Player with a higher value can therefore reach food a weaker Player cannot. Once a weaker Player
has stripped a tile, a stronger Player still finds their surplus on it.

> **Why an absolute amount rather than a percentage.** Storing a fraction remaining
> (`fraction × own value`) would make a stronger Player's advantage "more yield from the same
> animals", and a tile emptied by anyone would be empty for everyone. Storing the amount taken gives
> the stronger Player a reserve the weaker Player cannot touch, which is the advantage tech is meant
> to buy. Converting a Player's *view* into another Player's view (rather than storing a neutral
> amount) is order-dependent and leaks food on every hand-over, so it is avoided.

## Scenario — a unit moves onto a tile

Suppose Empire A has tech improvements raising their Grassland Hunt to 200, and moves a 100-man unit
onto an untouched grassland tile. The tile's `Taken` is `0` for every resource, so Empire A sees:

| | Forage | Hunt | Fish | Fodder |
| --- | --- | --- | --- | --- |
| Available to Empire A | 400 | 200 | 0 | 1000 |

At end of turn, the unit needs 100 food (100 men). On land the pool is Forage and Hunt — and Fish too
where a river runs along one of the tile's edges, see [food-yield.md](food-yield.md#rivers); on water
it is Fish alone. Fodder is for animals and is not eaten by men. The pool available is `400 + 200 = 600`,
and the need is split in proportion to what each resource has, so `2/3 × 100 = 66.667` is taken from
Forage and `1/3 × 100 = 33.333` from Hunt. The split is in thousandths, with any thousandth left over
by rounding going to the resource whose share lost the most, so the amounts taken always sum exactly
to the need:

| | Forage | Hunt | Fish | Fodder |
| --- | --- | --- | --- | --- |
| Taken (stored on the tile) | 66.667 | 33.333 | 0 | 0 |
| Available to Empire A | 333.333 | 166.667 | 0 | 1000 |
| Available to a Player on base values (Hunt 100) | 333.333 | 66.667 | 0 | 1000 |

Had the pool held less than 100, the unit would take everything available and go short by the rest.

## Depletion rules

- Units and settlement populations are fed at the **end of their turn**.
- A hostile unit within a settlement's controlled area **gathers the resources and the settlement
  gets nothing** — even if the unit only takes 5%, it blocks the settlement from collecting the
  rest.
- A unit **passing through** a tile (not ending its turn there) consumes **20%** of its food needs
  from that tile, leaving less to deduct on the tile it ends on.
- There is a maximum a unit can harvest per tile (`UNIT_HARVEST_CAP`), regardless of availability.
- **Pillaging** a tile (consuming the unit's turn) harvests more than `UNIT_HARVEST_CAP` allows,
  feeding the unit for multiple turns and depleting the tile.
- **Scorched earth** sets all resource quantities on the tile to `0`. The unit on the tile is fed
  for that turn, but the extra resources are *not* added to the unit's supply.

> TODO: Decide how scorched earth sets `Taken`. To leave the tile bare for every Player it must be at
> least the highest effective value any Player has for that tile.

**Consumption per Unit (within a Force):** if `[Food Required] > 0`,
`Σ([Number of individuals] × [Food requirements of Unit Base])`.

**Force gathering bonuses** (e.g. a forager specialist attached to a Command) reduce the food the
Force draws from the tile. They do **not** raise the tile's maximum. For example, a 100-man Force
with a forager worth 10 needs only 90 food from the tile.

## End-of-turn calculation

- Pull tiles that recently had a unit, or are within a settlement zone, from the index table.
- Add up all Action bonuses for the empire (e.g. FORAGE, HUNTING).
- For each unit, calculate food required (based on individuals and the food requirement of their
  base, plus attached specialists). A man = 1 food; a mounted man = 1 food + 2 fodder.
- Add each Force's extraction to the tile's `Taken`, and mark the tile as fed on this round.

Today this is the end-of-turn `Gathering` process. It feeds the Player's Forces that are not in a
Battle (so only one side ever feeds from a tile), at 1 troop = 1 food, from the tile each ends its
turn on. The Forces on a tile feed from one pool, and a shortfall is spread across them in proportion
to their troops. Each Force's need, food and shortfall is handed to `IUnderfedForceResolver`, the seam
for the consequences of going hungry, which kills and wounds troops in proportion to the shortfall
— see [Food](../units/unit-support-and-supply.md#food).

At the end of the **round** (after every Player's turn), the `RegenerateTileSustenance` process
replenishes each depleted tile as described in [Replenishment](replenishment.md).

## UI implications

- If a unit is on a tile that won't feed it, the player must be **alerted** (to move it, or at
  least warned if it cannot move).
- If a unit attempts to move into a tile that won't have enough food, the player should be warned.

How long a tile can feed a set of Forces is shown by the [support indicator](support-indicator.md).

---

# 2. Technical detail

## Database tables

Every amount — base values, modifiers, `Taken` — is stored as a `bigint` scaled by
`ScaledAmount.Scale` (1,000), so a value of `400` is held as `400000` and a thousandth is the finest
step. Percentages use `ScaledPercent.Scale` (1,000 = 100%).

| Schema | Table | Purpose |
| --- | --- | --- |
| `game` | `CampaignMapDepletion` | The feature's own state: what has been taken from a tile, by anyone, and the last round it was fed on. |
| `game` | `CampaignMap` | The Player-neutral tile. Read for its biome and geography, which decide what is in the tile's food pool. |
| `game` | `PlayerCampaignMap` | The Player's view of the tile. What a Player is told about a tile joins through here. |
| `game` | `PlayerSustenanceModifier` | The modifiers that make one Player's effective value differ from another's on the same biome. |
| `map` | `CampaignBiomes` | The biome catalogue: each biome's base sustenance values and its Regeneration Percent. |
| `units` | `UnitInstances` | The Forces. Read for who is on which tile and how many troops need feeding; written when starvation costs troops. |

### `game.CampaignMapDepletion`

One row per tile that has ever been fed on, created on the first feed and kept thereafter — a row
regrown back to `0` is reused the next time a Force feeds there. No navigation properties: the rows
are read and written by tile id, never walked as an entity graph.

| Column | Type | Purpose |
| --- | --- | --- |
| `CampaignMapId` | `int`, PK, not generated | The tile. A tile has at most one depletion row, so the tile id is the key. |
| `GameId` | `int` | The Game. Implied by the tile, but carried so the game delete can purge these rows directly. |
| `ForageTaken` | `bigint`, default `0` | Forage taken from the tile, by anyone. Never negative. |
| `HuntTaken` | `bigint`, default `0` | Hunt taken from the tile, by anyone. Never negative. |
| `FishTaken` | `bigint`, default `0` | Fish taken from the tile, by anyone. Never negative. |
| `FodderTaken` | `bigint`, default `0` | Fodder taken from the tile. Always `0` today: nothing eats fodder until mounted units have food requirements. |
| `FedOnRound` | `int`, nullable | The last round a Force fed on the tile, or null when none ever has. Records presence, not food taken. |

- Check constraint `CK_CampaignMapDepletion_TakenNonNegative` — all four `*Taken` columns `>= 0`.
  Extraction can never take more than is available, so a negative amount could only be a defect.
- Index `IX_CampaignMapDepletion_GameId` — the game delete's filter.
- Foreign keys `FK_CampaignMapDepletion_CampaignMap_CampaignMapId` and
  `FK_CampaignMapDepletion_GameIndex_GameId`, both `NoAction`: `GameDeleteExecutor` removes these
  rows in its own hand-ordered step.
- Created by migration `20260916091723_CM-37_AddCampaignMapDepletion`.

### Columns read from the other tables

| Table | Columns used | Read for |
| --- | --- | --- |
| `game.CampaignMap` | `CampaignMapId`, `GameId`, `XCoord`, `YCoord`, `BiomeTypeId`, `CampaignMapGeographyTypeId` | The tile's biome (which base values apply) and whether it is water (which resources are in its pool). |
| `map.CampaignBiomes` | `BiomeTypeId`, `IsWater`, `BaseForageValue`, `BaseHuntValue`, `BaseFishValue`, `BaseFodderValue`, `RegenerationPercent` | The base values a Player's modifiers are applied to, and the share that regrows each round. A null base means the resource does not exist in the biome. |
| `game.PlayerSustenanceModifier` | `PlayerId`, `Resource`, `BiomeTypeId` (null = every biome), `FlatValue`, `PercentValue` | Summing a Player's modifiers into their effective value. |
| `units.UnitInstances` | `Id`, `GameId`, `PlayerId`, `CampaignMapTileId`, `BattleMapTileId`, `HealthyCount`, `InjuredCount` | Which Forces feed, where, and how much they need. The troop counts are written back by starvation. |

## Key code files

### Domain

| File | Role |
| --- | --- |
| [CampaignMapDepletion.cs](../../../AscendantEmpire.Domain/Entities/Games/CampaignMapDepletion.cs) | The depletion row. |
| [PlayerSustenanceModifier.cs](../../../AscendantEmpire.Domain/Entities/Games/PlayerSustenanceModifier.cs) | One modifier to one of a Player's sustenance values. |
| [SustenanceResource.cs](../../../AscendantEmpire.Domain/Enums/Maps/SustenanceResource.cs) | Forage, Hunt, Fish, Fodder — the four resources a Player can modify. |
| [TileSupportLevel.cs](../../../AscendantEmpire.Domain/Enums/Maps/TileSupportLevel.cs) | Plenty / OneTurn / Insufficient / Exhausted. |

### Application — the depletion arithmetic

Pure and database-free apart from the two readers, so the rules are tested on their own.

| File | Role |
| --- | --- |
| [DepletionTaken.cs](../../../AscendantEmpire.Application/Features/Maps/Depletion/Services/DepletionTaken.cs) | The one place a `SustenanceResource` is mapped to its `*Taken` column, so writers and readers cannot disagree. |
| [TileFoodPool.cs](../../../AscendantEmpire.Application/Features/Maps/Depletion/Services/TileFoodPool.cs) | Builds the tile's pool: Forage and Hunt on land (Fish as well on a riverbank), Fish on water, each at `MAX(0, effective − Taken)` with the tile's river bonus added to the effective value. |
| [FoodExtractionCalculator.cs](../../../AscendantEmpire.Application/Features/Maps/Depletion/Services/FoodExtractionCalculator.cs) | Splits a food need across the pool, or strips it bare and reports the shortfall. |
| [ShortfallAllocator.cs](../../../AscendantEmpire.Application/Features/Maps/Depletion/Services/ShortfallAllocator.cs) | Spreads a tile's pooled shortfall across its Forces in proportion to troops. |
| [ProportionalSplitter.cs](../../../AscendantEmpire.Application/Features/Maps/Depletion/Services/ProportionalSplitter.cs) | Largest-remainder split in thousandths, used for both splits above, so the parts sum exactly to the total. |
| [TileRegenerationCalculator.cs](../../../AscendantEmpire.Application/Features/Maps/Depletion/Services/TileRegenerationCalculator.cs) | The two end-of-round regrowth rules — see [replenishment.md](replenishment.md). |
| [SustenanceValueCalculator.cs](../../../AscendantEmpire.Application/Features/Maps/Biomes/Services/SustenanceValueCalculator.cs) | `Effective = Base + Base × ΣPercent + ΣFlat`, and `Available = MAX(0, Effective − Taken)`. |
| [PlayerSustenanceValueReader.cs](../../../AscendantEmpire.Application/Features/Maps/Biomes/Services/PlayerSustenanceValueReader.cs) | Reads the biome bases and a Player's modifiers and produces their effective value per biome and resource. |
| [BiomeRegenerationValueReader.cs](../../../AscendantEmpire.Application/Features/Maps/Depletion/Services/BiomeRegenerationValueReader.cs) | Reads each biome's bases and Regeneration Percent, shared by the end-of-round regrowth and the support forecast. |
| [CampaignTileWater.cs](../../../AscendantEmpire.Application/Features/Maps/CampaignMaps/Services/CampaignTileWater.cs) | Whether a tile is water, from its biome and its geography. |
| [ScaledAmount.cs](../../../AscendantEmpire.Application/Helpers/ScaledNumerics/ScaledAmount.cs) | Integer arithmetic in thousandths; every amount here is one of these. |

### Application — the turn pipeline

| File | Role |
| --- | --- |
| [Gathering.cs](../../../AscendantEmpire.Application/Features/Turns/Services/TurnPipeline/Campaign/EndTurn/Gathering.cs) | End of turn: feeds the Player's Forces from the tile each ends on and adds what they eat to `Taken`. Runs at `EndCampaignTurnProcessOrder.Gathering` (10000), first of the end-of-turn processes. |
| [RegenerateTileSustenance.cs](../../../AscendantEmpire.Application/Features/Turns/Services/TurnPipeline/Campaign/EndRound/RegenerateTileSustenance.cs) | End of round: regrows every depleted tile in the Game. Runs at `EndCampaignRoundProcessOrder.RegenerateTileSustenance` (15000). |
| [StarvationUnderfedForceResolver.cs](../../../AscendantEmpire.Application/Features/Units/Services/StarvationUnderfedForceResolver.cs) | The `IUnderfedForceResolver` in use: kills and wounds troops that went short, and destroys an emptied Force. Stages writes, never saves. |
| [StarvationCasualtyCalculator.cs](../../../AscendantEmpire.Application/Features/Units/Services/StarvationCasualtyCalculator.cs) | What a Force loses to a turn without enough food, and which of its troops the loss comes out of. |
| [GameDeleteExecutor.cs](../../../AscendantEmpire.Application/Features/Game/Executors/GameDeleteExecutor.cs) | Purges the Game's depletion rows in its hand-ordered delete. |

### Application — reads

| File | Role |
| --- | --- |
| [GetTileSustenanceHandler.cs](../../../AscendantEmpire.Application/Features/Maps/Biomes/Handlers/GetTileSustenanceHandler.cs) | What is available to a Player on one tile, for the tile toolbar. Refuses any tile outside live sight. |
| [GetTileSupportLevelsHandler.cs](../../../AscendantEmpire.Application/Features/Maps/Depletion/Handlers/GetTileSupportLevelsHandler.cs) | Every observed tile's support level for a selection of Forces, for the hover marker. |
| [TileSupportEvaluator.cs](../../../AscendantEmpire.Application/Features/Maps/Depletion/Services/TileSupportEvaluator.cs) | Works out tiles' support levels in a fixed number of queries, however many tiles are asked about. |
| [TileSupportForecaster.cs](../../../AscendantEmpire.Application/Features/Maps/Depletion/Services/TileSupportForecaster.cs) | Plays feed → regrow forward against a detached copy of the depletion row — see [support-indicator.md](support-indicator.md). |

### API

Both endpoints are on
[CampaignMapController.cs](../../../AscendantEmpire.Api/Controllers/CampaignMapController.cs), and
both take the game and the Player from the game token: a Player's values, Forces and sight are their
own, and only the Force selection is the caller's to say.

| Endpoint | Action | Contracts |
| --- | --- | --- |
| `GET campaign-map/player/tiles/{x}/{y}/sustenance` | `GetTileSustenanceAsync` | `TileSustenance` (backend), `ITileSustenance` / `DTTileSustenance` (frontend) |
| `GET campaign-map/player/tiles/support?forceIds=1&forceIds=2` | `GetTileSupportLevelsAsync` | `TileSupportLevels` (backend), `ITileSupportLevels` / `DTTileSupportLevels` (frontend) |

The biome catalogue the base values come from is maintained through `BiomesController`
(`POST`/`PUT`/`GET biomes/campaign-map`), from the Entity Setup Tool's Biome Manager. The writes require
the `EntitySetupTool` permission; the read is open, so the Map Editor can show the catalogue as a
read-only reference table.

### Frontend

| File | Role |
| --- | --- |
| [tileSustenanceApi.ts](../../../AscendantEmpire.AscendantEmpireUI/src/features/map/api/tileSustenanceApi.ts) | The sustenance read, and the `TileSupportLevel` string union. |
| [tileSupportApi.ts](../../../AscendantEmpire.AscendantEmpireUI/src/features/map/api/tileSupportApi.ts) | The per-selection support-level read. |
| [useTileSustenance.ts](../../../AscendantEmpire.AscendantEmpireUI/src/features/map/hooks/useTileSustenance.ts) | Caches one tile's amounts; refetched on window focus, invalidated on turn end and on a force step. |
| [useTileSupportLevels.ts](../../../AscendantEmpire.AscendantEmpireUI/src/features/map/hooks/useTileSupportLevels.ts) | Asks once per selection, so hovering the map is a lookup rather than a request per tile. |
| [TileToolbar.tsx](../../../AscendantEmpire.AscendantEmpireUI/src/features/game/components/TileToolbar.tsx) | Shows the selected tile's Forage, Hunt, Fish and Fodder, and its support level. |
| [TileSupportMarker.tsx](../../../AscendantEmpire.AscendantEmpireUI/src/features/game/components/TileSupportMarker.tsx) | The hover marker on the map. |
| [formatSustenanceAmount.ts](../../../AscendantEmpire.AscendantEmpireUI/src/features/game/components/formatSustenanceAmount.ts) · [formatSupportTurns.ts](../../../AscendantEmpire.AscendantEmpireUI/src/features/game/components/formatSupportTurns.ts) · [tileSupportLevelClass.ts](../../../AscendantEmpire.AscendantEmpireUI/src/features/game/components/tileSupportLevelClass.ts) | Formatting, and the colour a support level is shown in. |

### Tests

`AscendantEmpire.Tests/Features/Maps/Depletion/` covers the arithmetic (`DepletionTakenTests`,
`TileFoodPoolTests`, `FoodExtractionCalculatorTests`, `ShortfallAllocatorTests`,
`TileRegenerationCalculatorTests`, `TileSupportEvaluatorTests`, `TileSupportForecasterTests`,
`GetTileSupportLevelsHandlerTests`) and the check constraint
(`CampaignMapDepletionConstraintTests`). The pipeline processes are covered by
`Features/Turns/Services/TurnPipeline/GatheringTests` and `RegenerateTileSustenanceTests`, and the
toolbar read by `Features/Maps/Biomes/GetTileSustenanceHandlerTests`.

## Process flow

### Feeding, at the end of a Player's turn (`Gathering`)

1. The turn pipeline runs `Gathering` first among the end-of-turn processes. A context with no Player
   id is refused: feeding every Player's Forces in the Game would be far worse than doing nothing.
2. Read the Player's Forces in the Game that still have troops (healthy + injured > 0) and are not on
   a Battle Map. Forces in a Battle are fighting rather than foraging, which is also what delivers
   "only one side feeds from a tile".
3. Read the distinct tiles those Forces stand on, for biome and geography, and load the tracked
   `CampaignMapDepletion` rows for them. A tile never fed on has no row, and is given one.
4. Read the Player's effective sustenance values once, through `IPlayerSustenanceValueReader`.
5. Per tile, in tile-id order:
   1. The need is the troops of every Force on the tile, at 1 troop = 1 food.
   2. `TileFoodPool` builds the pool — Forage and Hunt on land, Fish on water, and Fish on land too
      where the tile has a river — each entry at `MAX(0, effective − Taken)`, with the tile's river
      bonus added to the effective value first. A resource the tile does not have is left out
      entirely, which is not the same as one present but stripped, which is in the pool at 0.
   3. `FoodExtractionCalculator` splits the need across the pool in proportion to what each resource
      has; or, when the pool cannot cover it, takes everything and reports the rest as the shortfall.
   4. Each amount taken is added to the row through `DepletionTaken.AddTaken`.
   5. `FedOnRound` is set to the current round whether or not anything was taken: the marker records
      presence.
   6. `ShortfallAllocator` splits the tile's shortfall across its Forces by troops, so every Force on
      the tile goes equally short.
6. The per-Force need, food and shortfall go to `IUnderfedForceResolver` **before** the save, so the
   casualties commit in the same batch as the depletion that caused them — a turn can never take the
   food without the losses, or the losses without the food.
7. `SaveChangesAsync`, then one log line with the totals fed and short.

### Regrowth, at the end of the round (`RegenerateTileSustenance`)

Runs once per Game after every Player's turn, over the depletion rows with anything taken. The rules
are in [replenishment.md](replenishment.md) and live in `TileRegenerationCalculator`;
`FedOnRound == CurrentRoundNumber` is what "fed on this round" means, so nothing needs clearing for
the next round.

### Showing it to the Player

1. Selecting a tile calls `GET campaign-map/player/tiles/{x}/{y}/sustenance`.
2. `GetTileSustenanceHandler` checks the tile is in the Player's **live sight** through
   `IFieldOfViewService` — the same rules the live map read uses. An unwatched tile answers
   `isObserved: false` with no values, as a success rather than an error.
3. For a watched tile it reads the Player's effective values and the tile's `Taken` and returns
   `MAX(0, effective − Taken)` per resource, with null for a resource the biome does not have.
4. The same response carries the tile's `SupportLevel` from `ITileSupportEvaluator` with no requested
   Forces, so the level reflects only the Player's own Forces already standing there.
5. Selecting Forces calls `GET campaign-map/player/tiles/support` once for the whole selection, so
   hovering the map is a client-side lookup. A requested Force the Player does not own is refused
   rather than quietly ignored.

## Not yet implemented

The rules above that have no code yet, each carried as a TODO on the process that will own it:

- **Passing through** a tile drawing 20% of a Force's need, and marking the tile fed on. The tiles
  passed through are in the unit's movement history; the draw needs its own ordering rules against
  the end-of-turn feed.
- **`UNIT_HARVEST_CAP`**, and pillaging to exceed it.
- **Scorched earth**, including what it sets `Taken` to.
- **Food needs from unit design** rather than 1 troop = 1 food, and **Fodder** for mounted units.
- **Force gathering bonuses** reducing what a Force draws from a tile.
- **Settlements**: a hostile Force denying a Settlement its tile, settlement-zone tiles being found
  for replenishment, owned tiles regrowing against the owner's values, and Settlement-scoped
  modifiers. Who holds a tile, and what an occupier may take from it, is in
  [tile-ownership.md](tile-ownership.md).
- **Hidden Forces**: a hidden Force feeding raises the shared `Taken`, which signals that it is
  there.
- **Rerun safety**: the pipeline is not transactional, so ending a turn again after a later process
  fails would feed the Forces, or regrow the tiles, a second time.
- **Telling the Player**: troop counts simply drop when a Force starves; nothing is raised in the
  notifications panel yet.
