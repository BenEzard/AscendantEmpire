# Infrastructure (Terrain Improvements)

Part of the [Campaign Map](README.md) design.

## Concept

- Infrastructure is built on campaign tiles **surrounding a settlement**.
- Selection and construction happen through the **Settlement UI** construction list.
- Initial and upkeep costs are **empire-based** and can be modified through techs.

## Inputs

- **Mandatory input** — one or more assets required for the infrastructure to function. If only a
  portion is available, output is **pro-rata**.
- **Optional input** — one or more assets that *improve* functioning. If only a portion is
  available, the improvement is pro-rata.

Inputs are divided into **classes**; for each class the best available option is chosen (by highest
desirability value), each option conferring a different bonus.

## Properties of an infrastructure

| Property | Description |
| --- | --- |
| Code Name | Generic API name |
| Empire Display Name | Name according to the empire |
| Initial Cost | Assets (0+) required to build; normally includes a **Population** amount to staff it (e.g. 50 wood) |
| Effort Cost | Effort required to build |
| Upkeep Cost | Assets (0+) required per turn to keep operational. (Possibly too complicated — undecided. If run by slaves, they need topping up due to attrition.) |
| Requirements | Prerequisite technologies, tile types, terrain improvements, or other rules. E.g. roads need road tech (kept forever once discovered); a hydrodam needs a river tile; an offshore oilrig needs an ocean tile. |
| Specialist Class | If specialists can inhabit it, what kind (e.g. FARMER, ENGINEER). The class sets the progression path (e.g. Blacksmith → Journeyman → Mastersmith). |
| Specialist Slot Count | Number of specialists employable |
| Base Output (in "units") | Base output. May be split by an output slider if the infrastructure can output more than one asset type (e.g. a blacksmith producing mining vs farming equipment from 100 base units, split 70/30 or 20/80). |
| Mandatory Input(s) | Divided into classes; best available chosen |
| Optional Input(s) | Divided into classes; best available chosen |

See [manufacturing-chains.md](../manufacturing-chains.md) for fully worked farm and blacksmith
examples.
