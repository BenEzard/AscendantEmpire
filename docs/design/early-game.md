# Early Game

The early game is deliberately **nomadic**. Founding a settlement confers **no immediate benefit**
over staying nomadic — the player must first find a food-rich environment before settling (based on
having enough food for *x* turns).

Consequently, early technologies have acquisition costs that are **mainly food** and very small
**directed-research** requirements.

## Worked walkthrough

The player starts with **3× Roving Band (300 people)** on a "good" tile producing **400 food**.

**Two viable openings:**

| | Stay together | Split into three |
| --- | --- | --- |
| **Turn 1** | The 300-person unit forages on the good tile. Foraging takes 97% energy and yields 1.05 productivity → **315 food extracted, 85 remaining**. Foraging by 300 people produces **30 action research points** (10%) toward any FORAGING-type tech. `CREATE_CAMP` is unavailable because the tile won't provide a *surplus* after 10 turns. Turn ends. Food surplus (315 > 300) → population **+5%** (`BIRTH_RATE`). Tile regenerates 25% of its base (100 food), capped at 400. | The player splits into 3 bands of 100, each on a separate "good" tile. Each forages **105 food**, leaving 295 per tile. Still 30 research points total. `CREATE_CAMP` still unavailable. Same population growth and tile regeneration apply. |
| **Turn 2** | The player is warned the tile can't feed the tribe and must move to avoid starvation. (Tile would have `85 + 100 = 185`; population is 315.) | Each tile would have `295 + 100 = 395`; population per band is 105. Each band forages **110.25 food**, leaving ~285. Surplus → +5% population; tiles regenerate as above. |

The split opening sustains the population longer because no single tile is over-grazed — illustrating
the [resource-depletion](campaign-map/depletion.md) pressure that drives early
nomadism.

## Path to settling

Once a food-rich location is secured, the early build-out is:

1. **Create a Camp.**
2. **Build a Meeting Place** (the first [building](settlements.md#buildings)), which outputs craft
   materials.
