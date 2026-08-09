# Ascendant Empire — Design

This folder captures the game design for **Ascendant Empire**, a turn-based 4X strategy game.
Each document covers one area of the design. The content here is the *design intent* — some of it
is settled, some of it is still a working sketch with open questions called out inline.

> These documents were distilled from the original `Empire.docx` design notes. Where the source
> raised a question or left something unresolved, it has been preserved (see
> [open-questions.md](open-questions.md) and the **Open questions** sections in each file).

---

## Contents

| Document | Covers |
| --- | --- |
| [design-principles.md](design-principles.md) | The guiding philosophy ("fun over accuracy"). |
| [starting-requirements.md](starting-requirements.md) | The catalogue content a database needs before a game can be created: the settler and scout tags, components and templates. |
| [turn-structure.md](turn-structure.md) | The parallel "turn-based" model, rosters, and how battle rounds interleave with campaign turns. |
| [production-and-assets.md](production-and-assets.md) | Assets, Material Classes, Material Preferences, luxury goods, and resources. |
| [units-and-formations.md](units-and-formations.md) | Units, Sub Units, Formations, Unit Stacks, unit design, components, and obsolescence. |
| [unit-support-and-supply.md](unit-support-and-supply.md) | Supply chains and the four support types: fuel, ammunition, food, and medical. |
| [campaign-map.md](campaign-map.md) | The campaign map, tile geography and biome, resource depletion/regeneration/discovery, vision (sight & detection), and infrastructure. |
| [battlefield-maps.md](battlefield-maps.md) | How battlefield maps are generated, fortifications, battle flow, objectives, and manoeuvres. |
| [unit-mechanics.md](unit-mechanics.md) | Movement, positioning/facing, tactics, fog of war, strength, and ranged attack. |
| [settlements.md](settlements.md) | Settlement types, asset banks, and buildings. |
| [specialists.md](specialists.md) | Specialists and military commanders. |
| [technology.md](technology.md) | The three-bucket research model and the seven methods of technological advancement. |
| [foreign-relations.md](foreign-relations.md) | Inter-empire relationships and diplomatic arrangements. |
| [departments-advisors.md](departments-advisors.md) | Departments (the evolving "advisors" system). |
| [manufacturing-chains.md](manufacturing-chains.md) | Infrastructure manufacturing chains, with farm and blacksmith examples. |
| [early-game.md](early-game.md) | The nomadic early game and the path to settling. |
| [map-generation.md](map-generation.md) | Generating the campaign map from a source image. |
| [open-questions.md](open-questions.md) | Cross-cutting unresolved design questions and "unorganised thoughts". |

---

## Vocabulary at a glance

A few terms recur throughout these documents:

- **Empire** — a player (human or AI).
- **Asset** — anything collectable or manufacturable in the game, excluding Units.
- **Material Class** — a generic category of asset (e.g. `CONSTRUCTION_STONE`, `SWORD_MELEE`) that
  concrete assets belong to.
- **Unit** — multiple soldiers with the same equipment, training, and nationality.
- **Formation** — a group of Units combined under a command structure on a single campaign tile.
- **Campaign Map** — the strategic world map made up of tiles.
- **Battlefield Map** — the tactical map generated when a battle occurs on a campaign tile.
