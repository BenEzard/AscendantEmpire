# Replenishment

Part of the [Campaign Map](README.md) design. What a tile stores, and how it is depleted, is in
[depletion.md](depletion.md).

---

# 1. Conceptual overview

Replenishment runs **once per tile at the end of each round**, after every Player's turn, for each sustenance resource with a non-`null` base.

Today this is the end-of-round `RegenerateTileSustenance` process. It reads the Game's `game.CampaignMapDepletion` rows with any `Taken` above `0` (a row regrown to `0` is kept for the next feed) and treats every tile as unowned. A biome with a sustenance base but no Regeneration Percent predates the validator rule; its tiles are skipped with a warning until the data is re-seeded.

## Inputs

| Input | Unowned tile | Owned tile (within a Player's territory) |
| --- | --- | --- |
| `Base` | The biome's base value from `map.CampaignBiomes` | The **owner's** effective value for the tile (empire-wide and Settlement-scoped modifiers) |
| `Percent` | The biome's Regeneration Percent | The biome's Regeneration Percent plus the owner's Settlement-scoped regeneration modifiers, in percentage points (a 25% biome with a +10 modifier regrows 35%) |
| `FedOn` | Whether anyone fed on the tile this round | same |

`Base` sets how much regrows, the 10% floor and the cap. Using the owner's values on an owned tile is how a Player's management of their own land shows: their tech gives a higher `Base` (more food back each round, a higher floor) and their Settlements can raise `Percent`. Wild land always recovers at the biome's values, whoever was last on it.

> TODO: Settlement-scoped regeneration modifiers wait on Settlements, as Settlement-scoped sustenance modifiers do.

A tile was **fed on** this round if any Force ended its turn on it, or passed through it (the 20% draw), **whether or not there was anything left to take**. It depends on presence, not on food extracted.

> TODO: Whether a Settlement's population feeding on its own territory counts as feeding on the tile is deferred to the Settlement mechanics. A hostile Force **can** deny a Settlement the food it was drawing from a tile, which is what makes a ring of occupied tiles a siege — see [tile-ownership.md](tile-ownership.md#occupation).

## Calculation

Two rules, in order:

```
1. Regrow, every round:            Taken = MAX(0, Taken − Percent × Base)
2. Floor, only if nobody fed on it: Taken = MIN(Taken, 0.9 × Base)
```

- **Rule 1** gives back a fixed share of `Base` every round, whether or not the tile is being fed on, capped at `Base` (so `Taken` never goes below `0`). A bare tile regrows too, so a Force camping on it gets `Percent × Base` a round. Anything a stronger Player took above `Base` is paid off first, like any other `Taken`.
- **Rule 2** makes a tile nobody fed on this round recover to **at least 10% of `Base`**. It is a floor, not a replacement: the tile keeps whichever of the two leaves more food, so a biome that regrows more than 10% a round keeps its full regrowth.

`FedOn` is `FedOnRound` equal to the round that is ending, so nothing needs clearing for the next round. Amounts are in thousandths, and rounding always goes against the tile: the regrowth is rounded down and `0.9 × Base` is rounded up.

> **Why Rule 2 exists.** `Taken` is shared, but each Player measures it against their own value. A stronger Player can eat a tile down past `Base`, to their own higher value, and Rule 1 pays that surplus off before anything reappears at base values. Without Rule 2 a Player with lower values would see nothing on the tile for several rounds after the stronger Player left (at 10% a round, a surplus of 25% of `Base` takes three rounds to pay off). Rule 2 forgives the surplus once nobody is feeding there, so a Player with lower values can eat on the tile the very next round.
>
> **Why Rule 2 only applies when nobody fed on the tile.** Applied while a Force is still there, it would hand a stronger Player camping on the tile their surplus back every round: they eat down to their own value, the floor puts the tile back to 10% of `Base`, and their whole surplus reappears for them. The surplus grows with tech (at +100% it is a whole tile's worth every round), which would break denial — scorched earth, stripping a tile ahead of an enemy, starving besiegers — against stronger Players. This is why `FedOnRound` is kept.
>
> **Why `Base` rather than the highest value of any Player.** Regrowth and the floor are measured against `Base` (or the owner's value on an owned tile). Measuring them against the highest value any Player has would make every Player's land recover faster because of the best Player's tech.

## Worked examples

Illustrative values: an unowned tile, Hunt `Base` 80, Regeneration Percent 10% (8 a round). Player **Default** has Hunt 80 (no modifiers); Player **Advanced** has Hunt 100 (+25%).

**Default, then Advanced.**

| Step | `Taken` | Default sees | Advanced sees |
| --- | --- | --- | --- |
| Untouched | 0 | 80 | 100 |
| Default takes 30 | 30 | 50 | 70 |
| Round end, fed on: regrow `30 − 8` | 22 | 58 | 78 |
| Default leaves; Advanced arrives and takes 30 | 52 | 28 | 48 |

The order of arrival makes no difference: had Advanced taken the first 30, every number would be the same.

**Default strips the tile; Advanced camps on it, then leaves.**

| Step | `Taken` | Default sees | Advanced sees |
| --- | --- | --- | --- |
| Default takes 80 | 80 | 0 | 20 |
| Round end, fed on: regrow `80 − 8` | 72 | 8 | 28 |
| Advanced arrives and takes 28 | 100 | 0 | 0 |
| Round end, fed on: regrow `100 − 8`, paying off Advanced's surplus | 92 | 0 | 8 |
| Advanced leaves; round end, nobody fed on it: `MIN(92 − 8, 0.9 × 80)` | 72 | 8 | 28 |
| Round end, nobody fed on it: regrow `72 − 8` | 64 | 16 | 36 |

Without Rule 2, the round after Advanced left would end at `Taken` 84 and Default would still see nothing.

**Owned tile.** The same tile owned by Advanced uses `Base` 100, so it regrows 10 a round where wild land regrows 8. A Settlement-scoped +5 point regeneration modifier would make it 15.

## Accepted behaviour

- **A small Force can live on a tile indefinitely** by eating no more than the tile regrows: `Percent × Base` a round, per resource. For example, Hunt 30 sustains 3 a round at 10%, or 15 at 50%.
- **Staying on a tile does not deny it.** A bare tile still regrows `Percent × Base` every round, and whoever is on it gets that much. Denial comes from eating the regrowth as it arrives.
- **A stronger Player's surplus is paid off before a weaker Player sees food** for as long as someone keeps feeding there. A round left alone forgives it (Rule 2).
- **Recovery time depends on the percentage, not the size of the tile.** A bare tile left alone is back to full in about `1 ÷ Percent` rounds (10 rounds at 10%, 4 at 25%), and is never below 10% after its first round alone.
- **A base of `0`.** If a Player's modifiers give them a value in a biome whose base is `0` (and not `null`), an unowned tile regrows nothing: anything taken stays while the tile is fed on, and resets to `Taken = 0` after a round left alone.

---

# 2. Technical detail

Replenishment writes to exactly one table, and to only the four `*Taken` columns of it. It reads no
Player state at all today, because every tile is treated as unowned — which is why it is a
per-Game process rather than a per-Player one.

## Database tables

| Schema | Table | Purpose |
| --- | --- | --- |
| `game` | `CampaignMapDepletion` | The rows regrown. Read for what has been taken and the last round the tile was fed on; the `*Taken` columns are written back. |
| `game` | `CampaignMap` | The tile. Read for its `BiomeTypeId`, which decides the biome whose `Base` and Regeneration Percent apply. |
| `map` | `CampaignBiomes` | Read for each biome's four sustenance bases and its Regeneration Percent. |

### `game.CampaignMapDepletion`

The full column list is in [depletion.md](depletion.md#gamecampaignmapdepletion). What replenishment
does with each:

| Column | Read or written | Role here |
| --- | --- | --- |
| `CampaignMapId` | read | Identifies the tile, so its biome can be looked up. Also the ordering key, so a log read back later follows a stable order. |
| `GameId` | read (filter) | Every depleted tile in the Game is regrown in one pass. |
| `ForageTaken`, `HuntTaken`, `FishTaken`, `FodderTaken` | read **and written** | The amount regrown is subtracted from each, per the two rules. A resource whose biome base is `null` is skipped and left exactly as it was. |
| `FedOnRound` | read | `FedOnRound == CurrentRoundNumber` is what "fed on this round" means, which decides whether Rule 2 applies. Never written here, so nothing needs clearing for the next round. |

Rows are loaded **tracked**, since they are about to be written, and filtered to those with something
taken: `ForageTaken > 0 OR HuntTaken > 0 OR FishTaken > 0 OR FodderTaken > 0`. A row already at `0`
has nothing to regrow whether or not it was fed on, and is left in place to be reused the next time a
Force feeds there. The check constraint `CK_CampaignMapDepletion_TakenNonNegative` is what catches a
regrowth that overshot; `TileRegenerationCalculator` floors at `0` so it never can.

### Columns read from the other tables

| Table | Columns used | Read for |
| --- | --- | --- |
| `game.CampaignMap` | `CampaignMapId`, `BiomeTypeId` | Which biome's values regrow the tile. Scalars only — the biome is all that is needed off the tile. |
| `map.CampaignBiomes` | `BiomeTypeId`, `BaseForageValue`, `BaseHuntValue`, `BaseFishValue`, `BaseFodderValue`, `RegenerationPercent` | `Base` and `Percent`. A `null` base means the biome does not have the resource, so it is never regenerated. |

`RegenerationPercent` is a `decimal(18,3)` fraction (`0.25` is 25%); the base values are `bigint`
scaled by `ScaledAmount.Scale` (1,000). See [food-yield.md](food-yield.md) for the biome columns in
full and the rules the validator enforces on them.

## Key code files

| File | Role |
| --- | --- |
| [RegenerateTileSustenance.cs](../../../AscendantEmpire.Application/Features/Turns/Services/TurnPipeline/Campaign/EndRound/RegenerateTileSustenance.cs) | The end-of-round process: reads the depleted tiles and their biomes, calls the calculator per tile and resource, and saves. Runs at `EndCampaignRoundProcessOrder.RegenerateTileSustenance` (15000). |
| [TileRegenerationCalculator.cs](../../../AscendantEmpire.Application/Features/Maps/Depletion/Services/TileRegenerationCalculator.cs) | The two rules, and nothing else. Pure and database-free. `_REFILL_DIVISOR = 10` is the 10% floor, expressed as the divisor taken off `Base`. |
| [IBiomeRegenerationValueReader.cs](../../../AscendantEmpire.Application/Features/Maps/Depletion/Services/IBiomeRegenerationValueReader.cs) · [BiomeRegenerationValueReader.cs](../../../AscendantEmpire.Application/Features/Maps/Depletion/Services/BiomeRegenerationValueReader.cs) | Reads every biome's four bases and Regeneration Percent in one projection. Shared with the support forecast, so the forecast regrows a tile by exactly what this process will. |
| [BiomeRegenerationValues.cs](../../../AscendantEmpire.Application/Features/Maps/Depletion/Classes/BiomeRegenerationValues.cs) | One biome's `Percent` and four bases, with `GetBaseValue(resource)` mapping a resource to its column. |
| [DepletionTaken.cs](../../../AscendantEmpire.Application/Features/Maps/Depletion/Services/DepletionTaken.cs) | `GetTaken` / `SetTaken` — the one place a `SustenanceResource` is mapped to its `*Taken` column, shared with the feed. |
| [CampaignMapDepletion.cs](../../../AscendantEmpire.Domain/Entities/Games/CampaignMapDepletion.cs) | The row regrown. |
| [CampaignBiome.cs](../../../AscendantEmpire.Domain/Entities/Maps/CampaignMaps/CampaignBiome.cs) | `RegenerationPercent`, and the rules tying it to having sustenance at all. |
| [ScaledAmount.cs](../../../AscendantEmpire.Application/Helpers/ScaledNumerics/ScaledAmount.cs) | Integer arithmetic in thousandths. `Divide` rounds down, which is what makes `0.9 × Base` round up. |
| [TileSupportForecaster.cs](../../../AscendantEmpire.Application/Features/Maps/Depletion/Services/TileSupportForecaster.cs) | The other caller of the calculator: plays feed → regrow forward to forecast how long a tile lasts — see [support-indicator.md](support-indicator.md). |
| [EndCampaignRoundProcessOrder.cs](../../../AscendantEmpire.Application/Features/Turns/Services/TurnPipeline/Campaign/EndRound/EndCampaignRoundProcessOrder.cs) | Where the position in the end-of-round sequence is declared. |

There is no API endpoint and no frontend code: replenishment is invisible except through the amounts
the tile toolbar and the support indicator report afterwards, both covered in
[depletion.md](depletion.md).

### Tests

| File | Covers |
| --- | --- |
| `AscendantEmpire.Tests/Features/Maps/Depletion/TileRegenerationCalculatorTests` | The two rules on their own: regrowth, the cap at `Base`, the 10% floor, that a fed-on tile does not get the floor, the rounding direction, and a `Base` of `0`. |
| `AscendantEmpire.Tests/Features/Turns/Services/TurnPipeline/RegenerateTileSustenanceTests` | The process: which rows are read, that a `null` base is skipped, the warnings for missing tiles and biomes, and what is saved. |
| `AscendantEmpire.Tests/Features/Maps/Depletion/CampaignMapDepletionConstraintTests` | The non-negative check constraint the floor at `0` keeps satisfied. |

## Process flow

`RegenerateTileSustenance` runs once per Game at the end of a round, after every Player's turn —
after `HealTroops` (the wounded recover first) and before `EvaluateVictoryConditions` (which judges
the state everything else leaves behind).

1. Read the Game's tracked `CampaignMapDepletion` rows with any `*Taken` above `0`. Nothing taken
   anywhere in the Game means there is nothing to do, and the process returns.
2. Read the `BiomeTypeId` of each of those tiles, as scalars.
3. Read every biome's bases and Regeneration Percent through `IBiomeRegenerationValueReader`.
4. Per row, in `CampaignMapId` order:
   1. A tile or biome that cannot be found is a data defect. It is logged as a warning and skipped —
      failing here would stop the round from ending at all.
   2. A biome with no Regeneration Percent is likewise skipped with a warning. Something was taken, so
      the biome has a sustenance base, and the validator requires a Percent wherever there is one:
      this is data saved before that rule existed, and re-seeding fixes it.
   3. `fedOn` is `depletion.FedOnRound == context.CurrentRoundNumber`.
   4. For each of the four resources, a `null` base means the biome does not have it, so it is skipped
      and its `Taken` is untouched. Otherwise `TileRegenerationCalculator.Regenerate(taken, base, percent, fedOn)`
      returns the new amount and `DepletionTaken.SetTaken` writes it back:
      - **Rule 1** subtracts `floor(Base × Percent)` from `Taken`, floored at `0`.
      - **Rule 2**, only when `fedOn` is false, also caps `Taken` at `Base − floor(Base ÷ 10)`, and the
        tile keeps whichever of the two leaves less taken.
5. `SaveChangesAsync`, then one log line with the number of tiles regrown.

Feeding always happens before the round's regrowth, whatever the turn order, so `feed → regrow → feed`
holds for every Player — which is why the support forecast can play the same two steps forward and
get the same answer.

## Not yet implemented

- **Owned tiles.** Every tile is regrown as unowned: `Base` is the biome's base value and
  `Percent` is the biome's Regeneration Percent. An owned tile should use the owner's effective
  values as `Base`, and the biome's Percent plus the owner's Settlement-scoped regeneration
  modifiers. `TileSupportForecaster` regrows through the same values and must follow; that this
  reveals an owner's tech to anyone forecasting on their land is accepted deliberately — see
  [tile-ownership.md](tile-ownership.md#occupation).
- **Settlement-scoped regeneration modifiers.** `PlayerSustenanceModifier` has no `SettlementId`
  scope yet.
- **Settlement-zone tiles** being found for replenishment at all.
- **Whether a Settlement's population feeding on its own territory counts as feeding on the tile**,
  which decides whether Rule 2 applies to it.
- **Scorched earth** setting `Taken`, which is undecided — see [depletion.md](depletion.md).
- **Rerun safety.** The pipeline is not transactional, so ending the round again after a later
  process fails would regrow every tile a second time. A per-round regenerated stamp waits on the
  pipeline's retry story being settled.
