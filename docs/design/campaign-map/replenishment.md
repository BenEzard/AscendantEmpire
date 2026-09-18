# Replenishment

Part of the [Campaign Map](README.md) design. What a tile stores, and how it is depleted, is in
[depletion.md](depletion.md).

Replenishment runs **once per tile at the end of each round**, after every Player's turn, for each sustenance resource with a non-`null` base.

Today this is the end-of-round `RegenerateTileSustenance` process. It reads the Game's `game.CampaignMapDepletion` rows with any `Taken` above `0` (a row regrown to `0` is kept for the next feed) and treats every tile as unclaimed. A biome with a sustenance base but no Regeneration Percent predates the validator rule; its tiles are skipped with a warning until the data is re-seeded.

## Inputs

| Input | Unclaimed tile | Claimed tile (within a Player's territory) |
| --- | --- | --- |
| `Base` | The biome's base value from `map.CampaignBiomes` | The **claimant's** effective value for the tile (empire-wide and Settlement-scoped modifiers) |
| `Percent` | The biome's Regeneration Percent | The biome's Regeneration Percent plus the claimant's Settlement-scoped regeneration modifiers, in percentage points (a 25% biome with a +10 modifier regrows 35%) |
| `FedOn` | Whether anyone fed on the tile this round | same |

`Base` sets how much regrows, the 10% floor and the cap. Using the claimant's values on a claimed tile is how a Player's management of their own land shows: their tech gives a higher `Base` (more food back each round, a higher floor) and their Settlements can raise `Percent`. Wild land always recovers at the biome's values, whoever was last on it.

> TODO: Settlement-scoped regeneration modifiers wait on Settlements, as Settlement-scoped sustenance modifiers do.

A tile was **fed on** this round if any Force ended its turn on it, or passed through it (the 20% draw), **whether or not there was anything left to take**. It depends on presence, not on food extracted.

> TODO: Whether a Settlement's population feeding on its own territory counts as feeding on the tile is deferred to the Settlement mechanics.

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
> **Why `Base` rather than the highest value of any Player.** Regrowth and the floor are measured against `Base` (or the claimant's value on a claimed tile). Measuring them against the highest value any Player has would make every Player's land recover faster because of the best Player's tech.

## Worked examples

Illustrative values: an unclaimed tile, Hunt `Base` 80, Regeneration Percent 10% (8 a round). Player **Default** has Hunt 80 (no modifiers); Player **Advanced** has Hunt 100 (+25%).

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

**Claimed tile.** The same tile claimed by Advanced uses `Base` 100, so it regrows 10 a round where wild land regrows 8. A Settlement-scoped +5 point regeneration modifier would make it 15.

## Accepted behaviour

- **A small Force can live on a tile indefinitely** by eating no more than the tile regrows: `Percent × Base` a round, per resource. For example, Hunt 30 sustains 3 a round at 10%, or 15 at 50%.
- **Staying on a tile does not deny it.** A bare tile still regrows `Percent × Base` every round, and whoever is on it gets that much. Denial comes from eating the regrowth as it arrives.
- **A stronger Player's surplus is paid off before a weaker Player sees food** for as long as someone keeps feeding there. A round left alone forgives it (Rule 2).
- **Recovery time depends on the percentage, not the size of the tile.** A bare tile left alone is back to full in about `1 ÷ Percent` rounds (10 rounds at 10%, 4 at 25%), and is never below 10% after its first round alone.
- **A base of `0`.** If a Player's modifiers give them a value in a biome whose base is `0` (and not `null`), an unclaimed tile regrows nothing: anything taken stays while the tile is fed on, and resets to `Taken = 0` after a round left alone.
