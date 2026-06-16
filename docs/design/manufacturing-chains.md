# Manufacturing Chains

Infrastructure turns raw inputs into outputs through a **manufacturing chain**. This document gives
the worked examples; for the generic properties of infrastructure see
[campaign-map.md](campaign-map.md#infrastructure-terrain-improvements).

## Definitions

To function (produce goods):

- **all mandatory inputs** must be met, and
- for any **optional input**, at least one option from each **option group** must be met.

Within an option group, the option with the **highest Desirability Index** that is available (and
unlocked) is chosen. If only a portion of a required input is available, output is **pro-rata**.

## Example: Farm

| Property | Value | Note |
| --- | --- | --- |
| Code Name | `[CROP]_FARM` | |
| Empire Display Name | `[CROP] Farm` | |
| Initial Cost | X wood, Y stone, Z population | |
| Effort Cost | | |
| Upkeep Cost | Assets (0+) per turn to keep operational | |
| Requirements | Infrastructure: Irrigation (tile must be irrigated first); an asset of type CROP or STOCK | e.g. Wheat, Barley, Horse, Sheep |
| Specialist Class | CROP → `FARMER`; STOCK → `GRAZIER` | |
| Specialist Slot Count | 5 | |
| Base Output | X | |
| Mandatory Input(s) | None | |
| Optional Input(s) | e.g. a Machinery class (choosing desirability #4, a combine harvester, uses a more expensive asset and more fuel than a tractor but yields more); animal manure costs nothing per turn (beyond the tech) but yields less than chemical fertilizer | Open question: does chemical fertilizer slowly degrade the soil? |

### Calculations

At the start of the turn, all negotiated trade deals are settled first. (There is some stickiness in
exactly how/when trade should be done.)

- For each settlement:
  - Look up its associated Resource Bank, then do all calculations on the aggregated values for all
    settlements that share that bank.
  - For each type of farm: …

## Example: Blacksmith

| Property | Value | Note |
| --- | --- | --- |
| Specialist Slots | 2 | Allows: Peasant, Amateur, Journeyman, Master, Renowned Blacksmith |
| Base Production Amount | 100 | |
| Construction Requirement(s) | Initially `MAX_BLACKSMITH_PER_SETTLEMENT = 1`. After the **Blacksmith's Guild** tech, a new rule activates (≥ 1 Blacksmith's Guild building per 10 Blacksmith's Shops) and `MAX_BLACKSMITH_PER_SETTLEMENT = 5`. | |

### Inputs

**Option group: fuel** — choose the highest available Desirability Index.

| Fuel | Desirability | Amount Required | Pollution Factor |
| --- | --- | --- | --- |
| Gas | 4 | 6 | 0 |
| Coke | 3 | 4 | 3 |
| Coal | 2 | 6 | 5 |
| Charcoal | 1 | 10 | 7 |

**Option group: material** — choose the highest available Desirability Index.

| Material | Desirability | Amount Required |
| --- | --- | --- |
| Ingot | 3 | 9 |
| Ore | 2 | 12 |
| Scrap metal | 1 | 10 |

### Specialist progression

| Specialist | Training Points | Properties | Conditions |
| --- | --- | --- | --- |
| Peasant | | None | (Not a specialist.) |
| Amateur Blacksmith | 300 | +5% productivity | Requires tech Blacksmith's Guild. After 3 turns, a Peasant becomes Amateur. |
| Journeyman Blacksmith | 1,200 | B-Grade output; +10% productivity | Requires tech Blacksmith's Guild. After 9 turns, an Amateur becomes Journeyman. |
| Master Blacksmith | 5,000 | A-Grade output; −5% amount required; +20% productivity | Requires tech Blacksmith's Guild. After 9 turns, a Journeyman has a 5% chance per turn of becoming Master. |
| Renowned Blacksmith | | A-Grade output; +20% productivity; −5% amount required; +2 culture/turn | Requires building Grand Blacksmith's Guild. After 5 turns, a Master has a 0.5% chance of becoming Renowned. |

### Calculation

Determine the **fuel** source (by Desirability Index and available assets), then the **material**
source the same way:

1. Is the option unlocked **and** do we hold `≥ [Amount Required]` in the Resource Bank? If yes, use
   it; else continue down the list.
2. `Production Amount = [Base Production Amount]` (then modified by the chosen specialist and input
   bonuses).

### Output

A blacksmith's output is split via a **slider group**, e.g.:

- horse equipment
- mining tools
- farming tools
- …or various weapons/armour
